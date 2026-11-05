"""check_swagger_perimeter.py — описание API не должно быть доступно вне периметра.

Критерий @AR `chat/ar.md` `[2026-11-05 18:12]`: `/swagger*` — часть дрейф-сверки, и проверка
«снаружи `401`/`404`, изнутри `200`» ловит самый дешёвый класс дефекта: **спека открыта миру
при формально закрытом API**. Снаружи `200` — `rc=1` с текстом «описание API доступно вне периметра».

Правило @DO `chat/do.md` `[2026-11-05 18:5x]` + @TL `chat/tl.md` `[18:58]`: **код 200 сам по себе
не значит «спека доступна»**. `200` засчитывается как «спека открыта» только если тело —
действительно спецификация (JSON с ключом `openapi`/`swagger`, либо YAML/JSON по `Content-Type`).
`200` с телом-заглушкой (например 71 байт «API-only (dev stand)», `Server: Caddy`) — это
**не нарушение**, но и **не доказательство**: такой ответ уходит в «не засчитано» (`rc=2`).

Шаг (2) порядка @TL `[18:44]`: включили `Swagger__Enabled=true` на dev — мерим периметр; только
после этого шаг (3) — ре-снятие `api-index.yaml`/спек. Внутренних сервисов несколько, поэтому
`--internal-base` можно указывать **несколько раз**.

Коды возврата: `0` — измерено и в норме; `1` — измерено и нарушено; `2` — не измерено.
"""

import argparse
import http.server
import json
import sys
import threading
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001 — не критично для работы гейта
    pass

PATHS = ("/swagger/v1/swagger.json", "/swagger/index.html", "/swagger")
SPEC = PATHS[0]
PUBLIC_ALLOWED = (401, 403, 404)
SPEC_KEYS = ("openapi", "swagger")


def probe(url, timeout=5.0):
    """(код, тело, заголовки) или (None, причина, {}). Не бросает — «не измерено» тоже результат."""
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.getcode(), resp.read(200_000).decode("utf-8", "replace"), dict(resp.headers)
    except urllib.error.HTTPError as e:
        return e.code, "", dict(e.headers or {})
    except Exception as e:  # noqa: BLE001 — сеть: любая ошибка = не измерено
        return None, f"{type(e).__name__}: {e}", {}


def is_spec(body, headers):
    """Похоже ли тело на спецификацию OpenAPI (@DO: проверять тело, а не только код)."""
    text = (body or "").strip()
    if not text:
        return False
    if text.startswith("{"):
        try:
            doc = json.loads(text)
        except ValueError:
            return False
        return isinstance(doc, dict) and any(k in doc for k in SPEC_KEYS)
    ctype = (headers.get("Content-Type") or headers.get("content-type") or "").lower()
    if "yaml" in ctype or "json" in ctype:
        return any(k in text[:2000] for k in SPEC_KEYS)
    return False


def evaluate(public, internal):
    """public — {путь: (код, тело, заголовки)}; internal — {адрес: (код, тело, заголовки)}.

    Возвращает (issues, unmeasured, checked).
    """
    issues, unmeasured, checked = [], [], 0
    for path in PATHS:
        code, body, headers = public.get(path, (None, "не запрашивался", {}))
        if code is None:
            unmeasured.append(f"снаружи {path}: {body}")
            continue
        if code == 200:
            if is_spec(body, headers):
                checked += 1
                issues.append(f"описание API доступно вне периметра: снаружи {path} → 200 "
                              f"и тело — спецификация (ожидали 401/403/404)")
            else:
                srv = headers.get("Server") or headers.get("server") or "?"
                unmeasured.append(f"снаружи {path} → 200, но тело НЕ спецификация "
                                  f"({len(body or '')} байт, Server: {srv}) — не засчитано: "
                                  f"код 200 сам по себе не значит «спека доступна»")
            continue
        checked += 1
        if code not in PUBLIC_ALLOWED:
            issues.append(f"снаружи {path} → {code}: ожидали 401/403/404 или «не измерено»")

    if not internal:
        unmeasured.append("изнутри: не задан ни один --internal-base")
    for base, (code, body, headers) in internal.items():
        if code is None:
            unmeasured.append(f"изнутри {base}{SPEC}: {body}")
            continue
        checked += 1
        if code != 200:
            issues.append(f"изнутри {base}{SPEC} → {code}: описание API должно быть доступно "
                          f"внутри периметра")
            continue
        if not is_spec(body, headers):
            issues.append(f"изнутри {base}{SPEC} → 200, но тело не спецификация — сверять нечего")
    return issues, unmeasured, checked


def receipt_line(checked, issues, unmeasured, public_base, internal_bases, when):
    """Квитанция с провенансом замера (@AR `[18:25]`): адреса и время UTC."""
    bases = ", ".join(internal_bases) if internal_bases else "НЕ УКАЗАН"
    total = len(PATHS) + max(1, len(internal_bases))
    return (f"квитанция: проверено {checked} из {total} | "
            f"нарушено: {len(issues)} | не засчитано: {len(unmeasured)} | "
            f"public-base: {public_base or 'НЕ УКАЗАН'} | "
            f"internal-base: {bases} | измерено: {when}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--public-base", help="публичный адрес (вне периметра)")
    ap.add_argument("--internal-base", action="append", default=None,
                    help="адрес внутри периметра; можно указывать несколько раз")
    ap.add_argument("--timeout", type=float, default=5.0)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    internal_bases = a.internal_base or []
    if not (a.public_base or internal_bases):
        print("::error::check_swagger_perimeter: не задан ни --public-base, ни --internal-base "
              "— измерять нечего (rc=2)")
        return 2

    public, internal = {}, {}
    if a.public_base:
        for p in PATHS:
            public[p] = probe(a.public_base.rstrip("/") + p, a.timeout)
    for base in internal_bases:
        internal[base] = probe(base.rstrip("/") + SPEC, a.timeout)

    issues, unmeasured, checked = evaluate(public, internal)
    for msg in issues:
        print(f"::error::check_swagger_perimeter: {msg}")
    for msg in unmeasured:
        print(f"::warning::check_swagger_perimeter: не засчитано: {msg}")
    from datetime import datetime as _dt, timezone as _tz
    when = _dt.now(_tz.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    print(receipt_line(checked, issues, unmeasured, a.public_base, internal_bases, when))
    if issues:
        return 1
    return 0 if not unmeasured else 2


def selftest():
    """Красный контроль: код и ТЕЛО проверяются отдельно."""
    ok = total = 0
    spec_body = '{"openapi": "3.0.0", "paths": {}}'
    stub = "API-only (dev stand)"
    jhead = {"Content-Type": "application/json", "Server": "Kestrel"}
    chead = {"Content-Type": "text/plain", "Server": "Caddy"}
    deny = {p: (404, "", {}) for p in PATHS}

    def case(name, public, internal, want_rc, must=(), must_not=()):
        nonlocal ok, total
        total += 1
        issues, unmeasured, checked = evaluate(public, internal)
        text = "\n".join(issues + unmeasured) + "\n" + receipt_line(
            checked, issues, unmeasured, "https://pub.example", list(internal), "2026-11-05T18:58:00Z")
        rc = 1 if issues else (0 if not unmeasured else 2)
        miss = [m for m in must if m not in text]
        bad = [m for m in must_not if m in text]
        if rc == want_rc and not miss and not bad:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, лишнее {bad}")

    case("норма: снаружи 404, изнутри спека", deny, {"http://partners:8080": (200, spec_body, jhead)}, 0,
         ["проверено 4 из 4", "public-base: https://pub.example"])
    case("снаружи 200 И тело — спецификация → нарушение", {**deny, SPEC: (200, spec_body, jhead)},
         {"http://p:8080": (200, spec_body, jhead)}, 1, ["доступно вне периметра"])
    case("снаружи 200, но заглушка (не спека) → НЕ нарушение, не засчитано",
         {**deny, SPEC: (200, stub, chead)}, {"http://p:8080": (200, spec_body, jhead)}, 2,
         ["тело НЕ спецификация", "не засчитано"], ["доступно вне периметра"])
    case("снаружи 401 — норма", {p: (401, "", {}) for p in PATHS},
         {"http://p:8080": (200, spec_body, jhead)}, 0)
    case("снаружи 500 — не норма", {**deny, SPEC: (500, "", {})},
         {"http://p:8080": (200, spec_body, jhead)}, 1, ["ожидали 401/403/404"])
    case("изнутри 404 — описание недоступно", deny, {"http://p:8080": (404, "", {})}, 1,
         ["должно быть доступно внутри периметра"])
    case("изнутри 200, но заглушка", deny, {"http://p:8080": (200, stub, chead)}, 1,
         ["тело не спецификация"])
    case("изнутри 200, но не JSON", deny, {"http://p:8080": (200, "<html>", chead)}, 1,
         ["тело не спецификация"])
    case("ничего не измерено", {}, {}, 2, ["не задан ни один --internal-base"],
         ["доступно вне периметра"])
    case("два сервиса, второй 500", deny,
         {"http://a:8080": (200, spec_body, jhead), "http://b:8080": (500, "", {})}, 1,
         ["http://b:8080/swagger/v1/swagger.json → 500"])
    case("два сервиса в квитанции", deny,
         {"http://a:8080": (200, spec_body, jhead), "http://b:8080": (200, spec_body, jhead)}, 0,
         ["internal-base: http://a:8080, http://b:8080", "проверено 5 из 5"])

    class Closed(http.server.BaseHTTPRequestHandler):
        def do_GET(self):  # noqa: N802
            self.send_response(404)
            self.end_headers()

        def log_message(self, *a):
            pass

    class Open(http.server.BaseHTTPRequestHandler):
        def do_GET(self):  # noqa: N802
            if self.path == SPEC:
                body = spec_body.encode()
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            else:
                self.send_response(404)
                self.end_headers()

        def log_message(self, *a):
            pass

    srv_pub = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Closed)
    srv_int = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Open)
    for s_ in (srv_pub, srv_int):
        threading.Thread(target=s_.serve_forever, daemon=True).start()
    pub = f"http://127.0.0.1:{srv_pub.server_address[1]}"
    intr = f"http://127.0.0.1:{srv_int.server_address[1]}"
    total += 1
    got = {p_: probe(pub + p_) for p_ in PATHS}
    internal = {intr: probe(intr + SPEC)}
    issues, unmeasured, checked = evaluate(got, internal)
    if not issues and not unmeasured and checked == len(PATHS) + 1:
        ok += 1
    else:
        print(f"  [FAIL] живой сервер: issues={issues}, unmeasured={unmeasured}, checked={checked}")
    srv_pub.shutdown()
    srv_int.shutdown()

    total += 1
    issues, unmeasured, checked = evaluate(deny, {"http://p:8080": (200, spec_body, jhead)})
    line = receipt_line(checked, issues, unmeasured, "https://pub.example",
                        ["http://p:8080"], "2026-11-05T18:58:00Z")
    if ("проверено 4 из 4" in line and "internal-base: http://p:8080" in line
            and "измерено: 2026-11-05T18:58:00Z" in line and "None" not in line):
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_swagger_perimeter --selftest: {ok}/{total} "
          f"(200 со заглушкой = не засчитано, 200 со спекой = нарушение)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
