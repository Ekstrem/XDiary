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

Коды возврата: 0 — все неанонимные операции дают 401; 1 — есть нарушения (список в выводе);
2 — не найдена база/индекс/нет PyYAML (это не «зелено», а «не проверено»).

`anonymous-inside` (`/internal/**`) считается **не** анонимным снаружи: это внутренний контур,
снаружи он обязан отвечать 401 — поэтому входит в проверку.
"""
from __future__ import annotations

import argparse
import json
import re
import socket
import sys
import urllib.error
import urllib.request

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


def verdict(results: list[tuple[dict, int | None, str]]) -> tuple[int, list[str], list[str]]:
    bad: list[str] = []
    unknown: list[str] = []
    for op, code, url in results:
        name = f"{op.get('method')} {op.get('path')} [{op.get('operationId')}]"
        if code is None:
            unknown.append(f"{name} — нет ответа: {url}")
        elif 200 <= code < 300:
            bad.append(f"{name} — {code} БЕЗ токена (операция открыта), {url}")
        elif code in (401, 403):
            continue
        elif code in (404, 405):
            # маршрут не сматчился — это не «открыто», но и не доказанное 401: показываем отдельно
            unknown.append(f"{name} — {code} (маршрут не сматчился, требует ручной сверки), {url}")
        elif code in (502, 503, 504):
            # Инфраструктурный ответ (прокси/шлюз, сервис недоступен) — НЕ «операция открыта»
            # и не доказательство 401. Класс `[infra]`, как в global-setup: иначе прогон извне
            # контура обвинял бы продукт в собственной недоступности.
            unknown.append(f"{name} — {code} [infra] (прокси/сервис недоступен), {url}")
        else:
            bad.append(f"{name} — {code} вместо 401, {url}")
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
        # 401 и 403 — отказ (зелено); 200 и 500 — дефект (красно); 404 — отдельно
        if rc == 1 and len(bad) == 2 and len(unknown) == 1:
            ok += 1
        else:
            print(f"  [FAIL] классификатор: ждали rc=1, 2 нарушения, 1 неразрешённый; "
                  f"получили rc={rc}, bad={len(bad)}, unknown={len(unknown)}")

        total += 1
        # 200 без токена обязан попасть в список нарушителей поимённо
        if any("B.Open" in b for b in bad) and any("D.Error" in b for b in bad):
            ok += 1
        else:
            print(f"  [FAIL] в нарушениях нет имён открытой/ошибочной операции: {bad}")

        total += 1
        # фильтр: `anonymous` исключён, `anonymous-inside` включён (снаружи обязан 401)
        if len(targets) == 6:
            ok += 1
        else:
            print(f"  [FAIL] фильтр анонимных: ждали 6 (7 операций минус `anonymous`), получили {len(targets)}")
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

    # --- режим --direct: измеряем УСЛОВИЕ @AR (прямой запрос в сервис), а не прокси ---
    if args.direct:
        unmapped = sorted({str(op.get("bc")) for op in targets if str(op.get("bc")) not in SERVICE_BY_BC})
        if unmapped:
            print(f"::error::check_apim_auth: нет маппинга BC → сервис для {unmapped}")
            return 2
        per_service: dict[str, list[dict]] = {}
        for op in targets:
            per_service.setdefault(SERVICE_BY_BC[str(op.get("bc"))], []).append(op)
        if args.dry_run:
            for svc, lst in sorted(per_service.items()):
                print(f"   {svc:22} {len(lst):3} операций   {args.host_template.format(service=svc)}")
            print(f"DRY-RUN --direct: сервисов {len(per_service)}, целей {len(targets)} "
                  f"(снаружи не видно — запускать изнутри контура, @DO)")
            return 0

        results: list[tuple[dict, int | None, str]] = []
        for svc, lst in sorted(per_service.items()):
            host = args.host_template.format(service=svc)
            for op in lst:
                url = host.rstrip("/") + concrete_path(str(op["path"]))
                code, got = check_url(url, str(op.get("method", "GET")))
                results.append((op, code, got))
        rc, bad, unknown = verdict(results)
        passed = len(targets) - len(bad) - len(unknown)
        # «Ничего не измерено» ≠ «зелено»: контур недоступен/прокси отвечает 502 → rc=2.
        # Без этого прогон извне выглядел бы PASS-ом на пустоте (класс, который ловил @TL).
        if not bad and passed == 0:
            print("::error::check_apim_auth: ни один сервис не дал измеримого ответа — "
                  "запускать изнутри контура (@DO); это НЕ «нарушения» и НЕ «зелено»")
            print(f"REF=direct | FAIL: 0/{len(targets)} измерено — контур недоступен")
            return 2
        for svc, lst in sorted(per_service.items()):
            svc_res = [r for r in results if SERVICE_BY_BC[str(r[0].get("bc"))] == svc]
            svc_pass = sum(1 for _op, code, _u in svc_res if code in (401, 403))
            print(f"   {svc:22} {svc_pass}/{len(svc_res)} дают 401 без токена")
        for b in bad:
            print(f"::error::apim-auth(service): {b}")
        for u in unknown:
            print(f"::warning::apim-auth(service): {u}")
        print(f"{'PASS' if rc == 0 else 'FAIL'}: сервисы напрямую — {passed}/{len(targets)} "
              f"операций дают 401 без токена")
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

    results = [(op, *check_one(args.base, op)) for op in targets]
    rc, bad, unknown = verdict(results)
    passed = len(targets) - len(bad) - len(unknown)

    if args.json:
        print(json.dumps({"base": args.base, "targets": len(targets), "passed": passed,
                          "failures": bad, "unresolved": unknown, "rc": rc}, ensure_ascii=False, indent=2))
    else:
        print(f"REF={args.base} | 401 без токена: {passed}/{len(targets)}")
        for b in bad:
            print(f"::error::apim-auth: {b}")
        for u in unknown:
            print(f"::warning::apim-auth: {u}")
        print(f"{'PASS' if rc == 0 else 'FAIL'}: {passed}/{len(targets)} операций дают 401 без токена")
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
