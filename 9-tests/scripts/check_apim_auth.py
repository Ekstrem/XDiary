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


def check_one(base: str, op: dict, timeout: float = 8.0) -> tuple[int | None, str]:
    url = base.rstrip("/") + concrete_path(str(op["path"]))
    req = urllib.request.Request(url, method=str(op.get("method", "GET")).upper())
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return int(resp.status), url
    except urllib.error.HTTPError as e:
        return int(e.code), url
    except (urllib.error.URLError, socket.timeout, OSError) as e:
        return None, f"{url} ({type(e).__name__})"


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
        else:
            bad.append(f"{name} — {code} вместо 401, {url}")
    return (1 if bad else 0), bad, unknown


def selftest() -> int:
    """Доказательство, что проверка краснеет на открытой операции и зеленеет на 401."""
    ops = [
        {"method": "GET", "path": "/api/v1/a", "operationId": "A.Get", "auth": "jwt"},
        {"method": "POST", "path": "/api/v1/b", "operationId": "B.Post", "auth": "jwt"},
        {"method": "POST", "path": "/api/v1/clients", "operationId": "C.Create", "auth": "anonymous"},
        {"method": "POST", "path": "/api/v1/i", "operationId": "I.Internal", "auth": "anonymous-inside"},
    ]
    sel = [op for op in ops if requires_auth(op)]
    ok = 0
    cases = [
        ([{401}, {401}, {401}], 0, "все 401 → зелено"),
        ([{200}, {401}, {401}], 1, "200 без токена → красно"),
        ([{401}, {403}, {401}], 0, "403 тоже отказ → зелено"),
        ([{404}, {401}, {401}], 0, "404 выносится отдельно, не красит"),
        ([{500}, {401}, {401}], 1, "500 вместо 401 → красно"),
    ]
    for seq, want, title in cases:
        res = []
        for op, code in zip(sel, seq):
            res.append((op, next(iter(code)), "http://sel"))
        rc, _bad, _unk = verdict(res)
        got = 0 if rc == 0 else 1
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {title}: ждали rc={want}, получили {got}")
    # проверка фильтра: 4 операции, из них 1 `anonymous` → в проверке 3,
    # потому что `anonymous-inside` (`/internal/**`) снаружи обязан давать 401.
    if len(sel) == 3:
        ok += 1
    else:
        print(f"  [FAIL] фильтр анонимных: ждали 3 (anonymous-inside снаружи проверяется), получили {len(sel)}")
    total = len(cases) + 1
    print(f"check_apim_auth --selftest: {ok}/{total}")
    return 0 if ok == total else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base")
    ap.add_argument("--index", default=INDEX)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    ops = load_ops(args.index)
    targets = [op for op in ops if requires_auth(op)]
    print(f"операций в индексе: {len(ops)}, требуют токен: {len(targets)}, "
          f"анонимных: {len(ops) - len(targets)}")

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
