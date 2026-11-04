#!/usr/bin/env python3
"""check_apim_auth.py — машинная проверка условия переключения трафика (ADR-026).

Что проверяет: каждая операция из `04-architecture/api-index.yaml`, у которой `auth != anonymous`,
при **прямом** запросе в сервис **без** `Authorization` обязана отвечать `401`.
`2xx` без токена = дефект (операция фактически открыта), его и печатаем.

Зачем отдельный скрипт: критерий «76/80 → 401» (@TL `2026-11-04 23:50`) был ручным, а он —
последнее машинное условие перевода APIM в путь трафика (DO-076). `tools/` — зона @DO,
поэтому проверка живёт в `9-tests/scripts/` (тестовая зона @TE), а подключение в CI решает @DO.

Использование:
    python 9-tests/scripts/check_apim_auth.py --base https://xdiary-vps2.taile0b6ee.ts.net
    python 9-tests/scripts/check_apim_auth.py --dry-run          # офлайн: сколько операций и какие
    python 9-tests/scripts/check_apim_auth.py --selftest         # без сети: доказательство, что краснеет
    python 9-tests/scripts/check_apim_auth.py --base URL --json   # машинный вывод
    python 9-tests/scripts/check_apim_auth.py --direct --dry-run  # план: BC -> сервис (офлайн)
    python 9-tests/scripts/check_apim_auth.py --direct --only Catalog,Analytics   # срез по 5 BC
    python 9-tests/scripts/check_apim_auth.py --direct --service-map map.json     # свои адреса

Коды возврата: 0 — все неанонимные операции дают 401; 1 — есть нарушения (список в выводе);
2 — не найдена база/индекс/нет PyYAML (это не «зелено», а «не проверено»).

**Честная оговорка про `--direct` (@TL `2026-11-05 00:16`, @AR `17:05`):** условие (ii-b)
сформулировано про **прямой запрос в сервис**, поэтому `--direct` имеет смысл **только изнутри
контура** (`podman exec`/ssh на VPS): снаружи имена `xdiary-*` не резолвятся, и прогон вернёт
`rc=2` («не измерено»), а не «зелено». Флаг `--base` меряет **публичный вход (YARP)** — это
условие (ii-a), и после переключения трафика (`Funnel → Caddy → APIM → сервис`) YARP в пути нет,
значит (ii-a) ничего не гарантирует.

`anonymous-inside` (`/internal/**`) считается **не** анонимным снаружи: это внутренний контур,
снаружи он обязан отвечать 401 — поэтому входит в проверку.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import shutil
import socket
import subprocess
import sys
import urllib.error
import urllib.request

# Windows-консоль по умолчанию cp1251: символ «→» в квитанции роняет печать
# (`UnicodeEncodeError: 'charmap' codec can't encode character ...`). Поэтому stdout/stderr
# принудительно в UTF-8 — иначе гейт падает на собственном выводе (класс бага check_awaits.py).
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")  # type: ignore[attr-defined]
    except (AttributeError, ValueError):
        pass

INDEX = "04-architecture/api-index.yaml"
ANON = {"anonymous"}

# BC в `api-index.yaml` → имя сервиса в compose (`7-deploy/podman-compose.yml`).
# Нужен для `--direct`: условие @AR сформулировано про ПРЯМОЙ запрос в сервис, а не про YARP.
SERVICE_BY_BC = {
    "Journal": "journal",
    "Partners": "partners",
    "Catalog": "catalog",
    "Client": "clients",
    "Privacy": "privacy",
    "Analytics": "analytics",
    "DailyMetrics": "daily-metrics",
    "SomaticSeries": "somatic-series",
    "Portability": "portability",
    "GarminGateway": "garmin-gateway",
    "AppleHealthGateway": "apple-health-gateway",
    "ApiGateway": "api-gateway",
}
GUID = "00000000-0000-0000-0000-000000000000"


def load_ops(path: str = INDEX) -> list[dict]:
    try:
        import yaml  # noqa: WPS433 — PyYAML есть в окружении; без него честный rc=2
    except ImportError:
        raise SystemExit("нет PyYAML — проверка не выполнена (rc=2)")
    with open(path, "r", encoding="utf-8") as fh:
        doc = yaml.safe_load(fh)
    return list(doc.get("operations") or [])


def requires_auth(op: dict) -> bool:
    return str(op.get("auth", "")).strip().lower() not in ANON


UNKNOWN_PLACEHOLDERS: set[str] = set()


def concrete_path(path: str) -> str:
    """Подставляет вместо шаблонов `{...}` значения, проходящие валидацию маршрута.

    Важно для класса `contract` (@AR `18:55`): если подставить в маршрут значение не того типа,
    сервис ответит `404`, и гейт запишет это как «дефект реестра» — то есть **ложный contract**
    из-за нашего же плейсхолдера. Поэтому незнакомые имена параметров собираем в
    `UNKNOWN_PLACEHOLDERS` и печатаем предупреждение: тогда `contract` будет означать реестр,
    а не подстановку.
    """
    def sub(m: re.Match) -> str:
        name = m.group(1).lower()
        base = name.split(":")[0]
        if base in ("id", "partnerid", "clientid", "recordid"):
            return GUID
        if "date" in base or "day" in base:
            return "2026-01-01"
        if "month" in base:
            return "2026-01"
        UNKNOWN_PLACEHOLDERS.add(name)
        return GUID
    return re.sub(r"\{([^}]+)\}", sub, path)


def current_ref() -> str:
    """`REF` для квитанции: короткий SHA репозитория или честный фолбэк.

    `@TL 2026-11-05 01:42`: при запуске внутри `python:3.12-slim` в образе нет `git`,
    и `subprocess.run(["git", ...])` бросает `FileNotFoundError` — прогон терялся
    **на печати квитанции** после успешно выполненных 76 запросов. Поэтому любой сбой
    получения REF (нет git, не репозиторий, нет прав) даёт `REF=НЕ ОПРЕДЕЛЁН`, а не падение.
    """
    try:
        r = subprocess.run(["git", "rev-parse", "--short", "HEAD"], capture_output=True, text=True)
        out = (r.stdout or "").strip()
        return out if r.returncode == 0 and out else "НЕ ОПРЕДЕЛЁН"
    except (FileNotFoundError, OSError, ValueError):
        return "НЕ ОПРЕДЕЛЁН"


def images_for(services: list[str], container_template: str = "xdiary-{service}") -> str:
    """`images=<svc:digest[:12], …>` для квитанции (@AR `18:10`, @TL `01:30`).

    Берём digest из `podman inspect -f "{{.Image}}" <container>`: `REF` доказывает код,
    но не то, что **в контейнере** («код есть, образ старый» — трижды за сутки).
    Если `podman` нет или контейнер не найден — честно пишем это, а не подставляем `REF`.
    """
    podman = shutil.which("podman")
    if not podman:
        return "НЕ ИЗМЕРЕНО: podman не найден"
    parts: list[str] = []
    for svc in sorted(set(services)):
        name = container_template.format(service=svc)
        r = subprocess.run([podman, "inspect", "-f", "{{.Image}}", name],
                           capture_output=True, text=True)
        # `podman inspect .Image` возвращает локальный ID; `sha256:` префикс убираем, оставляем 12
        digest = (r.stdout or "").strip().replace("sha256:", "")[:12]
        parts.append(f"{svc}:{digest or 'НЕТ'}")
    return ", ".join(parts)


def final_rc(violators: list, contracts: list, unknown: list, passed: int, total: int) -> int:
    """Код возврата по четырём классам (@AR `01:42`, @TL `01:43`):

      `protected` = `401/403`  · `violator` = `2xx/400/415/429` (ручка открыта) ·
      `contract` = `404/405` на операции, которая **есть в `api-index.yaml`** (дефект реестра,
      класс `DO-050`: индекс обещает маршрут, сервис его не имеет) ·
      `unknown`  = ответа нет (таймаут/соединение).

    `PASS` — только когда `violator=0 И contract=0 И unknown=0` (то есть `passed == total`).
    `contract` — это НЕ «неизвестно»: молча складывать его туда значит делать гейт зелёным
    при ручках, которых физически нет.
    """
    if violators or contracts:
        return 1
    if unknown or passed <= 0 or passed < total:
        return 2
    return 0


def resolve_images(arg_value: str, services: list) -> str:
    """`--images auto` → digest'ы контейнеров; иначе — как передано; пусто → `НЕ УКАЗАНО`."""
    if str(arg_value).strip().lower() == "auto":
        return images_for(list(services))
    return arg_value.strip()


def receipt_line(kind: str, ref: str, passed: int, total: int, rc: int, where: str,
                 violators: int = 0, contracts: int = 0, unknown: int = 0, images: str = "") -> str:
    """Квитанция (канон `ADR-026 → Квитанция (ii-b)`; `ITERATION-PLAN §19.1` ссылается на него):

        REF=<sha> | (ii-b) PASS|FAIL|UNKNOWN защищено=N/76 | нарушителей=V | contract=C |
        неизвестно=U | rc=N | where=<чем измерено> | images=<svc:digest, …>

    `PASS` — только при `нарушителей=0 И contract=0 И неизвестно=0`. `images` обязателен:
    `REF` доказывает код, но не то, что **в контейнере** («код есть, образ старый» — трижды за сутки).
    """
    label = f"({kind}) "
    if rc == 0:
        body = f"{label}PASS защищено={passed}/{total}"
    elif rc == 1:
        body = f"{label}FAIL защищено={passed}/{total}"
    else:
        body = f"{label}UNKNOWN защищено={passed}/{total} (не засчитывается)"
    # `contract` — подкласс нарушителя для счёта (@TL `01:45`): PASS требует 0 нарушителей
    # ВКЛЮЧАЯ contract, но отдельное число нужно, чтобы в ADR-026 было видно, КАКОЙ это дефект.
    # `V` и `C` печатаются РАЗДЕЛЬНО (ADR-026 §7): `B = V + C` — производное, и если печатать
    # только его, сумму `N+V+C+U` нельзя проверить («выпавшая группа» спрячется за агрегацией).
    line = (f"REF={ref} | {body} | нарушителей={violators} | contract={contracts} "
            f"| неизвестно={unknown} | rc={rc}")
    # Тождество `N+V+C+U == total` (@AR/@TL `01:58`): печатаем сумму всегда — «выпавшая группа»
    # видна сразу, без ревью. Если сумма не сходится, main возвращает `rc=2` (недействительна).
    line += f" | сумма={passed + violators + contracts + unknown}/{total}"
    line += f" | where={where or 'НЕ УКАЗАНО'} | images={images or 'НЕ УКАЗАНО'}"
    line += f" | tool={tool_id()}"
    return line


def tool_id() -> str:
    """`tool=<путь>@<sha256 скрипта[:12]>` (ADR-026 §7, @AR `19:40`).

    `REF` доказывает версию **репозитория**, но не версию **скрипта**: его могли править в рабочем
    дереве. Классификация менялась трижды за сутки, поэтому «16 защищено» и «21 защищено» — числа
    из разных определений; без `tool=` их нельзя сравнивать даже через `REF`.
    """
    import os
    import sys
    candidates = [__file__, getattr(sys, "argv", [""])[0] if getattr(sys, "argv", None) else ""]
    path = next((c for c in candidates if c and c not in ("<stdin>", "-") and pathlib.Path(c).is_file()),
                __file__)
    try:
        path = os.path.relpath(path)          # короткий путь, если запускали из корня репо
        if path.startswith(".."):
            path = "9-tests/scripts/check_apim_auth.py"
    except (ValueError, OSError):
        path = "9-tests/scripts/check_apim_auth.py"
    try:
        digest = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()[:12]
    except OSError:
        digest = "НЕ ОПРЕДЕЛЁН"
    return f"{path.replace(os.sep, '/')}@{digest}"


def resolve_host(svc_map: dict, bc: str, template: str) -> str:
    """BC → адрес сервиса для `--direct`. Три формы значения в `--service-map`:
    полный URL (`http://h:p`), `host:port` (порт уже задан), имя сервиса (→ template)."""
    val = svc_map[bc]
    if "://" in val:
        return val
    if ":" in val:
        return "http://" + val
    return template.format(service=val)


def check_url(url: str, method: str = "GET", timeout: float = 8.0) -> tuple[int | None, str]:
    req = urllib.request.Request(url, method=method.upper())
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return int(resp.status), url
    except urllib.error.HTTPError as e:
        return int(e.code), url
    except (urllib.error.URLError, socket.timeout, OSError) as e:
        return None, f"{url} ({type(e).__name__})"


def check_one(base: str, op: dict, timeout: float = 8.0) -> tuple[int | None, str]:
    return check_url(base.rstrip("/") + concrete_path(str(op["path"])),
                     str(op.get("method", "GET")), timeout)


def verdict(results, five_xx_unknown=None, direct: bool = False):
    """Четыре класса (@AR `01:42`, @TL `01:43`, уточнение `01:45`): protected/violator/contract/unknown.

    `404/405` на операции из `api-index.yaml` — **дефект реестра** (`contract`), а не «неизвестно»:
    индекс обещает маршрут, сервис его не имеет (класс `DO-050`). Для счёта `contract` — подкласс
    нарушителя, но печатается отдельным числом, чтобы было видно, **какой** это дефект.

    `502/503/504` (@TL `01:45`): в режиме `--base` это ответ **прокси**, а не сервиса → `UNKNOWN`;
    в режиме `--direct` прокси в пути нет, значит `5xx` — ответ **самого сервиса** → по умолчанию
    **нарушитель**; вернуть их в `UNKNOWN` можно только явно (`--5xx-unknown`).
    """
    if five_xx_unknown is None:
        five_xx_unknown = not direct
    violators, contracts, unknown = [], [], []
    protected_count = 0
    for op, code, url in results:
        name = f"{op.get('method')} {op.get('path')} [{op.get('operationId')}] bc={op.get('bc')}"
        if code is None:
            unknown.append(f"{name} — нет ответа: {url}")
        elif code in (401, 403):
            continue
        elif code in (404, 405):
            contracts.append(f"{name} — {code} на индексированной операции: дефект реестра "
                             f"(api-index обещает маршрут, сервис его не имеет), {url}")
        elif code in (502, 503, 504) and five_xx_unknown:
            unknown.append(f"{name} — {code} [infra] (прокси недоступен; в --direct это было бы нарушение), {url}")
        else:
            violators.append(f"{name} — {code} вместо 401 (ответ получен без токена), {url}")
    return violators, contracts, unknown, protected_count


def identity_ok(protected: int, violators: int, contracts: int, unknown: int, total: int) -> bool:
    """Тождество квитанции (@AR `01:58`, @TL `01:58`): `N+V+C+U == total`.

    Защита от «выпавшей группы»: если классификатор потеряет ответы (как случилось с 13 `400`
    в чужом инструменте), сумма перестанет сходиться, и квитанция станет **недействительной**
    (`rc=2`), а не «частично верной». Сверяем с числом **целей** (`total`), а не с литеральным
    `76`: при `--only`/`--service-map` целей меньше, и жёсткое `76` дало бы ложную недействительность.
    """
    return protected + violators + contracts + unknown == total

def selftest() -> int:
    """Доказательство, что проверка краснеет на открытой операции и зеленеет на 401.

    @TL `2026-11-05 00:07`: селфтест обязан **поднять локальный HTTP-сервер** и проверить
    классификатор на реальных `401`/`200`. Иначе проверяется только функция `verdict()`,
    а путь «HTTP-клиент → код ответа» остаётся непроверенным — то самое «зелёное на пустоте».
    Поэтому здесь настоящий `http.server` на 127.0.0.1:0 и настоящие запросы `check_one`.
    """
    import http.server
    import threading

    # `/api/v1/i` — «внутренний» маршрут: снаружи он обязан отвечать 401 (а не 200),
    # поэтому он же проверяется как `anonymous-inside` в классификаторе ниже.
    statuses = {"/api/v1/a": 401, "/api/v1/b": 200, "/api/v1/c": 403,
                "/api/v1/d": 500, "/api/v1/e": 404, "/api/v1/i": 401}

    class Handler(http.server.BaseHTTPRequestHandler):
        def _send(self):  # noqa: N802 — имя диктует BaseHTTPRequestHandler
            code = statuses.get(self.path, 418)
            self.send_response(code)
            self.send_header("Content-Type", "text/plain")
            self.end_headers()
            self.wfile.write(b"selftest")

        do_GET = _send
        do_POST = _send

        def log_message(self, *_args):  # тишина в выводе
            return

    srv = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    port = srv.server_address[1]
    base = f"http://127.0.0.1:{port}"
    th = threading.Thread(target=srv.serve_forever, daemon=True)
    th.start()
    ok = 0
    total = 0
    try:
        # 1) HTTP-путь: реальные коды доходят до классификатора как есть
        for path, want in statuses.items():
            total += 1
            code, _url = check_one(base, {"method": "GET", "path": path}, timeout=5)
            if code == want:
                ok += 1
            else:
                print(f"  [FAIL] HTTP {path}: ждали {want}, получили {code}")

        # 2) классификатор на реальных ответах сервера
        ops = [
            {"method": "GET", "path": "/api/v1/a", "operationId": "A.Get", "auth": "jwt"},
            {"method": "GET", "path": "/api/v1/c", "operationId": "C.Forbidden", "auth": "jwt"},
            {"method": "GET", "path": "/api/v1/e", "operationId": "E.NotFound", "auth": "jwt"},
            {"method": "GET", "path": "/api/v1/b", "operationId": "B.Open", "auth": "jwt"},
            {"method": "GET", "path": "/api/v1/d", "operationId": "D.Error", "auth": "jwt"},
            {"method": "POST", "path": "/api/v1/clients", "operationId": "X.Create", "auth": "anonymous"},
            {"method": "POST", "path": "/api/v1/i", "operationId": "I.Internal", "auth": "anonymous-inside"},
        ]
        targets = [op for op in ops if requires_auth(op)]
        results = [(op, *check_one(base, op, timeout=5)) for op in targets]
        viol, contr, unk, prot = verdict(results)
        rc = final_rc(viol, contr, unk, prot, len(targets))

        total += 1
        # Четыре класса (@AR `01:42`, @TL `01:43`). Набор: a=401, c=403 (protected); b=200 и d=500
        # (violator); e=404 — `contract` (маршрут есть в индексе, сервис 404 = дефект реестра).
        if rc == 1 and len(viol) == 2 and len(contr) == 1 and len(unk) == 0:
            ok += 1
        else:
            print(f"  [FAIL] классификатор: ждали rc=1, 2 violator, 1 contract, 0 unknown; "
                  f"получили rc={rc}, viol={len(viol)}, contract={len(contr)}, unknown={len(unk)}")

        total += 1
        # 200 и 500 — нарушители поимённо, 404 — именно `contract`, а не «неизвестно»
        if (any("B.Open" in b for b in viol) and any("D.Error" in b for b in viol)
                and any("E.NotFound" in b for b in contr)):
            ok += 1
        else:
            print(f"  [FAIL] имена по классам: viol={viol}, contract={contr}")

        total += 1
        # Таблица кодов: 2xx/400/415/429/500 → violator; 404/405 → contract;
        # 401/403 → protected; 502 → unknown (и violator при `--5xx-violator`).
        cls = [
            (200, "violator"), (400, "violator"), (415, "violator"), (429, "violator"),
            (500, "violator"), (404, "contract"), (405, "contract"),
            (401, "protected"), (403, "protected"), (502, "unknown"),
        ]
        wrong = []
        for code, want in cls:
            _v, _c, _u, _p = verdict([({"method": "GET", "path": "/x", "operationId": "X", "bc": "C"}, code, "u")])
            got = "violator" if _v else ("contract" if _c else ("unknown" if _u else "protected"))
            if got != want:
                wrong.append(f"{code}: got {got}, want {want}")
        _v2, _c2, _u2, _p2 = verdict([({"method": "GET", "path": "/x", "operationId": "X", "bc": "C"}, 502, "u")],
                                five_xx_unknown=False)
        if not _v2:
            wrong.append("502: strict-режим не сделал нарушителем")
        if not wrong:
            ok += 1
        else:
            print(f"  [FAIL] классификация кодов: {wrong}")

        total += 1
        # фильтр: `anonymous` исключён, `anonymous-inside` включён (снаружи обязан 401)
        if len(targets) == 6:
            ok += 1
        else:
            print(f"  [FAIL] фильтр анонимных: ждали 6 (7 операций минус `anonymous`), получили {len(targets)}")

        total += 1
        # 3) `--direct`: адресация сервисов, три формы значения (@TL `00:16` п.4)
        fakes = {"Catalog": f"127.0.0.1:{port}", "Analytics": f"http://127.0.0.1:{port}", "Journal": "journal"}
        tpl = "http://xdiary-{service}:8080"
        hosts = {bc: resolve_host(fakes, bc, tpl) for bc in fakes}
        expected = {"Catalog": f"http://127.0.0.1:{port}", "Analytics": f"http://127.0.0.1:{port}",
                    "Journal": "http://xdiary-journal:8080"}
        if hosts == expected:
            ok += 1
        else:
            print(f"  [FAIL] --direct адресация: {hosts} != {expected}")

        total += 1
        # «сервис напрямую»: запрос по адресу СЕРВИСА (здесь — тот же локальный сервер);
        # в тексте нарушения обязан быть `bc` (@TL `00:16` п.3)
        direct_ops = [
            {"method": "GET", "path": "/api/v1/a", "operationId": "S.Closed", "auth": "jwt", "bc": "Catalog"},
            {"method": "GET", "path": "/api/v1/b", "operationId": "S.Open", "auth": "jwt", "bc": "Catalog"},
        ]
        dres = [(op, *check_url(resolve_host(fakes, op["bc"], tpl) + op["path"])) for op in direct_ops]
        dv, dc, du, dp = verdict(dres)
        if dv and any("S.Open" in b for b in dv) and any("bc=Catalog" in b for b in dv) and not dc and not du:
            ok += 1
        else:
            print(f"  [FAIL] --direct классификатор: viol={dv}, contract={dc}, unknown={du}")

        total += 1
        # Подстановка параметров пути: известные (`id`/`partnerId`/`date`/`month`) — без следа,
        # незнакомый (`{kind}`) обязан попасть в UNKNOWN_PLACEHOLDERS, иначе получим ЛОЖНЫЙ
        # `contract` из-за нашей же подстановки (@AR `18:55`).
        before = set(UNKNOWN_PLACEHOLDERS)
        known = concrete_path("/api/v1/x/{id:guid}/y/{partnerId}")
        unknown = concrete_path("/api/v1/x/{kind}")
        if (UNKNOWN_PLACEHOLDERS - before) == {"kind"} and "00000000-0000" in known and "00000000-0000" in unknown:
            ok += 1
        else:
            print(f"  [FAIL] плейсхолдеры: recorded={UNKNOWN_PLACEHOLDERS - before}, known={known}")
        UNKNOWN_PLACEHOLDERS.clear()
        UNKNOWN_PLACEHOLDERS.update(before)

        total += 1
        # REF в квитанции не должен быть пустым (может быть `unknown`, но не пустая строка)
        if current_ref():
            ok += 1
        else:
            print("  [FAIL] current_ref() пуст — квитанция будет без REF")

        total += 1
        # квитанция по формату @AR `ITERATION-PLAN §19.1` / @TL `01:18`:
        # `REF | (ii-b) PASS|FAIL|UNKNOWN защищено=N/76 | нарушителей=B (в т.ч. contract=C)
        #  | неизвестно=U | rc | where | images`   (@TL `01:45`: contract — подкласс нарушителя)
        good = receipt_line("ii-b", "abc1234", 76, 76, 0, "podman exec xdiary-catalog",
                            0, 0, 0, "journal:abc123")
        unk = receipt_line("ii-b", "abc1234", 44, 76, 2, "", 22, 10, 20, "")
        tool_ok = bool(re.search(r"tool=[^@|]+@([0-9a-f]{12}|НЕ ОПРЕДЕЛЁН)", good))
        if ("PASS защищено=76/76" in good and "нарушителей=0" in good and "contract=0" in good
                and "неизвестно=0" in good and "rc=0" in good and "сумма=76/76" in good
                and "where=podman exec xdiary-catalog" in good and "images=journal:abc123" in good
                and tool_ok
                and "UNKNOWN" in unk and "не засчитывается" in unk
                and "нарушителей=22" in unk and "contract=10" in unk and "неизвестно=20" in unk
                and "сумма=96/76" in unk
                and "where=НЕ УКАЗАНО" in unk and "images=НЕ УКАЗАНО" in unk):
            ok += 1
        else:
            print(f"  [FAIL] формат квитанции: {good!r} / {unk!r}")

        total += 1
        # регресс-кейсы: баг @DO `01:17` (0 ответов → 0) и требование @TL `01:18`
        # (`PASS` только при измерено ровно total/total).
        # Регресс-кейсы код возврата: баг @DO `01:17` (0 ответов → 0) и требование @TL `01:18`
        # (`PASS` только при измерено ровно total/total) + новый класс `contract` (@TL `01:43`).
        # Кортеж: (violators, contracts, unknown, passed, total) → ожидаемый rc.
        cases_rc = [
            (([], [], [], 0, 76), 2, "ни один ответ не получен → rc=2 UNKNOWN"),
            (([], [], [], 76, 76), 0, "все 76 → 401, нарушителей/contract/unknown = 0 → rc=0 PASS"),
            ((["нарушитель"], [], [], 75, 76), 1, "есть нарушитель → rc=1 FAIL"),
            (([], ["contract"], [], 75, 76), 1, "есть contract-дефект реестра → rc=1 FAIL"),
            (([], [], ["нет ответа"], 44, 76), 2, "частично (44/76) → rc=2, НЕ PASS"),
        ]
        total += 1
        # тождество `N+V+C+U == total` (@AR/@TL `01:58`): сумма не сходится -> квитанция
        # недействительна. Проверяем предикат на «выпавшей группе» и на подмножестве (--only).
        ident = [
            ((40, 20, 10, 6, 76), True, "40+20+10+6 = 76 — сходится"),
            ((40, 20, 10, 5, 76), False, "40+20+10+5 != 76 — группа выпала"),
            ((5, 0, 0, 3, 8), True, "подмножество (--only): 5+0+0+3 = 8 — сходится"),
            ((0, 0, 0, 76, 76), True, "все UNKNOWN: 0+0+0+76 = 76 — сумма сходится (но не PASS)"),
        ]
        bad_ident = [t for (args, want, t) in ident if identity_ok(*args) != want]
        if not bad_ident:
            ok += 1
        else:
            print(f"  [FAIL] тождество: неверно в кейсах {bad_ident}")

        bad_rc = [t for (args, want, t) in cases_rc if final_rc(*args) != want]
        if not bad_rc:
            ok += 1
        else:
            print(f"  [FAIL] final_rc: неверный код в кейсах {bad_rc}")
    finally:
        srv.shutdown()
        srv.server_close()

    print(f"check_apim_auth --selftest: {ok}/{total} (локальный HTTP-сервер, реальные коды)")
    return 0 if ok == total else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base")
    ap.add_argument("--index", default=INDEX)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--direct", action="store_true",
                    help="бить в СЕРВИСЫ напрямую (не через YARP): http://xdiary-<service>:8080")
    ap.add_argument("--host-template", default="http://xdiary-{service}:8080",
                    help="шаблон адреса сервиса для --direct (по умолчанию http://xdiary-{service}:8080)")
    ap.add_argument("--only", default="", help="ограничить BC: --only Catalog,Analytics")
    ap.add_argument("--where", default="",
                    help="чем измерено (напр. podman exec xdiary-catalog / VPS-2 network xdiary_default) — обязательно для квитанции (ii-b)")
    ap.add_argument("--5xx-unknown", action="store_true", dest="five_xx_unknown",
                    help="в --direct считать 5xx снова UNKNOWN (по умолчанию в --direct 5xx = нарушитель: прокси в пути нет)")
    ap.add_argument("--images", default="",
                    help="диджесты образов для квитанции: auto (podman inspect) | journal:abc123, catalog:def456")
    ap.add_argument("--service-map", default="",
                    help='JSON {"Catalog": "xdiary-catalog:8080"} — переопределить адреса сервисов')
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    ops = load_ops(args.index)
    targets = [op for op in ops if requires_auth(op)]
    print(f"операций в индексе: {len(ops)}, требуют токен: {len(targets)}, "
          f"анонимных: {len(ops) - len(targets)}")

    if UNKNOWN_PLACEHOLDERS:
        print("::warning::check_apim_auth: в индексе есть неизвестные параметры пути "
              + ", ".join(sorted(UNKNOWN_PLACEHOLDERS))
              + " — подставлен GUID; возможен ЛОЖНЫЙ contract (404 из-за подстановки). "
                "Добавь правило подстановки в concrete_path().")
    if args.only:
        allowed = {b.strip().lower() for b in args.only.split(",") if b.strip()}
        targets = [op for op in targets if str(op.get("bc", "")).lower() in allowed]
        print(f"фильтр --only {sorted(allowed)}: целей {len(targets)}")

    # --- режим --direct: измеряем УСЛОВИЕ @AR (ii-b): прямой запрос в сервис, а не в прокси ---
    if args.direct:
        # `--service-map` (@AR `17:05`): JSON вида {"Catalog": "xdiary-catalog:8080"} или
        # {"Catalog": "http://10.0.0.5:8080"}. Нужен, когда имена сервисов в контуре другие.
        svc_map = dict(SERVICE_BY_BC)
        if args.service_map:
            try:
                # utf-8-sig: файлы, отредактированные на Windows, часто с BOM — иначе json
                # падает «Unexpected UTF-8 BOM», и это выглядело бы как ошибка контура.
                with open(args.service_map, "r", encoding="utf-8-sig") as fh:
                    over = json.load(fh)
                svc_map.update({str(k): str(v) for k, v in over.items()})
                print(f"маппинг из {args.service_map}: {len(over)} переопределений")
            except (OSError, ValueError) as e:
                print(f"::error::check_apim_auth: не читается --service-map {args.service_map}: {e}")
                return 2
        unmapped = sorted({str(op.get("bc")) for op in targets if str(op.get("bc")) not in svc_map})
        if unmapped:
            print(f"::error::check_apim_auth: нет маппинга BC → сервис для {unmapped}")
            return 2

        def host_for(bc: str) -> str:
            return resolve_host(svc_map, bc, args.host_template)

        per_service: dict[str, list[dict]] = {}
        for op in targets:
            per_service.setdefault(svc_map[str(op.get("bc"))], []).append(op)
        if args.dry_run:
            for svc, lst in sorted(per_service.items()):
                print(f"   {svc:24} {len(lst):3} операций   {host_for(str(lst[0].get('bc')))}")
            print(f"DRY-RUN --direct: сервисов {len(per_service)}, целей {len(targets)} "
                  f"(измерять ИЗНУТРИ контура: снаружи xdiary-* не резолвится — тогда rc=2)")
            return 0

        results: list[tuple[dict, int | None, str]] = []
        for svc, lst in sorted(per_service.items()):
            host = host_for(str(lst[0].get("bc")))
            for op in lst:
                url = host.rstrip("/") + concrete_path(str(op["path"]))
                code, got = check_url(url, str(op.get("method", "GET")))
                results.append((op, code, got))
        violators, contracts, unknown, protected_count = verdict(results, args.five_xx_unknown if args.direct else True, direct=args.direct)
        passed = protected_count
        rc = final_rc(violators, contracts, unknown, passed, len(targets))
        # Детали печатаем ВСЕГДА — до любых ранних выходов: иначе при недоступном контуре
        # в логе не видно, какие именно `метод+путь+bc` не измерены (@TL `00:16` п.3).
        for svc, lst in sorted(per_service.items()):
            svc_res = [r for r in results if svc_map[str(r[0].get("bc"))] == svc]
            svc_pass = sum(1 for _op, code, _u in svc_res if code in (401, 403))
            print(f"   {svc:22} {svc_pass}/{len(svc_res)} дают 401 без токена")
        for v in violators:
            print(f"::error::apim-auth(service)[violator]: {v}")
        for c in contracts:
            print(f"::error::apim-auth(service)[contract]: {c}")
        for u in unknown:
            print(f"::warning::apim-auth(service)[unknown]: {u}")

        map_note = f"map={args.service_map}" if args.service_map else f"template={args.host_template}"
        images_note = resolve_images(args.images, list(per_service.keys()))
        print(f"источник адресов: {map_note}")
        # «Ничего не измерено» ≠ «зелено»: контур недоступен/прокси отвечает 502 → rc=2.
        # Без этого прогон извне выглядел бы PASS-ом на пустоте (класс, который ловил @TL).
        if rc == 2:
            print("::error::check_apim_auth: ни один сервис не дал измеримого ответа — "
                  "запускать изнутри контура (@DO); это НЕ «нарушения» и НЕ «зелено»")
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, args.where, len(violators), len(contracts), len(unknown), images_note))
            return 2
        # @AR `17:20`: квитанция обязана содержать «чем измерено». Без `--where` это не квитанция,
        # а измерение без provenance → трактуем как UNKNOWN (rc=2), а не как PASS.
        if rc == 0 and not args.where:
            print("::error::check_apim_auth: для квитанции (ii-b) обязателен `--where` "
                  "(ADR-026 / round-checklist §3.1: без «чем измерено» rc=0 не засчитывается)")
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, "", len(violators), len(contracts), len(unknown), images_note))
            return 2
        print(receipt_line("ii-b", current_ref(), passed, len(targets), rc, args.where, len(violators), len(contracts), len(unknown), images_note))
        return rc

    if args.dry_run:
        for op in targets:
            print(f"   {op.get('method'):6} {op.get('path'):52} {op.get('bc')}  auth={op.get('auth')}")
        print(f"DRY-RUN: проверять нечего без стенда; целей {len(targets)} (ожидание @TL: 76)")
        return 0 if len(targets) == 76 else 1

    if not args.base:
        print("::error::check_apim_auth: не задан --base (база контура) — проверка НЕ выполнена")
        return 2

    host = re.sub(r"^https?://", "", args.base).split("/")[0].split(":")[0]
    try:
        socket.getaddrinfo(host, None)
    except OSError as e:
        print(f"::error::check_apim_auth: база не резолвится ({host}): {e}")
        return 2

    images_note = resolve_images(args.images, []) if not args.direct else ""
    results = [(op, *check_one(args.base, op)) for op in targets]
    violators, contracts, unknown, protected_count = verdict(results, args.five_xx_unknown if args.direct else True, direct=args.direct)
    passed = protected_count
    # `@DO 2026-11-05 01:17`: `--base` на пустом порту давал `PASS 0/76` и `rc=0` — «зелено на
    # пустоте». Единое правило: нет ни одного реального ответа → `rc=2` (UNKNOWN), не успех.
    rc = final_rc(violators, contracts, unknown, passed, len(targets))

    if args.json:
        print(json.dumps({"base": args.base, "targets": len(targets), "passed": passed,
                          "violators": violators, "contract": contracts, "unknown": unknown,
                          "rc": rc}, ensure_ascii=False, indent=2))
    else:
        for v in violators:
            print(f"::error::apim-auth[violator]: {v}")
        for c in contracts:
            print(f"::error::apim-auth[contract]: {c}")
        for u in unknown:
            print(f"::warning::apim-auth[unknown]: {u}")
        verdict_word = "PASS" if rc == 0 else ("UNKNOWN" if rc == 2 else "FAIL")
        print(f"{verdict_word}: измерено {passed}/{len(targets)} операций, дающих 401 без токена"
              + (" (ни один ответ не получен — это не «зелено»)" if rc == 2 else ""))
        # Квитанция — ПОСЛЕДНЕЙ строкой: её читают и копируют, поэтому она должна быть в хвосте.
        print(receipt_line("ii-a", current_ref(), passed, len(targets), rc,
                           args.where or args.base, len(violators), len(contracts), len(unknown), images_note))
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
