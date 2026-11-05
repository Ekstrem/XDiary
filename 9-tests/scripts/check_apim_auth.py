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
ANON = {"anonymous", "anonymous-inside"}

# BC в `api-index.yaml` → имя сервиса в compose (`7-deploy/podman-compose.yml`).
# Нужен для `--direct`: условие @AR сформулировано про ПРЯМОЙ запрос в сервис, а не про YARP.
SERVICE_BY_BC = {
    "Journal": "journal",
    "Partners": "partners",
    "Catalog": "catalog",
    "Client": "client",
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
                 violators: int = 0, contracts: int = 0, unknown: int = 0, images: str = "",
                 network: str = "") -> str:
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
    line += f" | network={network or 'НЕ УКАЗАНО'}"
    line += f" | via={VIA_NOTE or 'НЕ ОПРЕДЕЛЕНО'}"
    line += f" | tool={tool_id()}"
    return line


VIA_NOTE = ""   # кто ответил в текущем прогоне (@TL `05:00`); заполняется один раз в main


def via_from_headers(headers: dict, code) -> str:
    """`via=` — КТО ответил, в отличие от `where=` (откуда мерили). @TL `05:00`.

    `X-Gravitee-*` — ответил шлюз (`via=gravitee`); `Server: Kestrel` на `401` — приложение/YARP
    (`via=yarp`); `404` без `Server` — шлюз без маршрута. Иначе `НЕ ОПРЕДЕЛЕНО`, и тогда
    «перемерено на новом входе» **не утверждается**: вход тот же, а объект за ним мог не измениться.
    """
    low = {str(k).lower(): str(v) for k, v in (headers or {}).items()}
    if any(k.startswith("x-gravitee") for k in low):
        return "gravitee"
    if str(low.get("server", "")).lower() == "kestrel" and code in (401, 403):
        return "yarp"
    if code == 404 and not low.get("server"):
        return "gateway-no-route"
    return "НЕ ОПРЕДЕЛЕНО"


def probe_via(base: str, timeout: float = 12.0) -> str:
    """Один запрос за `via=`: только смотрит заголовки, ничего не меняет."""
    import http.client
    from urllib.parse import urlsplit
    u = urlsplit(base.rstrip("/"))
    cls = http.client.HTTPSConnection if u.scheme == "https" else http.client.HTTPConnection
    try:
        conn = cls(u.hostname, u.port, timeout=timeout)
        try:
            conn.request("GET", (u.path or "") + "/api/v1/catalog")
            r = conn.getresponse()
            hdrs = {k.lower(): v for k, v in r.getheaders()}
            r.read(200)
            return via_from_headers(hdrs, r.status)
        finally:
            conn.close()
    except Exception:                                   # noqa: BLE001
        return "НЕ ОПРЕДЕЛЕНО"


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


def unresolved_services(hosts: dict) -> list:
    """BC, чьё имя не резолвится — дефект **карты адресов**, а не контура (@DO/@TL `03:04`).

    Класс ошибки: `SERVICE_BY_BC["Client"] = "clients"` (множественное) при контейнере
    `xdiary-client` терял 5 операций в `unknown` — то есть одна опечатка выглядела как
    «сервис лежит», а квитанция показывала `35` вместо `40`. Такую ошибку нельзя оставлять
    на разбор: её надо валить **до** запросов, с именем BC в тексте.
    """
    import socket
    from urllib.parse import urlsplit
    bad = []
    for bc, base in hosts.items():
        host = urlsplit(base).hostname or ""
        if not host:
            bad.append(f"{bc} — адрес без хоста: {base}")
            continue
        try:
            socket.getaddrinfo(host, None)
        except OSError as e:
            bad.append(f"{bc} — хост `{host}` не резолвится ({type(e).__name__}); "
                       f"это дефект карты адресов (SERVICE_BY_BC), а не контура")
    return bad


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
            protected_count += 1
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

def identity_rc(rc: int, passed: int, violators: int, contracts: int, unknown: int, total: int) -> int:
    """Тождество на **основном пути** (`@DO 02:37`): если `N+V+C+U != total`, квитанция недействительна.

    Раньше `identity_ok()` вызывался только в `--selftest`, поэтому прогон с `сумма=41/76` уходил как
    `rc=1` — то есть защита существовала, но не срабатывала. `rc=2` уже недействителен, его не трогаем.
    """
    if rc in (0, 1) and not identity_ok(passed, violators, contracts, unknown, total):
        return 2
    return rc


def via_rc(rc: int, kind: str = "ii-b") -> int:
    """Засчитывать `PASS` только при доказанном `via=` (@TL `05:00`, @AR `05:15`).

    `(ii-a)` считается **только снаружи по публичному имени** и **требует `via=gravitee`**
    (@AR `05:15`): иначе замер доказывает «публичный вход закрыт», но не «пользователь идёт
    через APIM» — это верно и для YARP. Прочие условия требуют лишь определённого `via=`.
    """
    if rc != 0:
        return rc
    if kind == "ii-a":
        if VIA_NOTE != "gravitee":
            print(f"::error::check_apim_auth: (ii-a) не засчитывается — `via={VIA_NOTE or 'НЕ ОПРЕДЕЛЕНО'}` "
                  "(требуется `via=gravitee`: APIM в пути публичного входа); rc=2")
            return 2
        return rc
    if VIA_NOTE in ("", "НЕ ОПРЕДЕЛЕНО"):
        print("::error::check_apim_auth: `via=` не определён — PASS не засчитывается "
              "(ADR-026: квитанция обязана называть, кто ответил); rc=2")
        return 2
    return rc


def classify_code(code: int | None, five_xx_unknown: bool) -> str:
    """Класс одного ответа — единственный источник правды для `verdict` и `breakdown_by_bc`."""
    if code is None:
        return "unknown"
    if code in (401, 403):
        return "protected"
    if code in (404, 405):
        return "contract"
    if code in (502, 503, 504) and five_xx_unknown:
        return "unknown"
    return "violator"


def breakdown_by_bc(results, five_xx_unknown: bool) -> dict:
    """Расшифровка по BC (ADR-026: «`UNKNOWN` обязан быть расшифрован по BC, а не числом»).

    Без неё `28 unknown` — агрегат из двух разных фактов: «измеритель не видит сервис» (наш дефект)
    против «сервис лежит» (дефект продукта). По BC видно, чей это дефект, без переписки.
    """
    out: dict = {}
    for op, code, _url in results:
        bc = str(op.get("bc"))
        row = out.setdefault(bc, {"protected": 0, "violator": 0, "contract": 0, "unknown": 0})
        row[classify_code(code, five_xx_unknown)] += 1
    return out


def breakdown_line(table: dict) -> str:
    """Компактная строка для лога: только ненулевые классы, по BC."""
    parts = []
    for cls in ("violator", "contract", "unknown"):
        items = [f"{bc}:{row[cls]}" for bc, row in sorted(table.items()) if row[cls]]
        if items:
            parts.append(f"{cls} = " + ", ".join(items))
    return " | ".join(parts) if parts else "нарушителей/contract/unknown нет"


def internal_ops(ops) -> list:
    """Операции внутреннего контура (`/internal/**` или `auth=anonymous-inside`) — условие (ii-c).

    Компенсация за исключение `anonymous-inside` из знаменателя `(ii-b)` (@AR `03:14`): такие
    ручки **не обязаны** отвечать `401`, но **обязаны быть недостижимы снаружи**. Критерий:
    через публичный вход (`Funnel`/YARP/APIM) допустимы только `401`/`403`/`404`;
    любой `2xx` = публикация внутреннего API (серьёзнее открытой ручки) → `rc=1`.
    """
    out = []
    for op in ops:
        if str(op.get("auth", "")).strip().lower() == "anonymous-inside" \
                or str(op.get("path", "")).startswith("/internal"):
            out.append(op)
    return out


def verdict_internal(results):
    """Классификация (ii-c): ok = `401/403/404`; `2xx` и прочие полученные ответы = нарушение."""
    ok, bad, unknown = [], [], []
    for op, code, url in results:
        name = f"{op.get('method')} {op.get('path')} [{op.get('operationId')}]"
        if code is None:
            unknown.append(f"{name} — нет ответа: {url}")
        elif code in (401, 403, 404):
            ok.append(f"{name} — {code} (недостижим снаружи)")
        else:
            bad.append(f"{name} — {code}: внутренняя операция ДОСТИЖИМА снаружи "
                       f"(допустимы только 401/403/404), {url}")
    return ok, bad, unknown


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
        # РЕГРЕССИЯ-КЕЙС: «все 401» обязано давать rc=0 и passed=total. Пока 401 выходил
        # через `continue` без инкремента, passed всегда был 0 → гейт не мог дать PASS вообще
        # (и это не поймал ни один кейс: везде были нарушения или пустой ответ).
        all_ok = [({"method": "GET", "path": f"/x{i}", "operationId": f"X{i}", "bc": "C"}, 401, "u")
                  for i in range(5)]
        _v, _c, _u, _p = verdict(all_ok)
        if not _v and not _c and not _u and _p == 5 and final_rc(_v, _c, _u, _p, 5) == 0:
            ok += 1
        else:
            print(f"  [FAIL] все 401: passed={_p}/5 (ждали 5), rc={final_rc(_v, _c, _u, _p, 5)} (ждали 0)")

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
        # Знаменатель (ii-b) по @AR `03:14`: `anonymous` И `anonymous-inside` вне счёта —
        # внутренние ручки не обязаны давать 401, они проверяются условием (ii-c).
        # В наборе 7 операций: минус 1 `anonymous` и минус 1 `anonymous-inside` = 5.
        if len(targets) == 5:
            ok += 1
        else:
            print(f"  [FAIL] знаменатель: ждали 5 (7 - anonymous - anonymous-inside), получили {len(targets)}")

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
        total += 1
        # `network=` (@AR `02:17`): обязателен при замере из контейнера, печатается в квитанции
        with_net = receipt_line("ii-b", "abc", 76, 76, 0, "podman run", 0, 0, 0, "", "xdiary")
        no_net = receipt_line("ii-b", "abc", 0, 76, 2, "podman run", 0, 0, 76, "", "")
        if "network=xdiary" in with_net and "network=НЕ УКАЗАНО" in no_net:
            ok += 1
        else:
            print(f"  [FAIL] network= в квитанции: {with_net!r} / {no_net!r}")

        total += 1
        # Тождество на ОСНОВНОМ пути (@DO `02:37`): пока `identity_ok` вызывался только в selftest,
        # прогон с `сумма=41/76` уходил как `rc=1`. `identity_rc` обязан превратить это в `rc=2`.
        irc = [
            ((1, 0, 8, 0, 33, 76), 2, "сумма 41/76 -> недействительна"),
            ((1, 40, 8, 0, 28, 76), 1, "сумма 76/76 -> rc сохраняется"),
            ((0, 76, 0, 0, 0, 76), 0, "всё защищено -> PASS"),
            ((2, 0, 0, 0, 76, 76), 2, "rc=2 не пересчитывается"),
        ]
        total += 1
        # Форма реального замера (@TL `02:39`, @DO `02:35`): 40 защищено + 8 нарушителей +
        # 28 неизвестно = 76. Тождество сходится, поэтому rc=1 (нарушители есть), а НЕ 2.
        shape = ([({"method": "GET", "path": f"/ok{i}", "operationId": f"OK{i}", "bc": "Catalog"}, 401, "u")
                  for i in range(40)]
                 + [({"method": "GET", "path": f"/v{i}", "operationId": f"V{i}", "bc": "Analytics"}, 200, "u")
                    for i in range(8)]
                 + [({"method": "GET", "path": f"/u{i}", "operationId": f"U{i}", "bc": "Journal"}, None, "u")
                    for i in range(28)])
        _v, _c, _u, _p = verdict(shape, five_xx_unknown=False, direct=True)
        _rc = identity_rc(final_rc(_v, _c, _u, _p, 76), _p, len(_v), len(_c), len(_u), 76)
        # Вторая форма — дословный критерий @TL `02:40`: 40 `401` + 36 прочих (все `403`)
        # -> protected=76, сумма 76/76, rc=0. Один кейс на обе формы реального замера.
        shape_ok = ([({"method": "GET", "path": f"/p{i}", "operationId": f"P{i}", "bc": "Catalog"}, 403, "u")
                     for i in range(36)])
        allpot = list(shape[:0]) + [r for r in shape[:40]] + shape_ok
        _v2, _c2, _u2, _p2 = verdict(allpot, five_xx_unknown=False, direct=True)
        _rc2 = identity_rc(final_rc(_v2, _c2, _u2, _p2, 76), _p2, len(_v2), len(_c2), len(_u2), 76)
        if (_p == 40 and len(_v) == 8 and len(_c) == 0 and len(_u) == 28
                and _p + len(_v) + len(_c) + len(_u) == 76 and _rc == 1
                and _p2 == 76 and not _v2 and not _c2 and not _u2 and _rc2 == 0):
            ok += 1
        else:
            print(f"  [FAIL] формы 40/8/0/28 и 76/0/0/0: "
                  f"({_p},{len(_v)},{len(_c)},{len(_u)},rc={_rc}) / ({_p2},{len(_v2)},{len(_c2)},{len(_u2)},rc={_rc2})")

        total += 1
        # @TL `02:41` п.2-3: «операция без ответа обязана закрываться в `unknown`», иначе сумма
        # не сходится (`неизвестно=0` при `измерено 0/76`). 76 без ответа -> unknown=76, rc=2.
        norsp = [({"method": "GET", "path": f"/n{i}", "operationId": f"N{i}", "bc": "Catalog"}, None, "u")
                 for i in range(76)]
        _v3, _c3, _u3, _p3 = verdict(norsp)
        _rc3 = identity_rc(final_rc(_v3, _c3, _u3, _p3, 76), _p3, len(_v3), len(_c3), len(_u3), 76)
        if (not _v3 and not _c3 and len(_u3) == 76 and _p3 + len(_u3) == 76 and _rc3 == 2):
            ok += 1
        else:
            print(f"  [FAIL] 76 без ответа: unknown={len(_u3)} protected={_p3} сумма={_p3 + len(_v3) + len(_c3) + len(_u3)} rc={_rc3}")

        total += 1
        # @TL `03:04`: несуществующее имя сервиса не должно «терять» операцию. Два уровня:
        # (1) preflight ловит неверный адрес карты ДО запросов; (2) если ответа всё же нет —
        # операция закрывается в `unknown` (не исчезает).
        bad_map = unresolved_services({"Client": "http://xdiary-clients.invalid:8080"})
        good_map = unresolved_services({"Local": f"http://127.0.0.1:{port}"})
        lost = [({"method": "GET", "path": "/x", "operationId": "X", "bc": "Client"}, None, "http://xdiary-clients.invalid:8080/x")]
        _v4, _c4, _u4, _p4 = verdict(lost)
        if (len(bad_map) == 1 and "Client" in bad_map[0] and not good_map
                and len(_u4) == 1 and _p4 == 0 and _p4 + len(_u4) == 1):
            ok += 1
        else:
            print(f"  [FAIL] карта/несуществующий хост: bad_map={bad_map}, good_map={good_map}, unknown={len(_u4)}")

        total += 1
        # (ii-c), TE-056 (@TL `03:14`): `/internal/**` снаружи обязан давать 401/403/404.
        # Проверяем обе стороны: 2xx = нарушение, 401/404 = норма, нет ответа = unknown.
        iops = internal_ops(load_ops())
        if len(iops) != 2 or not all(str(o.get("path", "")).startswith("/internal") for o in iops):
            print(f"  [FAIL] (ii-c): внутренних операций {len(iops)}, ждали 2 /internal/**")
        else:
            rv = [({"method": "GET", "path": "/internal/journal/records",
                    "operationId": "Journal.ListAll", "bc": "Journal"}, 200, "u")]
            ro = [({"method": "GET", "path": "/internal/journal/records",
                    "operationId": "Journal.ListAll", "bc": "Journal"}, 404, "u"),
                  ({"method": "POST", "path": "/internal/partners/events",
                    "operationId": "Journal.IngestPartnerEvent", "bc": "Journal"}, 401, "u")]
            rn = [({"method": "GET", "path": "/internal/journal/records",
                    "operationId": "Journal.ListAll", "bc": "Journal"}, None, "u")]
            okv, badv, unkv = verdict_internal(rv)
            oko, bado, unko = verdict_internal(ro)
            okn, badn, unkn = verdict_internal(rn)
            if (len(badv) == 1 and not okv and not unkv
                    and len(oko) == 2 and not bado and not unko
                    and len(unkn) == 1 and not okn and not badn):
                ok += 1
            else:
                print(f"  [FAIL] (ii-c) классификация: 200->{len(badv)} bad, "
                      f"401/404->{len(oko)} ok, none->{len(unkn)} unknown")

        total += 1
        # `via=` (@TL `05:00`): квитанция обязана говорить, КТО ответил, а не только откуда мерили.
        vias = [
            ({"X-Gravitee-Request-Id": "x"}, 401, "gravitee"),
            ({"Server": "Kestrel"}, 401, "yarp"),
            ({}, 404, "gateway-no-route"),
            ({}, 200, "НЕ ОПРЕДЕЛЕНО"),
            ({"Server": "Caddy"}, 502, "НЕ ОПРЕДЕЛЕНО"),
        ]
        bad_via = [f"{code}: got {via_from_headers(h, code)}, want {want}"
                   for (h, code, want) in vias if via_from_headers(h, code) != want]
        # и сама квитанция печатает поле (значение берёт из VIA_NOTE)
        global VIA_NOTE
        saved = VIA_NOTE
        VIA_NOTE = "gravitee"
        with_via = receipt_line("ii-a", "abc", 74, 74, 0, "w", 0, 0, 0, "", "")
        VIA_NOTE = saved
        saved2 = VIA_NOTE
        VIA_NOTE = ""
        rc_no_via = via_rc(0)
        VIA_NOTE = "yarp"
        rc_via = via_rc(0)                 # (ii-b)/(ii-c): yarp достаточно
        rc_iia_yarp = via_rc(0, "ii-a")    # (ii-a): yarp НЕ достаточно (@AR 05:15)
        VIA_NOTE = "gravitee"
        rc_iia_grav = via_rc(0, "ii-a")
        VIA_NOTE = saved2
        if (not bad_via and "via=gravitee" in with_via and rc_no_via == 2 and rc_via == 0
                and rc_iia_yarp == 2 and rc_iia_grav == 0):
            ok += 1
        else:
            print(f"  [FAIL] via=: {bad_via or 'поле не печатается'} / {with_via!r}")

        bad_irc = [t for (args, want, t) in irc if identity_rc(*args) != want]
        if not bad_irc:
            ok += 1
        else:
            print(f"  [FAIL] identity_rc: неверно {bad_irc}")

        total += 1
        # Расшифровка UNKNOWN по BC (ADR-026): «28 неизвестно» — агрегат двух разных фактов,
        # поэтому квитанция обязана называть BC, а не только число.
        dres = [
            ({"method": "GET", "path": "/a", "operationId": "A", "bc": "Catalog"}, 401, "u"),
            ({"method": "GET", "path": "/b", "operationId": "B", "bc": "Catalog"}, 403, "u"),
            ({"method": "GET", "path": "/c", "operationId": "C", "bc": "Journal"}, None, "u"),
            ({"method": "GET", "path": "/d", "operationId": "D", "bc": "Partners"}, None, "u"),
            ({"method": "GET", "path": "/e", "operationId": "E", "bc": "Analytics"}, 200, "u"),
            ({"method": "GET", "path": "/f", "operationId": "F", "bc": "Privacy"}, 404, "u"),
        ]
        table = breakdown_by_bc(dres, five_xx_unknown=False)
        line_txt = breakdown_line(table)
        want_table = {
            "Catalog": {"protected": 2, "violator": 0, "contract": 0, "unknown": 0},
            "Journal": {"protected": 0, "violator": 0, "contract": 0, "unknown": 1},
            "Partners": {"protected": 0, "violator": 0, "contract": 0, "unknown": 1},
            "Analytics": {"protected": 0, "violator": 1, "contract": 0, "unknown": 0},
            "Privacy": {"protected": 0, "violator": 0, "contract": 1, "unknown": 0},
        }
        if (table == want_table and "violator = Analytics:1" in line_txt
                and "contract = Privacy:1" in line_txt
                and "unknown = Journal:1, Partners:1" in line_txt):
            ok += 1
        else:
            print(f"  [FAIL] расшифровка по BC: {table} / {line_txt!r}")

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
    ap.add_argument("--probe-entry", action="store_true", dest="probe_entry",
                    help="доказать, ЧТО стоит в пути трафика: заголовки ответа + поведение "
                         "несуществующего пути (APIM отдаёт 404 и X-Gravitee-*, YARP — 401)")
    ap.add_argument("--all", action="store_true", dest="all_conditions",
                    help="одна квитанция на все условия: (ii-a) публичный вход + (ii-c) /internal/** "
                         "(при --direct дополнительно (ii-b))")
    ap.add_argument("--ii-c", action="store_true", dest="ii_c",
                    help="условие (ii-c): /internal/** снаружи обязан давать 401/403/404, 2xx = публикация внутреннего API (rc=1)")
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
    ap.add_argument("--network", default="",
                    help="сеть контура, если мерили из контейнера (например xdiary); обязательна для --direct (ADR-026)")
    ap.add_argument("--images", default="",
                    help="диджесты образов для квитанции: auto (podman inspect) | journal:abc123, catalog:def456")
    ap.add_argument("--service-map", default="",
                    help='JSON {"Catalog": "xdiary-catalog:8080"} — переопределить адреса сервисов')
    args = ap.parse_args()

    global VIA_NOTE
    VIA_NOTE = probe_via(args.base) if args.base else ""

    if args.selftest:
        return selftest()

    if args.probe_entry and not args.all_conditions:
        # «Переключено» = доказано заголовком/поведением, а не тем, что команда выполнена
        # (@DO `03:56`). Поэтому смотрим три факта: заголовки, несуществующий путь, /health.
        if not args.base:
            print("::error::check_apim_auth: для --probe-entry нужен --base <публичный вход>")
            return 2
        import http.client
        from urllib.parse import urlsplit

        def probe(path: str, method: str = "GET"):
            u = urlsplit(args.base.rstrip("/"))
            cls = http.client.HTTPSConnection if u.scheme == "https" else http.client.HTTPConnection
            conn = cls(u.hostname, u.port, timeout=12)
            try:
                conn.request(method, (u.path or "") + path, headers={"Accept": "*/*"})
                r = conn.getresponse()
                hdrs = {k.lower(): v for k, v in r.getheaders()}
                r.read(200)
                return r.status, hdrs
            except Exception as e:                      # noqa: BLE001 — нужен факт, не трасса
                return None, {"error": f"{type(e).__name__}: {e}"}
            finally:
                conn.close()

        code_unknown, h_unknown = probe("/api/v1/does-not-exist-xyz")
        code_health, h_health = probe("/health")
        code_api, h_api = probe("/api/v1/catalog")
        grav = sorted(k for k in h_api if k.startswith("x-gravitee"))
        server = h_api.get("server", h_api.get("Server", "НЕ УКАЗАН"))
        print(f"   несуществующий путь /api/v1/does-not-exist-xyz → {code_unknown}")
        print(f"   /health → {code_health} · /api/v1/catalog → {code_api}")
        print(f"   Server: {server} · X-Gravitee-*: {grav or 'нет'}")
        if grav:
            entry, why = "APIM", "есть заголовки X-Gravitee-*"
        elif code_unknown == 401:
            entry, why = "YARP (или APIM с catch-all)", \
                "несуществующий путь даёт 401, а не 404: маршрут ловит всё и требует токен"
        elif code_unknown == 404:
            entry, why = "шлюз без маршрута (вероятно APIM)", \
                "несуществующий путь даёт 404 и нет заголовка Server — так отвечает шлюз, а не приложение"
        else:
            entry, why = "НЕ ОПРЕДЕЛЕНО", "нет ни X-Gravitee-*, ни однозначного кода"
        print(f"ВХОД: {entry} — {why} (where={args.where or args.base}, tool={tool_id()})")
        return 0 if entry != "НЕ ОПРЕДЕЛЕНО" else 2

    if args.all_conditions:
        # Одно действие — один артефакт: перед переключением трафика решение принимается по ТРЁМ
        # условиям, и разрозненные прогоны дают три числа из трёх моментов времени (@EX `03:31`).
        if not args.base:
            print("::error::check_apim_auth: для --all нужен --base <публичный вход>")
            return 2
        ops_all = [o for o in load_ops(args.index) if requires_auth(o)]
        res_all = [(op, *check_one(args.base, op)) for op in ops_all]
        v_a, c_a, u_a, p_a = verdict(res_all, args.five_xx_unknown if args.direct else True,
                                     direct=args.direct)
        rc_a = identity_rc(final_rc(v_a, c_a, u_a, p_a, len(ops_all)), p_a,
                           len(v_a), len(c_a), len(u_a), len(ops_all))
        iops = internal_ops(load_ops(args.index))
        res_c = [(op, *check_one(args.base, op)) for op in iops]
        ok_c, bad_c, unk_c = verdict_internal(res_c)
        rc_c = 1 if bad_c else (2 if (unk_c or not ok_c) else 0)
        where = args.where or args.base
        # `via_rc` ДО печати: иначе квитанция скажет `PASS rc=0`, а ИТОГ — `rc=2` (разные вердикты
        # об одном замере — ровно тот класс, который вычищаем).
        rc_a = via_rc(rc_a, "ii-a")
        rc_c = via_rc(rc_c, "ii-c")
        print(receipt_line("ii-a", current_ref(), p_a, len(ops_all), rc_a, where,
                           len(v_a), len(c_a), len(u_a),
                           resolve_images(args.images, []) if not args.direct else "", args.network))
        print(receipt_line("ii-c", current_ref(), len(ok_c), len(iops), rc_c, where,
                           len(bad_c), 0, len(unk_c),
                           resolve_images(args.images, []) if not args.direct else "", args.network))
        # Если попросили и пробу входа — печатаем её в том же прогоне: «после переключения»
        # должно сниматься одной командой, иначе сравнивают два прогона из разных моментов.
        if args.probe_entry:
            code_unknown, h_unknown = None, {}
            import http.client
            from urllib.parse import urlsplit
            u = urlsplit(args.base.rstrip("/"))
            cls = http.client.HTTPSConnection if u.scheme == "https" else http.client.HTTPConnection
            conn = cls(u.hostname, u.port, timeout=12)
            try:
                conn.request("GET", (u.path or "") + "/api/v1/does-not-exist-xyz")
                r = conn.getresponse()
                code_unknown = r.status
                h_unknown = {k.lower(): v for k, v in r.getheaders()}
                r.read(200)
            except Exception as e:                       # noqa: BLE001
                print(f"   проба входа: ошибка {type(e).__name__}: {e}")
            finally:
                conn.close()
            grav = sorted(k for k in h_unknown if k.startswith("x-gravitee"))
            server = h_unknown.get("server", "НЕ УКАЗАН")
            print(f"   проба входа: неизвестный путь → {code_unknown} · Server: {server} · "
                  f"X-Gravitee-*: {grav or 'нет'}")
            if grav:
                entry = "APIM (есть X-Gravitee-*)"
            elif code_unknown == 401:
                entry = "YARP/приложение (неизвестный путь требует токен)"
            elif code_unknown == 404 and str(server).upper() in ("НЕ УКАЗАН", "НЕ УКАЗАНО", ""):
                entry = "шлюз без маршрута (вероятно APIM: 404 и нет заголовка Server)"
            else:
                entry = "НЕ ОПРЕДЕЛЕНО"
            print(f"   ВХОД: {entry}")
        worst = max(rc_a, rc_c)
        names = {0: "PASS", 1: "FAIL", 2: "НЕ ЗАСЧИТАНО"}
        # Тождество протокола (ADR-026, @AR `04:15`): знаменатели `(ii-a)` и `(ii-c)` вместе
        # покрывают ВСЕ операции индекса, кроме `anonymous` (74 + 2 = 76 из 80). Если сумма
        # разойдётся — значит класс операций выпал из обоих условий, и протокол недействителен.
        all_ops = load_ops(args.index)
        # `anonymous` считаем ЯВНО, а не как остаток: остаток делает проверку тавтологией
        # (сумма всегда сходится по построению) — ровно класс, который мы ловим четвёртый раз.
        anon_cnt = len([o for o in all_ops
                        if str(o.get("auth", "")).strip().lower() == "anonymous"])
        protocol_sum = len(ops_all) + len(iops)
        expected = len(all_ops) - anon_cnt
        if protocol_sum != expected:
            print(f"::error::check_apim_auth: знаменатели не покрывают индекс: "
                  f"{len(ops_all)} + {len(iops)} = {protocol_sum}, ожидалось {expected} "
                  f"(в индексе {len(all_ops)}, anonymous {anon_cnt}) — протокол недействителен (rc=2)")
            worst = 2
        print(f"ИТОГ: (ii-a) {names[rc_a]} {p_a}/{len(ops_all)} · (ii-c) {names[rc_c]} "
              f"{len(ok_c)}/{len(iops)} · знаменатели {len(ops_all)}+{len(iops)}={protocol_sum} "
              f"из {len(all_ops)} (anonymous {anon_cnt}) · rc={worst}")
        return worst

    if args.ii_c:
        ops = internal_ops(load_ops(args.index))
        if not ops:
            print("::error::check_apim_auth: в индексе нет операций /internal/** — проверять нечего (rc=2)")
            return 2
        if not args.base:
            print("::error::check_apim_auth: для (ii-c) нужен --base <публичный вход> (Funnel/YARP)")
            return 2
        results = [(op, *check_one(args.base, op)) for op in ops]
        ok, bad, unk = verdict_internal(results)
        rc_c = 0
        if bad:
            rc_c = 1
        elif unk or not ok:
            rc_c = 2
        for b in bad:
            print(f"::error::apim-auth(ii-c)[violator]: {b}")
        for u in unk:
            print(f"::warning::apim-auth(ii-c)[unknown]: {u}")
        for o in ok:
            print(f"   ok: {o}")
        images_c = resolve_images(args.images, []) if not args.direct else ""
        print(receipt_line("ii-c", current_ref(), len(ok), len(ops), rc_c,
                           args.where or args.base, len(bad), 0, len(unk), images_c, args.network))
        return rc_c

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

        # Прежде чем мерить: имена должны резолвиться. Иначе опечатка в карте даст `unknown`
        # и «неизвестно», неотличимое от лежащего сервиса (@DO/@TL `03:04`).
        host_map = {bc: resolve_host(svc_map, bc, args.host_template)
                    for bc in {str(op.get("bc")) for op in targets}}
        bad_hosts = unresolved_services(host_map)
        if bad_hosts:
            for b in bad_hosts:
                print(f"::error::apim-auth(service)[map]: {b}")
            print("::error::check_apim_auth: карта адресов неверна — прогон НЕ начат (rc=2); "
                  "исправь SERVICE_BY_BC или передай --service-map")
            # images= считаем здесь же: до этого места `images_note` ещё не определён,
            # а квитанция обязана быть полной даже при отказе (иначе падение вместо rc=2).
            print(receipt_line("ii-b", current_ref(), 0, len(targets), 2, args.where,
                               0, 0, len(targets),
                               resolve_images(args.images, sorted(host_map)), args.network))
            return 2

        # ADR-026: при замере из контейнера `network=` обязателен. Проверяем ДО запросов,
        # иначе получим «измерил, но квитанция недействительна» — wasted run.
        if not args.network:
            print("::error::check_apim_auth: для (ii-b) обязателен `--network <имя сети>` "
                  "(ADR-026: замер изнутри контура обязан называть сеть); rc=2 UNKNOWN")
            print(receipt_line("ii-b", current_ref(), 0, len(targets), 2, args.where,
                               0, 0, len(targets), "", ""))
            return 2

        results: list[tuple[dict, int | None, str]] = []
        for svc, lst in sorted(per_service.items()):
            host = host_for(str(lst[0].get("bc")))
            for op in lst:
                url = host.rstrip("/") + concrete_path(str(op["path"]))
                code, got = check_url(url, str(op.get("method", "GET")))
                results.append((op, code, got))
        violators, contracts, unknown, protected_count = verdict(results, args.five_xx_unknown if args.direct else True, direct=args.direct)
        passed = protected_count
        # Порядок важен (@TL `02:39` п.2): сначала базовый вердикт, затем тождество — и только
        # потом печать. Обратный порядок затирал результат `identity_rc` вызовом `final_rc`.
        rc = final_rc(violators, contracts, unknown, passed, len(targets))
        rc = identity_rc(rc, passed, len(violators), len(contracts), len(unknown), len(targets))
        rc = via_rc(rc, "ii-a")
        print("   расшифровка по BC: " + breakdown_line(breakdown_by_bc(
            results, args.five_xx_unknown if args.direct else True)))
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
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, args.where, len(violators), len(contracts), len(unknown), images_note, args.network))
            return 2
        # @AR `17:20`: квитанция обязана содержать «чем измерено». Без `--where` это не квитанция,
        # а измерение без provenance → трактуем как UNKNOWN (rc=2), а не как PASS.
        if rc == 0 and not args.where:
            print("::error::check_apim_auth: для квитанции (ii-b) обязателен `--where` "
                  "(ADR-026 / round-checklist §3.1: без «чем измерено» rc=0 не засчитывается)")
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, "", len(violators), len(contracts), len(unknown), images_note, args.network))
            return 2
        print(receipt_line("ii-b", current_ref(), passed, len(targets), rc, args.where, len(violators), len(contracts), len(unknown), images_note, args.network))
        return rc

    if args.dry_run:
        for op in targets:
            print(f"   {op.get('method'):6} {op.get('path'):52} {op.get('bc')}  auth={op.get('auth')}")
        total_idx = [o for o in load_ops(args.index)]
        print(f"DRY-RUN: проверять нечего без стенда; целей {len(targets)} "
              f"(в индексе {len(total_idx)}, без токена: 4 anonymous + 2 anonymous-inside)")
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
    if not args.json:
        print("   расшифровка по BC: " + breakdown_line(breakdown_by_bc(
            results, args.five_xx_unknown if args.direct else True)))
    # `@DO 2026-11-05 01:17`: `--base` на пустом порту давал `PASS 0/76` и `rc=0` — «зелено на
    # пустоте». Единое правило: нет ни одного реального ответа → `rc=2` (UNKNOWN), не успех.
    rc = final_rc(violators, contracts, unknown, passed, len(targets))
    # Тождество на основном пути (`@DO` `02:37`): расхождение суммы делает квитанцию недействительной.
    rc = identity_rc(rc, passed, len(violators), len(contracts), len(unknown), len(targets))
    rc = via_rc(rc, "ii-a")

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
                           args.where or args.base, len(violators), len(contracts), len(unknown), images_note, args.network))
    return rc


if __name__ == "__main__":
    raise SystemExit(main())