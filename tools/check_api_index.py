#!/usr/bin/env python3
"""check_api_index.py — гейт «индекс API ↔ код» (DO-064, спека @AR 2026-11-11).

Что проверяет:
  1. Набор операций индекса (`04-architecture/api-index.yaml`) == набор маршрутов
     в контроллерах `6-msa/BC-*/**/*Controller.cs`. Расхождение в любую сторону → rc=1
     с конкретной парой (метод, путь).
  2. У каждой операции индекса заполнен `codes` (иначе rc=1).
  3. `auth: anonymous` ровно у 4 контроллерных маршрутов allow-list
     (register / verify / refresh / verify-token). `GET /health` исключён — не контроллер.

Зачем: индекс — это реестр контракта (ADR-025), и «индекс отстал от кода» иначе
обнаруживается глазами. Правило изменения индекса (правка + гейт в одном коммите)
записано в самом файле.

Почему без PyYAML: индекс — плоский список простых пар, а зависимость в CI дороже
20 строк разбора; формат жёстко задан владельцем и проверяется этим же гейтом.

    python tools/check_api_index.py            # зелёный/красный по репозиторию
    python tools/check_api_index.py --index f  # другой индекс (для негативного контроля)
    python tools/check_api_index.py --selftest
"""
import argparse
import os
import re
import shutil
import sys

INDEX = "04-architecture/api-index.yaml"
ANON_ALLOW = {
    ("POST", "/api/v1/clients"),
    ("POST", "/api/v1/clients/refresh"),
    ("POST", "/api/v1/clients/{id}/verify"),
    ("POST", "/api/v1/privacy/verify-token"),
}
NORM = re.compile(r"\{(\w+)(?::[a-z]+)?\}")

# Область проверки анонимности (ADR-026, @AR 2026-11-12 23:52): 6 BC с auth-стеком.
# Остальные 5 (Catalog, SomaticSeries, DailyMetrics, Portability, Analytics) доверяют
# шлюзу по ADR-008 — там auth-стека нет ОСОЗНАННО, и требовать fallback нельзя:
# он закрыл бы всё без разбора. Расширение списка — триггер @BE (AddAuthentication),
# тогда гейт проверяет 11 BC.
AUTH_BCS = {
    "BC-Client", "BC-Journal", "BC-Partners", "BC-Privacy",
    "BC-GarminGateway", "BC-AppleHealthGateway",
}


def norm_path(p):
    p = p.strip().strip('"').strip("'")
    p = NORM.sub(lambda m: "{%s}" % m.group(1), p)
    if not p.startswith("/"):
        p = "/" + p
    return p.rstrip("/") or "/"


def parse_index(path):
    ops, cur = [], None
    for line in open(path, encoding="utf-8"):
        s = line.rstrip("\n")
        m = re.match(r"\s*-\s*bc:\s*(.+)$", s)
        if m:
            if cur:
                ops.append(cur)
            cur = {"bc": m.group(1).strip()}
            continue
        if cur is None:
            continue
        m = re.match(r"\s+(method|path|operationId|auth|codes|codes_status|internal):\s*(.*)$", s)
        if m:
            cur[m.group(1)] = m.group(2).strip()
            continue
        # AR-022 (@AR 2026-11-07 08:49): `request` — многострочный блок, поэтому прежний
        # построчный парсер его не видел вовсе, и правило «POST обязан иметь request»
        # не могло работать (давало 54 ложных нарушения при заполненном индексе).
        m = re.match(r"\s+request:\s*(.*)$", s)
        if m:
            cur["request"] = m.group(1).strip() or "<block>"
    if cur:
        ops.append(cur)
    return ops


def parse_code(root):
    """Маршруты из контроллеров: [(method, path, anonymous, file)]."""
    found = []
    for dirpath, _, names in os.walk(os.path.join(root, "6-msa")):
        for n in names:
            if not n.endswith("Controller.cs"):
                continue
            f = os.path.join(dirpath, n)
            try:
                text = open(f, encoding="utf-8-sig").read()
            except OSError:
                continue
            base = re.search(r'\[Route\("([^"]*)"\)\]', text)
            base_path = base.group(1) if base else ""
            for mm in re.finditer(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', text):
                verb = mm.group(1).upper()
                suffix = mm.group(2) or ""
                path = norm_path("/" + base_path.strip("/") + ("/" + suffix.strip("/") if suffix else ""))
                tail = text[mm.end():mm.end() + 300]
                # было `... if "public " in tail else False`: при длинном наборе атрибутов
                # окно 300 символов не доходило до `public `, и маршрут молча считался
                # НЕ анонимным — так `POST /api/v1/clients` давал ложный red на всём main.
                anon = "[AllowAnonymous]" in tail.split("public ")[0]
                found.append((verb, path, anon, os.path.relpath(f, root)))
    return found


def auth_scope(root):
    """(с fallback, без fallback, расширенные) по BC: машинная проверка ADR-026."""
    ok, broken, extended = [], [], []
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return ok, broken, extended
    for name in sorted(os.listdir(base)):
        if not name.startswith("BC-"):
            continue
        has_auth = has_fallback = False
        for dirpath, _, names in os.walk(os.path.join(base, name)):
            for n in names:
                if n != "Program.cs":
                    continue
                try:
                    t = open(os.path.join(dirpath, n), encoding="utf-8-sig").read()
                except OSError:
                    continue
                has_auth = has_auth or "AddAuthentication" in t
                has_fallback = has_fallback or "RequireAuthenticatedUser" in t
        if name in AUTH_BCS:
            (ok if has_fallback else broken).append(name)
        elif has_auth:
            extended.append(name)
    return ok, broken, extended



def check_post_request(ops):
    """AR-022 (@AR 2026-11-07 08:49): у каждой POST-операции должен быть блок `request:`.

    «Тела нет» — тоже утверждение о контракте, поэтому допустим `request: {required: []}`
    (пример: `Journal.StartManualEntry` — `clientId` берётся из токена, тела нет).
    Отсутствие блока `request` целиком — нарушение: следующая команда иначе появится
    без тела, и клиент снова будет угадывать форму запроса.
    """
    bad = []
    for o in ops:
        if str(o.get("method", "")).upper() != "POST":
            continue
        if "request" in o:
            continue
        bad.append(o.get("path") or o.get("operationId") or "<без пути>")
    return bad


# 2026-11-10 (@DO) по решению @AR `13:46`: ownership НЕ кодируется статусом.
# «не твой» и «не существует» неразличимы ⇒ только `404`. `403` законен лишь там, где
# ответ НЕ зависит от существования чужого объекта (сравнение с `sub`/ролью ДО запроса).
# Машинная форма: `403` у операции с `{id}` в пути допустим только для allowlist ниже
# и для `/internal/**` (там `403` маршрутный: вопрос «что позволено вызвать», не «чей объект»).
OWNERSHIP_403_ALLOW = {
    # причина обязательна: иначе allowlist превращается в «где нам удобно»
    "Client.Get": "ClientsController.cs:109 — сравнение id с sub ДО `_mediator.Send`, "
                  "существование чужой строки не раскрывается (@AR 13:46 п.2)",
}


def check_ownership_codes(ops):
    """@AR `13:46`: `403` у ресурсной операции = утечка существования. Возвращает нарушения."""
    bad = []
    for o in ops:
        path = (o.get("path") or "")
        codes = str(o.get("codes") or "")
        if "{" not in path or "403" not in codes:
            continue
        if path.startswith("/internal/"):
            continue
        oid = o.get("operationId") or (o.get("method", "") + " " + path)
        if oid in OWNERSHIP_403_ALLOW:
            continue
        bad.append((oid, o.get("method"), path))
    return bad



# 2026-11-10 (@DO) по запросу @AR `13:49`: сверка «канон ↔ код» без чтения глазами.
# `codes` операции обязаны покрывать явные возвраты действия + коды middleware.
# Механику «anonymous ⇒ нет 401» @AR сам признал неверной (`Client.Refresh` возвращает
# `Unauthorized()` из действия), поэтому смотрим НА КОД действия, а не на `auth`.
RET_TO_CODE = {
    "Ok(": 200, "NoContent(": 204, "BadRequest(": 400, "Unauthorized(": 401,
    "Forbid(": 403, "NotFound(": 404,
}


def code_codes_by_route(root):
    """{ (METHOD, path): {"returns": {коды явных возвратов}, "attr": {коды атрибутов}, "file": ... } }."""
    out = {}
    for dirpath, _, names in os.walk(os.path.join(root, "6-msa")):
        for n in names:
            if not n.endswith("Controller.cs"):
                continue
            f = os.path.join(dirpath, n)
            try:
                text = open(f, encoding="utf-8-sig").read()
            except OSError:
                continue
            base = re.search(r'\[Route\("([^"]*)"\)\]', text)
            base_path = base.group(1) if base else ""
            hits = list(re.finditer(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', text))
            for i, mm in enumerate(hits):
                verb = mm.group(1).upper()
                suffix = mm.group(2) or ""
                path = norm_path("/" + base_path.strip("/") + ("/" + suffix.strip("/") if suffix else ""))
                end = hits[i + 1].start() if i + 1 < len(hits) else len(text)
                block = text[mm.start():end]
                returns = {RET_TO_CODE[t] for t in RET_TO_CODE if ("return " + t) in block}
                attr = {int(c) for c in re.findall(r'ProducesResponseType\((?:typeof\([^)]*\),\s*)?(\d{3})', block)}
                attr |= {int(c) for c in re.findall(
                    r'ProducesResponseType\([^)]*StatusCodes\.Status(\d{3})', block)}
                out[(verb, path)] = {"returns": returns, "attr": attr,
                                     "file": os.path.relpath(f, root)}
    return out


def check_codes_vs_code(ops, code_map):
    """(пропущено в каноне, лишнее в каноне) — списки (operationId, код, источник)."""
    missing, extra = [], []
    for o in ops:
        key = (o.get("method", "").upper(), norm_path(o.get("path", "")))
        info = code_map.get(key)
        if not info:
            continue
        canon = {int(c) for c in re.findall(r'\b([1-5]\d{2})\b', str(o.get("codes") or ""))}
        oid = o.get("operationId") or (key[0] + " " + key[1])
        for c in sorted(info["returns"] - canon):
            missing.append((oid, c, "явный возврат действия"))
        for c in sorted(info["attr"] - canon):
            missing.append((oid, c, "[ProducesResponseType]"))
        # Направление «канон обещает код, которого нет в коде» НЕ реализуем: наивный разбор
        # дал 67 ложных на 82 операциях (границы тела действия по атрибутам `[Http*]` не
        # совпадают с реальными границами, `Ok(...)` уходит в соседний метод). Ложный гейт
        # хуже отсутствующего, поэтому здесь только достоверное: пропущенный код.
    return missing, extra




# 2026-11-10 (@AR 16:14, `1153`): копипаста в `verification` — это утверждение о БЕЗОПАСНОСТИ,
# скопированное на операции с другим `auth`. @AR нашёл руками: у 8 операций `Client.*` стояла
# одна строка «[AllowAnonymous] на :25/:28/:38», хотя анонимны только 3, а 5 — `[Authorize]`.
# Машинная проверка: сгруппировать операции по точному тексту `verification`; если в группе
# больше одного разного `auth` — это копипаста, а не проверка.
def check_verification_copypaste(ops):
    groups = {}
    for o in ops:
        text = (o.get("verification") or "").strip()
        if not text:
            continue
        groups.setdefault(text, []).append(o)
    bad = []
    for text, items in groups.items():
        auths = {(o.get("auth") or "").strip() for o in items}
        if len(auths) > 1 and len(items) > 1:
            ids = ", ".join((o.get("operationId") or "?") for o in items[:4])
            bad.append((ids, len(items), sorted(a for a in auths if a)))
    return bad


def selftest():
    ok = 0
    # @AR `1153` §5.4: копипаста `verification` ловится, а одинаковый auth в группе — нет.
    _cp = check_verification_copypaste([
        {"operationId": "Client.Me", "auth": "jwt", "verification": "одинаковый текст"},
        {"operationId": "Client.Get", "auth": "anonymous", "verification": "одинаковый текст"},
        {"operationId": "Journal.Get", "auth": "jwt", "verification": "свой текст"},
        {"operationId": "Journal.List", "auth": "jwt", "verification": "свой текст"},
    ])
    total_cp = 1
    if len(_cp) == 1 and _cp[0][1] == 2:
        ok += 1
    else:
        print("  [FAIL] копипаста verification: %s" % (_cp,))
    # @AR `13:46` §5.4: правило ownership проверяется на ДЕФЕКТЕ и на норме.
    _ops = [
        {"operationId": "Catalog.Get", "method": "GET",
         "path": "/api/v1/catalog/items/{id}", "codes": "{200: 'ok', 404: 'not-found'}"},
        {"operationId": "Catalog.Get", "method": "GET",
         "path": "/api/v1/catalog/items/{id}", "codes": "{200: 'ok', 403: 'not-owner', 404: 'nf'}"},
        {"operationId": "Client.Get", "method": "GET",
         "path": "/api/v1/clients/{id}", "codes": "{200: 'ok', 403: 'чужая запись'}"},
        {"operationId": "DailyMetrics.InternalSeries", "method": "GET",
         "path": "/internal/daily-metrics/series", "codes": "{200: 'ok', 403: 'route'}"},
    ]
    _half = check_ownership_codes(_ops[:2])
    _ok_cases = (len(_half) == 1 and _half[0][0] == "Catalog.Get"
                 and check_ownership_codes(_ops[2:]) == [])
    if _ok_cases:
        ok += 1
    else:
        print("  [FAIL] ownership-403: дефект=%s, allowlist/internal=%s"
              % (_half, check_ownership_codes(_ops[2:])))
    cases = [
        ('{"path":"/api/v1/x/{id}"}', norm_path("/api/v1/x/{id:guid}"), "/api/v1/x/{id}"),
    ]
    for _, got, expect in cases:
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] norm_path: %s != %s" % (got, expect))
    if norm_path("/api/v1/journal/records/") == "/api/v1/journal/records":
        ok += 1
    else:
        print("  [FAIL] слэш в конце")
    if ("POST", "/api/v1/clients") in ANON_ALLOW and ("GET", "/api/v1/clients") not in ANON_ALLOW:
        ok += 1
    else:
        print("  [FAIL] allow-list")
    # кейс: ссылка `BC.Method` в 03-ddd на несуществующий operationId -> ловится
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-apiidx")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    ddd_base = os.path.join(base, "03-ddd", "BC-Test")
    os.makedirs(ddd_base, exist_ok=True)
    with open(os.path.join(ddd_base, "02-aggregate-canvas.md"), "w", encoding="utf-8") as f:
        f.write("| `Journal.List` | ok |\n| `Journal.NoSuchOp` | должно покраснеть |\n")
    got = check_ddd_refs(base, {"Journal.List", "Journal.Get"})
    if len(got) == 1 and got[0][2] == "Journal.NoSuchOp":
        ok += 1
    else:
        print("  [FAIL] ddd-ссылка не поймана: %s" % (got,))

    shutil.rmtree(base, ignore_errors=True)

    # AR-022: «POST без request → нарушение» и «POST с required: [] → норма»
    post_no_req = [{"method": "POST", "path": "/api/v1/x"}]
    post_empty = [{"method": "POST", "path": "/api/v1/x", "request": {"required": []}}]
    get_no_req = [{"method": "GET", "path": "/api/v1/x"}]
    if check_post_request(post_no_req) == ["/api/v1/x"]:
        ok += 1
    else:
        print("  [FAIL] POST без request не пойман")
    if check_post_request(post_empty) == [] and check_post_request(get_no_req) == []:
        ok += 1
    else:
        print("  [FAIL] request: {required: []} или GET дали ложное нарушение")

    # кейс: длинный набор атрибутов — окно 300 символов не доходит до `public `,
    # анонимность всё равно обязана определиться (иначе `POST /api/v1/clients` даёт ложный red)
    many = "[HttpPost]\n[AllowAnonymous]\n" + "[ProducesResponseType(200)]\n" * 12 + \
           "public Task<IActionResult> X() => Ok();"
    if "[AllowAnonymous]" in many[len("[HttpPost]"):].split("public ")[0]:
        ok += 1
    else:
        print("  [FAIL] [AllowAnonymous] не найден при длинном наборе атрибутов")

    # кейс: `Partners.Storage` — имя проекта, а не операция (@TL 09:37)
    if "Storage" in NAMESPACE_TAILS and "List" not in NAMESPACE_TAILS:
        ok += 1
    else:
        print("  [FAIL] пространство имён не исключено из ddd-ссылок")

    total = 10
    print("check_api_index --selftest: %d/%d" % (ok, total))
    # 2026-11-05: было `ok == 3` при четырёх кейсах — текст печатал 4/4, а rc был 1.
    # Класс «верно считает, врёт кодом возврата»; теперь сравнение с total.
    return 0 if ok == total else 1


READ_PATHS = "04-architecture/read-paths.md"

# Пространства имён проектов (`BC-*.Storage`, `*.Api`, `*.Domain`, `*.Application`,
# `*.InternalContracts`) — не operationId, ссылками на API не являются (@TL 09:37).
NAMESPACE_TAILS = {
    "Storage", "Api", "Domain", "Application", "InternalContracts",
    "Infrastructure", "Persistence", "Migrations", "Tests",
}


def operation_ids(index_path):
    """operationId из индекса (артефакт @AR)."""
    ids = set()
    try:
        txt = open(index_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return ids
    for m in re.finditer(r'operationId:\s*([A-Za-z0-9_.]+)', txt):
        if m.group(1).isdigit():
            continue
        ids.add(m.group(1))
    return ids


def db_only_rows(root):
    """Строки `read-paths.md`, где источник — только `DB.*` (кеш), без `Api.*`.

    Находка @AR 2026-11-04 10:45: гейт искал только `Api.*`, поэтому строка
    «партнёры — локальный кэш `DB.getAllPartners()`» была для него невидима, и дыра
    в контракте проходила молча. Теперь такая строка **называется** явно: либо в ней
    появится `Api.*`, либо это осознанно кэш — но тогда это видно в выводе гейта.
    """
    path = os.path.join(root, READ_PATHS)
    if not os.path.exists(path):
        return []
    bad = []
    for i, line in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
        if "DB.get" not in line or "Api." in line:
            continue
        if line.lstrip().startswith(">"):        # пояснительный блокквот — не строка реестра
            continue
        bad.append((i, line.strip()[:90]))
    return bad


def check_read_paths(root, ids):
    """§2 `read-paths.md` ссылается на операции, которые обязаны быть в индексе.

    Идея @AR 2026-11-15 05:45 (§5, п.3): «список MV на экране = контракт» проверяется
    машинно — все вызовы `Api.<group>.<method>()` из реестра экранов должны иметь
    operationId в `api-index.yaml`. Иначе экран читает то, чего нет в контракте.
    """
    path = os.path.join(root, READ_PATHS)
    if not os.path.exists(path):
        return []
    txt = open(path, encoding="utf-8", errors="replace").read()
    missing = []
    for grp, meth in sorted(set(re.findall(r'Api\.([a-z]+)\.([a-z][A-Za-z]*)\(', txt))):
        base = grp.capitalize()
        variants = {base, base[:-1] if base.endswith("s") else base + "s"}
        cands = {"%s.%s" % (v, meth[0].upper() + meth[1:]) for v in variants}
        if not (cands & ids):
            missing.append((("Api.%s.%s" % (grp, meth)), sorted(cands)))
    return missing



DDD_DIR = "03-ddd"
REF_TICK = re.compile(r'`([A-Za-z][A-Za-z0-9]*\.[A-Za-z][A-Za-z0-9]*(?:/[A-Za-z][A-Za-z0-9]*)*)`')


def check_ddd_refs(root, ids):
    """Ссылки `BC.Method` из канвасов `03-ddd/**` должны существовать в индексе.

    Запрос @AR 2026-11-04 11:05: раскатка канвасов дала 100+ ссылок на `operationId`,
    но гейт проверял только `Api.*` из `read-paths.md` — то есть ссылки в канвасах
    не были проверены ничем. Норма — «неразрешённых 0», но без гейта это утверждение
    живёт до первой правки.

    Правила, чтобы не ловить идентификаторы кода:
      * берём только **в бэктиках** (`Journal.List`), а не любое `X.Y` в тексте;
      * группа (`Journal`) должна существовать в индексе — `Root.ClientId`, `Aggregate.Remove`
        и подобные идентификаторы кода не считаются ссылками на операцию;
      * сокращение `Journal.List/Get/Recent` раскрывается в три ссылки.
    """
    base = os.path.join(root, DDD_DIR)
    if not os.path.isdir(base):
        return []
    groups = {i.split(".")[0] for i in ids}
    bad = []
    for dirpath, _dirs, files in os.walk(base):
        for name in sorted(files):
            if not name.endswith(".md"):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root)
            for lineno, line in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
                for m in REF_TICK.finditer(line):
                    raw = m.group(1)
                    parts = raw.split(".")
                    if len(parts) != 2:
                        continue
                    grp, tail = parts
                    if grp not in groups:
                        continue
                    if tail.split("/")[0] in NAMESPACE_TAILS:
                        continue
                    for meth in tail.split("/"):
                        tok = "%s.%s" % (grp, meth)
                        if tok not in ids:
                            bad.append((rel, lineno, tok))
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--index", default=None)
    ap.add_argument("--selftest", action="store_true")
    # AR-022: по умолчанию — отчёт (`::warning::`), с флагом — блокирующая проверка.
    # Флаг снимается, когда @AR заполнит `request:` у всех POST в индексе (сейчас 54 без него).
    ap.add_argument("--require-response-types", action="store_true",
                      help="расхождение канон<->код = ошибка (по умолчанию отчёт)")
    ap.add_argument("--require-request", action="store_true",
                    help="POST без блока request = ошибка (AR-022); пока 54 нарушения — отчёт")
    a = ap.parse_args()

    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    index_path = a.index or os.path.join(root, INDEX)
    if not os.path.exists(index_path):
        print("::error::нет %s — гейт не может быть зелёным без индекса" % INDEX)
        return 1

    ops = parse_index(index_path)
    rp_missing = check_read_paths(root, operation_ids(index_path))
    rp_db_only = db_only_rows(root)
    ddd_bad = check_ddd_refs(root, operation_ids(index_path))
    idx = {(o.get("method", "").upper(), norm_path(o.get("path", ""))) for o in ops if o.get("method")}
    code = parse_code(root)
    code_set = {(v, p) for v, p, _, _ in code}
    bad = 0

    missing_in_index = sorted(code_set - idx)
    extra_in_index = sorted(idx - code_set)
    for v, p in missing_in_index[:20]:
        print("::error::в коде есть, в индексе нет: %s %s" % (v, p))
        bad += 1
    for v, p in extra_in_index[:20]:
        print("::error::в индексе есть, в коде нет: %s %s" % (v, p))
        bad += 1

    post_no_req = check_post_request(ops)
    if post_no_req:
        # @AR 2026-11-07 08:49: норма — блокирующая (индекс заполнен: 54/54 POST имеют `request`).
        # Флаг `--require-request` оставлен как синоним, чтобы старые вызовы не падали.
        for p in post_no_req:
            print("::error::(AR-022) POST без блока request: %s" % p)
        bad += 1

    own_bad = check_ownership_codes(ops)
    for oid, method, path in own_bad[:10]:
        print("::error::(codes_rule) %s %s: `403` у ресурсной операции — ownership не кодируется "
              "статусом, «не твой» ≡ «не существует» = `404` (@AR 13:46)" % (method, path))
    bad += 1 if own_bad else 0

    # @AR `13:49`: сверка канон <-> код. По умолчанию отчёт (`::warning::`),
    # блокирующая — флагом `--require-response-types` (как `--require-request`: сначала данные).
    _cmap = code_codes_by_route(root)
    _miss, _extra = check_codes_vs_code(ops, _cmap)
    if _miss or _extra:
        _lvl = "::error::" if a.require_response_types else "::warning::"
        # ВАЖНО: имя `code_num`, а не `code` — `code` выше это список маршрутов из parse_code,
        # и цикл `for oid, code, why` перекрывал его (`TypeError: 'int' object is not iterable`
        # на `anon_code`). Класс «локальная переменная цикла ломает соседнюю проверку».
        for oid, code_num, why in _miss[:12]:
            print("%s(codes) %s: канон не содержит %d, а код его отдаёт (%s)"
                  % (_lvl, oid, code_num, why))
        for oid, code_num, why in _extra[:12]:
            print("%s(codes) %s: канон обещает %d, но в коде его нет (%s)"
                  % (_lvl, oid, code_num, why))
        print("::notice::(codes) сверка канон<->код: пропущено %d, лишних %d | режим: %s"
              % (len(_miss), len(_extra),
                 "блокирующий" if a.require_response_types else "отчёт"))
        if a.require_response_types:
            bad += 1

    # @AR `1153`: копипаста `verification` у операций с разным `auth` — ошибка, а не стиль.
    copypaste = check_verification_copypaste(ops)
    for ids, n, auths in copypaste[:5]:
        print("::error::(verification) одна строка у %d операций с разным auth (%s): %s"
              % (n, ", ".join(auths), ids))
    bad += 1 if copypaste else 0

    no_codes = [o for o in ops if not (o.get("codes") or "").strip()]
    for o in no_codes[:10]:
        print("::error::%s %s — не заполнен codes" % (o.get("method"), o.get("path")))
        bad += len(no_codes) and 1

    for lineno, text in rp_db_only:
        print("::warning::(read-path) %s:%d — источник только `DB.*` (кеш), контракт не проверяется: %s"
              % (READ_PATHS, lineno, text))
    for rel, lineno, tok in ddd_bad[:20]:
        print("::error::(ddd) %s:%d — ссылка `%s` не найдена в api-index.yaml"
              % (rel, lineno, tok))
    for call, cands in rp_missing:
        print("::error::(read-path) %s: %s — нет в индексе (ждали один из %s)"
              % (READ_PATHS, call, ", ".join(cands)))
        bad += 1

    bad += len(ddd_bad)

    anon_idx = {(o.get("method", "").upper(), norm_path(o.get("path", "")))
                for o in ops if (o.get("auth") or "").strip() == "anonymous"}
    anon_code = {(v, p) for v, p, anon, _ in code if anon}
    if anon_idx != ANON_ALLOW:
        print("::error::анонимность в индексе != allow-list: индекс=%s" % sorted(anon_idx))
        bad += 1
    # @AR 16:14 п.4: `internal: true` — маршрут не публикуется снаружи и НЕ входит
    # в allow-list анонимности; гейт не должен требовать для него токен.
    internal = {(o.get("method", "").upper(), norm_path(o.get("path", "")))
                for o in ops if (o.get("internal") or "").strip().lower() == "true"}
    overlap = internal & ANON_ALLOW
    if overlap:
        print("::error::internal-маршрут в allow-list анонимности: %s" % sorted(overlap))
        bad += 1

    if anon_code != ANON_ALLOW:
        print("::error::анонимность в КОДЕ != allow-list: код=%s" % sorted(anon_code))
        bad += 1

    ok_bc, broken_bc, extended_bc = auth_scope(root)
    for name in broken_bc:
        print("::error::%s: нет FallbackPolicy=RequireAuthenticatedUser (ADR-026)" % name)
        bad += 1
    for name in extended_bc:
        print("::notice::%s: добавлен auth-стек — обнови область в ADR-026 "
              "(гейт ждёт 6 BC, станет %d)" % (name, len(ok_bc) + len(extended_bc)))
    print("check_api_index: read-path ссылок без операции: %d; строк только с DB.* "
          "(не проверяются): %d; ссылок в 03-ddd без операции: %d"
          % (len(rp_missing), len(rp_db_only), len(ddd_bad)))
    print("check_api_index: индекс %d операций, код %d маршрутов, anonymous %d, internal %d,"
          " расхождений %d" % (len(idx), len(code_set), len(anon_idx), len(internal), bad))
    print("check_api_index: auth-стек %d/%d с fallback; без auth по ADR-008: %d"
          % (len(ok_bc), len(AUTH_BCS), 11 - len(ok_bc) - len(broken_bc)))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
