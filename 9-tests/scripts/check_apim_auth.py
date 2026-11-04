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
import json
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


def concrete_path(path: str) -> str:
    """Подставляет вместо шаблонов `{...}` значения, которые проходят валидацию маршрута."""
    def sub(m: re.Match) -> str:
        name = m.group(1).lower()
        if "date" in name or "day" in name:
            return "2026-01-01"
        if "month" in name:
            return "2026-01"
        return GUID
    return re.sub(r"\{([^}]+)\}", sub, path)


def current_ref() -> str:
    """`REF` для квитанции: короткий SHA репозитория (или `unknown`, если git недоступен)."""
    r = subprocess.run(["git", "rev-parse", "--short", "HEAD"], capture_output=True, text=True)
    out = r.stdout.strip()
    return out if r.returncode == 0 and out else "unknown"


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


def final_rc(bad: list, passed: int, total: int) -> int:
    """Код возврата гейта: 1 — есть нарушения; **2 — ничего не измерено**; 0 — всё измерено и 401.

    `@DO 2026-11-05 01:17`: `--base` на пустом порту давал `PASS 0/76` и `rc=0` — «зелено на
    пустоте». `@TL 01:18` усилил правило: `PASS` — **только когда измерено ровно `total`/`total`**,
    а частичное измерение (`passed < total`, есть `unknown`) тоже НЕ `PASS`.
    Поэтому: нарушения → 1; измерено 0 → 2; измерено меньше total → 2 (UNKNOWN); иначе 0.
    """
    if bad:
        return 1
    if passed <= 0 or passed < total:
        return 2
    return 0


def resolve_images(arg_value: str, services: list) -> str:
    """`--images auto` → digest'ы контейнеров; иначе — как передано; пусто → `НЕ УКАЗАНО`."""
    if str(arg_value).strip().lower() == "auto":
        return images_for(list(services))
    return arg_value.strip()


def receipt_line(kind: str, ref: str, passed: int, total: int, rc: int, where: str,
                 failures: int = 0, unknown: int = 0, images: str = "") -> str:
    """Квитанция в формате @AR (`ITERATION-PLAN §19.1`) / @TL (`01:18`):

        REF=<sha> | (ii-b) PASS|FAIL|UNKNOWN защищено=N/76 | нарушителей=B | неизвестно=U |
        rc=N | where=<чем измерено> | images=<svc:digest, …>

    Требования, зашитые в текст: `PASS` — только при `нарушителей=0 И неизвестно=0`
    (то есть `passed == total`); `400`/`2xx` считаются нарушением; `UNKNOWN` не засчитывается
    никогда; `images` обязателен для квитанции релиза («код есть, образ старый» — трижды за сутки).
    """
    label = f"({kind}) "
    if rc == 0:
        body = f"{label}PASS защищено={passed}/{total}"
    elif rc == 1:
        body = f"{label}FAIL защищено={passed}/{total}"
    else:
        body = f"{label}UNKNOWN защищено={passed}/{total} (не засчитывается)"
    line = f"REF={ref} | {body} | нарушителей={failures} | неизвестно={unknown} | rc={rc}"
    line += f" | where={where or 'НЕ УКАЗАНО'} | images={images or 'НЕ УКАЗАНО'}"
    return line


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


def verdict(results: list[tuple[dict, int | None, str]],
            five_xx_violator: bool = False) -> tuple[int, list[str], list[str]]:
    """Классификация по правилу @AR (`2026-11-05 01:33-01:35`):

    «ответ получен = измерение; нарушитель — любой ответ кроме `401/403`; `UNKNOWN` — только
    когда ответа нет». Поэтому `2xx`, `400`, `415`, `404`, `405` — **нарушители**: в ASP.NET
    `[Authorize]` срабатывает до model binding и до маршрутизации, значит любой не-`401`
    ответ доказывает, что запрос дошёл до пайплайна без токена.

    Исключение (осознанное): `502/503/504` — ответ **не сервиса**, а прокси/шлюза, который до
    сервиса не дошёл. Это `[infra] UNKNOWN`, иначе прогон снаружи обвинял бы продукт в своей
    недоступности. Для строгого соответствия чужой формуле есть `five_xx_violator=True`.
    """
    bad: list[str] = []
    unknown: list[str] = []
    for op, code, url in results:
        name = f"{op.get('method')} {op.get('path')} [{op.get('operationId')}] bc={op.get('bc')}"
        if code is None:
            unknown.append(f"{name} — нет ответа: {url}")
        elif 200 <= code < 300:
            bad.append(f"{name} — {code} БЕЗ токена (операция открыта), {url}")
        elif code in (401, 403):
            continue
        elif code in (502, 503, 504) and not five_xx_violator:
            # прокси/шлюз не достучался до сервиса — это не доказательство ни «открыто», ни 401
            unknown.append(f"{name} — {code} [infra] (прокси/сервис недоступен), {url}")
        else:
            # 400/415/404/405 и прочие ответы: запрос дошёл до пайплайна без токена
            bad.append(f"{name} — {code} вместо 401 (ответ получен без токена — нарушитель), {url}")
    return (1 if bad else 0), bad, unknown


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
        rc, bad, unknown = verdict(results)

        total += 1
        # Правило @AR (`01:33-01:35`): 401/403 — отказ (зелено); 200, 500, **400, 415, 404** —
        # нарушители (ответ получен = запрос дошёл до пайплайна без токена); UNKNOWN — только
        # когда ответа нет. В наборе: a=401(ok), c=403(ok), e=404(violator), b=200, d=500, i=401(ok).
        if rc == 1 and len(bad) == 3 and len(unknown) == 0:
            ok += 1
        else:
            print(f"  [FAIL] классификатор: ждали rc=1, 3 нарушения, 0 неизвестных; "
                  f"получили rc={rc}, bad={len(bad)}, unknown={len(unknown)}")

        total += 1
        # 200, 500 и 404 без токена обязаны попасть в нарушения поимённо
        if any("B.Open" in b for b in bad) and any("D.Error" in b for b in bad) and any("E.NotFound" in b for b in bad):
            ok += 1
        else:
            print(f"  [FAIL] в нарушениях нет имён открытой/ошибочной/404 операции: {bad}")

        total += 1
        # 400/415 — тоже нарушители (аргумент @AR: [Authorize] до model binding), 502 — [infra] UNKNOWN
        cls = [
            (400, "bad"), (415, "bad"), (404, "bad"), (200, "bad"), (500, "bad"),
            (401, "ok"), (403, "ok"), (502, "unknown"),
        ]
        wrong = []
        for code, want in cls:
            _rc, b, u = verdict([({"method": "GET", "path": "/x", "operationId": "X", "bc": "C"}, code, "u")])
            got = "bad" if b else ("unknown" if u else "ok")
            if got != want:
                wrong.append(f"{code}: got {got}, want {want}")
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
        rc2, bad2, _u2 = verdict(dres)
        if rc2 == 1 and any("S.Open" in b for b in bad2) and any("bc=Catalog" in b for b in bad2):
            ok += 1
        else:
            print(f"  [FAIL] --direct классификатор: rc={rc2}, bad={bad2}")

        total += 1
        # REF в квитанции не должен быть пустым (может быть `unknown`, но не пустая строка)
        if current_ref():
            ok += 1
        else:
            print("  [FAIL] current_ref() пуст — квитанция будет без REF")

        total += 1
        # квитанция по формату @AR `ITERATION-PLAN §19.1` / @TL `01:18`:
        # `REF | (ii-b) PASS|FAIL|UNKNOWN защищено=N/76 | нарушителей | неизвестно | rc | where | images`
        good = receipt_line("ii-b", "abc1234", 76, 76, 0, "podman exec xdiary-catalog", 0, 0, "journal:abc123")
        unk = receipt_line("ii-b", "abc1234", 44, 76, 2, "", 22, 10, "")
        if ("PASS защищено=76/76" in good and "нарушителей=0" in good and "неизвестно=0" in good
                and "rc=0" in good and "where=podman exec xdiary-catalog" in good
                and "images=journal:abc123" in good
                and "UNKNOWN" in unk and "не засчитывается" in unk
                and "нарушителей=22" in unk and "неизвестно=10" in unk
                and "where=НЕ УКАЗАНО" in unk and "images=НЕ УКАЗАНО" in unk):
            ok += 1
        else:
            print(f"  [FAIL] формат квитанции: {good!r} / {unk!r}")

        total += 1
        # регресс-кейсы: баг @DO `01:17` (0 ответов → 0) и требование @TL `01:18`
        # (`PASS` только при измерено ровно total/total).
        cases_rc = [
            (([], 0, 76), 2, "ни один ответ не получен → rc=2 UNKNOWN"),
            (([], 76, 76), 0, "все 76 ответили 401 → rc=0 PASS"),
            ((["нарушитель"], 75, 76), 1, "есть нарушитель → rc=1 FAIL"),
            (([], 44, 76), 2, "частично (44/76) без нарушителей → rc=2, НЕ PASS"),
        ]
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
    ap.add_argument("--5xx-violator", action="store_true", dest="five_xx_violator",
                    help="считать 502/503/504 нарушением (строгая формула @AR: любой ответ кроме 401/403)")
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
        rc, bad, unknown = verdict(results)
        passed = len(targets) - len(bad) - len(unknown)
        rc = final_rc(bad, passed, len(targets))
        # Детали печатаем ВСЕГДА — до любых ранних выходов: иначе при недоступном контуре
        # в логе не видно, какие именно `метод+путь+bc` не измерены (@TL `00:16` п.3).
        for svc, lst in sorted(per_service.items()):
            svc_res = [r for r in results if svc_map[str(r[0].get("bc"))] == svc]
            svc_pass = sum(1 for _op, code, _u in svc_res if code in (401, 403))
            print(f"   {svc:22} {svc_pass}/{len(svc_res)} дают 401 без токена")
        for b in bad:
            print(f"::error::apim-auth(service): {b}")
        for u in unknown:
            print(f"::warning::apim-auth(service): {u}")

        map_note = f"map={args.service_map}" if args.service_map else f"template={args.host_template}"
        images_note = resolve_images(args.images, list(per_service.keys()))
        print(f"источник адресов: {map_note}")
        # «Ничего не измерено» ≠ «зелено»: контур недоступен/прокси отвечает 502 → rc=2.
        # Без этого прогон извне выглядел бы PASS-ом на пустоте (класс, который ловил @TL).
        if rc == 2:
            print("::error::check_apim_auth: ни один сервис не дал измеримого ответа — "
                  "запускать изнутри контура (@DO); это НЕ «нарушения» и НЕ «зелено»")
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, args.where, len(bad), len(unknown), images_note))
            return 2
        # @AR `17:20`: квитанция обязана содержать «чем измерено». Без `--where` это не квитанция,
        # а измерение без provenance → трактуем как UNKNOWN (rc=2), а не как PASS.
        if rc == 0 and not args.where:
            print("::error::check_apim_auth: для квитанции (ii-b) обязателен `--where` "
                  "(ADR-026 / round-checklist §3.1: без «чем измерено» rc=0 не засчитывается)")
            print(receipt_line("ii-b", current_ref(), passed, len(targets), 2, "", len(bad), len(unknown), images_note))
            return 2
        print(receipt_line("ii-b", current_ref(), passed, len(targets), rc, args.where, len(bad), len(unknown), images_note))
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
    _rc, bad, unknown = verdict(results, args.five_xx_violator)
    passed = len(targets) - len(bad) - len(unknown)
    # `@DO 2026-11-05 01:17`: `--base` на пустом порту давал `PASS 0/76` и `rc=0` — «зелено на
    # пустоте». Единое правило: нет ни одного реального ответа → `rc=2` (UNKNOWN), не успех.
    rc = final_rc(bad, passed, len(targets))

    if args.json:
        print(json.dumps({"base": args.base, "targets": len(targets), "passed": passed,
                          "failures": bad, "unresolved": unknown, "rc": rc}, ensure_ascii=False, indent=2))
    else:
        for b in bad:
            print(f"::error::apim-auth: {b}")
        for u in unknown:
            print(f"::warning::apim-auth: {u}")
        verdict_word = "PASS" if rc == 0 else ("UNKNOWN" if rc == 2 else "FAIL")
        print(f"{verdict_word}: измерено {passed}/{len(targets)} операций, дающих 401 без токена"
              + (" (ни один ответ не получен — это не «зелено»)" if rc == 2 else ""))
        # Квитанция — ПОСЛЕДНЕЙ строкой: её читают и копируют, поэтому она должна быть в хвосте.
        print(receipt_line("ii-a", current_ref(), passed, len(targets), rc,
                           args.where or args.base, len(bad), len(unknown), images_note))
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
