#!/usr/bin/env python
"""Гейт CORS на публичном входе (DO-106).

Зачем: браузер не пускал пользователя в прод, а все наши гейты были зелёными —
потому что они проверяли **коды без `Origin`** и не делали preflight. Класс дефекта:
`OPTIONS` с `Origin` → 401 и в ответах нет `Access-Control-Allow-Origin`, при этом
`POST` без `Origin` → 200. Такой дефект не виден ни `check_public_entry`, ни
`check_apim_auth` — они не cross-origin.

Что проверяет:
  1. `OPTIONS <base>/api/v1/clients` с `Origin` и `Access-Control-Request-Method: POST`
     → ждём 2xx и `Access-Control-Allow-Origin`;
  2. `POST <base>/api/v1/clients` с `Origin` → ждём `Access-Control-Allow-Origin`
     (код может быть 200 или 4xx — важно наличие заголовка);
  3. `GET <base>/health` → 200 (контроль, что вход жив, а не просто «всё 502»).

`rc=0` — CORS есть; `rc=1` — дефект (preflight не 2xx или нет ACAO); `rc=2` — не измерили
(сеть/таймаут): «не измерено» не маскируется под «всё хорошо».

Использование:
    python tools/check_cors_entry.py --base https://xdiary-vps1.taile0b6ee.ts.net
    python tools/check_cors_entry.py --base ... --origin https://ekstrem.github.io
    python tools/check_cors_entry.py --selftest
"""
import argparse
import json
import sys
import urllib.error
import urllib.request

DEFAULT_ORIGIN = "https://ekstrem.github.io"
CLIENTS_PATH = "/api/v1/clients"
HEALTH_PATH = "/health"


def _req(url, method="GET", headers=None, body=None, timeout=15):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, method=method, data=data, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, {k.lower(): v for k, v in r.headers.items()}
    except urllib.error.HTTPError as ex:
        return ex.code, {k.lower(): v for k, v in ex.headers.items()}
    except Exception:
        return None, {}


def classify_preflight(status, headers):
    """'ok' | 'no_acao' | 'bad_status' | 'unmeasured'"""
    if status is None:
        return "unmeasured"
    if not (200 <= status < 300):
        return "bad_status"
    if not headers.get("access-control-allow-origin"):
        return "no_acao"
    return "ok"


def classify_simple(status, headers):
    """Для POST/POST-подобного: код не важен, важен ACAO. None -> unmeasured."""
    if status is None:
        return "unmeasured"
    if not headers.get("access-control-allow-origin"):
        return "no_acao"
    return "ok"


def check(base, origin=DEFAULT_ORIGIN, timeout=15):
    base = base.rstrip("/")
    pre_status, pre_headers = _req(
        base + CLIENTS_PATH, method="OPTIONS",
        headers={"Origin": origin,
                 "Access-Control-Request-Method": "POST",
                 "Access-Control-Request-Headers": "content-type"}, timeout=timeout)
    post_status, post_headers = _req(
        base + CLIENTS_PATH, method="POST",
        headers={"Origin": origin, "Content-Type": "application/json"},
        body={"phone": "+79000000000"}, timeout=timeout)
    h_status, _ = _req(base + HEALTH_PATH, timeout=timeout)

    pre = classify_preflight(pre_status, pre_headers)
    simple = classify_simple(post_status, post_headers)
    print("check_cors_entry: base=%s origin=%s" % (base, origin))
    print("  OPTIONS %s -> HTTP=%s | ACAO=%s | %s"
          % (CLIENTS_PATH, pre_status, pre_headers.get("access-control-allow-origin", "нет"), pre))
    print("  POST    %s -> HTTP=%s | ACAO=%s | %s"
          % (CLIENTS_PATH, post_status, post_headers.get("access-control-allow-origin", "нет"), simple))
    print("  GET     %s -> HTTP=%s (контроль входа)" % (HEALTH_PATH, h_status))

    if pre == "unmeasured" and simple == "unmeasured":
        print("::error::check_cors_entry: НЕ ИЗМЕРЕНО — вход не отвечает")
        return 2
    bad = []
    if pre != "ok":
        bad.append("preflight: %s" % pre)
    if simple != "ok":
        bad.append("ответ без ACAO: %s" % simple)
    if h_status != 200:
        bad.append("контрольный /health = %s" % h_status)
    if bad:
        for b in bad:
            print("::error::check_cors_entry: %s" % b)
        return 1
    print("check_cors_entry: CORS в порядке")
    return 0


def selftest():
    ok = total = 0

    def case(name, got, want):
        nonlocal ok, total
        total += 1
        good = got == want
        ok += 1 if good else 0
        print("  [%s] %s -> %s" % ("OK" if good else "FAIL", name, got))

    acao = {"access-control-allow-origin": DEFAULT_ORIGIN}
    case("preflight 204 + ACAO", classify_preflight(204, acao), "ok")
    case("preflight 200 + ACAO", classify_preflight(200, acao), "ok")
    case("preflight 401 (было на проде)", classify_preflight(401, {}), "bad_status")
    case("preflight 204 без ACAO", classify_preflight(204, {}), "no_acao")
    case("preflight таймаут", classify_preflight(None, {}), "unmeasured")
    case("POST 200 + ACAO", classify_simple(200, acao), "ok")
    case("POST 400 без ACAO", classify_simple(400, {}), "no_acao")
    case("POST таймаут", classify_simple(None, {}), "unmeasured")
    print("check_cors_entry --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="публичный вход, например https://xdiary-vps1.taile0b6ee.ts.net")
    ap.add_argument("--origin", default=DEFAULT_ORIGIN)
    ap.add_argument("--timeout", type=int, default=15)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    if not a.base:
        print("нужен --base (или --selftest)", file=sys.stderr)
        return 2
    return check(a.base, origin=a.origin, timeout=a.timeout)


if __name__ == "__main__":
    sys.exit(main())
