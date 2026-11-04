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
                anon = "[AllowAnonymous]" in tail.split("public ")[0] if "public " in tail else False
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


def selftest():
    ok = 0
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
    print("check_api_index --selftest: %d/3" % ok)
    return 0 if ok == 3 else 1


READ_PATHS = "04-architecture/read-paths.md"


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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--index", default=None)
    ap.add_argument("--selftest", action="store_true")
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

    no_codes = [o for o in ops if not (o.get("codes") or "").strip()]
    for o in no_codes[:10]:
        print("::error::%s %s — не заполнен codes" % (o.get("method"), o.get("path")))
        bad += len(no_codes) and 1

    for lineno, text in rp_db_only:
        print("::warning::(read-path) %s:%d — источник только `DB.*` (кеш), контракт не проверяется: %s"
              % (READ_PATHS, lineno, text))
    for call, cands in rp_missing:
        print("::error::(read-path) %s: %s — нет в индексе (ждали один из %s)"
              % (READ_PATHS, call, ", ".join(cands)))
        bad += 1

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
    print("check_api_index: read-path ссылок без операции: %d; строк только с DB.* (не проверяются): %d"
          % (len(rp_missing), len(rp_db_only)))
    print("check_api_index: индекс %d операций, код %d маршрутов, anonymous %d, internal %d,"
          " расхождений %d" % (len(idx), len(code_set), len(anon_idx), len(internal), bad))
    print("check_api_index: auth-стек %d/%d с fallback; без auth по ADR-008: %d"
          % (len(ok_bc), len(AUTH_BCS), 11 - len(ok_bc) - len(broken_bc)))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
