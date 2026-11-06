"""check_5xx_correlation.py — любой `5xx` обязан нести `X-Correlation-Id`.

Норма @AR (`2026-11-06 00:26`, `api-index.yaml` → `trace_rule`): тело `5xx` намеренно без подробностей (не течём
внутренностями), но **корреляция обязательна**. Причина — реальный случай: `500` на `POST /partners/{id}/remove` и
`/link-client` отдавал **пустое тело и ни одного идентификатора**, поэтому «запрос → строка в логе»
восстанавливался вручную целый вечер.

Гейт делает запросы к стенду и проверяет: **если ответ `5xx`, в нём есть `X-Correlation-Id`**. Отсутствует ⇒
`rc=1` с указанием операции. `4xx` не проверяются: там корреляция не требуется нормой.

Коды: `0` — все `5xx` несут заголовок (или `5xx` не встретилось); `1` — есть `5xx` без заголовка; `2` — не измерено
(нет `--base`/`--token`, стенд недоступен).
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

CORRELATION_HEADERS = ("x-correlation-id", "x-correlationid", "x-request-id")


def correlation(headers):
    for name, value in (headers or {}).items():
        if name.lower() in CORRELATION_HEADERS:
            return f"{name}: {value[:8]}…"
    return None


def evaluate(results):
    """results: [{'op','status','correlation'}] → (issues, stats)."""
    issues = []
    five = [r for r in results if isinstance(r.get("status"), int) and r["status"] >= 500]
    for r in five:
        if not r.get("correlation"):
            issues.append(f"{r['op']} → {r['status']} без `X-Correlation-Id` "
                          f"(тело и идентификатор пусты: «запрос → лог» не восстановить)")
    stats = {"probes": len(results), "5xx": len(five),
             "5xx_with_id": sum(1 for r in five if r.get("correlation"))}
    return issues, stats


def receipt(stats, issues):
    return (f"квитанция: запросов {stats['probes']} | из них 5xx {stats['5xx']} | "
            f"с X-Correlation-Id {stats['5xx_with_id']} | нарушено: {len(issues)}")


def call(base, op, method, path, payload=None, token=None, timeout=25):
    headers = {"User-Agent": "xdiary-5xx", "Origin": "https://ekstrem.github.io",
               "Accept": "application/json"}
    data = None
    if payload is not None:
        data = json.dumps(payload).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(base.rstrip("/") + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return {"op": op, "status": r.status, "correlation": correlation(dict(r.headers))}
    except urllib.error.HTTPError as e:
        return {"op": op, "status": e.code, "correlation": correlation(dict(e.headers))}
    except (urllib.error.URLError, OSError) as e:
        return {"op": op, "status": None, "correlation": None, "error": str(e)}


def probes_for(token, client_id, partner_id):
    """Список проб. `5xx` ожидается там, где сейчас реальный дефект (команды партнёра)."""
    p = [
        ("GET /health", "GET", "/health", None),
        ("GET /api/v1/partners (anon)", "GET", "/api/v1/partners", None),
    ]
    if not token:
        return p
    p += [
        ("GET /api/v1/partners", "GET", "/api/v1/partners", None),
        ("GET /api/v1/journal/records", "GET", "/api/v1/journal/records", None),
    ]
    if partner_id:
        p += [
            ("POST /partners/{id}/name", "POST", f"/api/v1/partners/{partner_id}/name", {"name": "corr-probe"}),
            ("POST /partners/{id}/remove", "POST", f"/api/v1/partners/{partner_id}/remove", {}),
        ]
    if client_id:
        p += [("POST /partners/{id}/link-client", "POST",
               f"/api/v1/partners/{partner_id}/link-client", {"clientId": client_id})]
    return p


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API (dev/прод)")
    ap.add_argument("--token", help="JWT (или XDIARY_TOKEN); без него проверяются только анонимные ручки")
    ap.add_argument("--client-id", help="clientId для link-client")
    ap.add_argument("--partner-id", help="partnerId для команд партнёра")
    ap.add_argument("--require-5xx", action="store_true",
                    help="если ни одного 5xx не встретилось — rc=2 (не измерено), а не «зелено на пустоте»")
    ap.add_argument("--status", type=int, help="офлайн: подставить статус")
    ap.add_argument("--has-correlation", choices=("yes", "no"), help="офлайн: есть ли заголовок")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.status is not None:
        results = [{"op": "офлайн", "status": a.status,
                    "correlation": ("x-correlation-id: 12345678…" if a.has_correlation == "yes" else None)}]
    else:
        if not a.base:
            print("::error::check_5xx_correlation: нет --base — не измерено (rc=2)")
            return 2
        token = a.token or os.environ.get("XDIARY_TOKEN")
        results = []
        for op, method, path, payload in probes_for(token, a.client_id, a.partner_id):
            r = call(a.base, op, method, path, payload, token)
            if r.get("status") is None:
                print(f"::error::check_5xx_correlation: {op} — стенд недоступен: {r.get('error')} — не измерено (rc=2)")
                return 2
            results.append(r)
            tag = "5xx" if r["status"] >= 500 else ""
            print(f"   {op:34} → {r['status']:>3} {tag:4} корреляция: {r.get('correlation') or '—'}")

    issues, stats = evaluate(results)
    for m in issues:
        print(f"::error::check_5xx_correlation: {m}")
    print(receipt(stats, issues))
    if issues:
        return 1
    if a.require_5xx and stats["5xx"] == 0:
        print("::error::check_5xx_correlation: ни одного 5xx не встретилось — заголовок не проверялся, "
              "это «не измерено», а не «зелено» (rc=2)")
        return 2
    return 0


def selftest():
    ok = total = 0

    def case(name, results, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(results)
        text = "\n".join(issues) + "\n" + receipt(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    case("5xx без заголовка — красный",
         [{"op": "POST /partners/{id}/remove", "status": 500, "correlation": None}], 1,
         ["без `X-Correlation-Id`", "нарушено: 1"])

    case("5xx с заголовком — зелёный",
         [{"op": "POST /partners/{id}/remove", "status": 500, "correlation": "x-correlation-id: 1…"}], 0,
         ["с X-Correlation-Id 1", "нарушено: 0"])

    case("4xx не требует корреляции",
         [{"op": "POST /clients", "status": 400, "correlation": None},
          {"op": "GET /partners", "status": 401, "correlation": None}], 0, ["из них 5xx 0"])

    case("смесь: один 5xx с id, один без — красный",
         [{"op": "a", "status": 500, "correlation": "x-request-id: 2…"},
          {"op": "b", "status": 503, "correlation": None}], 1, ["b → 503"])

    case("5xx не встретилось — зелёный, но в квитанции это видно",
         [{"op": "GET /health", "status": 200, "correlation": None}], 0, ["из них 5xx 0"])

    total += 1
    line = receipt({"probes": 6, "5xx": 2, "5xx_with_id": 2}, [])
    if "запросов 6" in line and "5xx 2" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_5xx_correlation --selftest: {ok}/{total} "
          f"(5xx без X-Correlation-Id = rc=1: иначе «запрос → лог» не восстановить)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
