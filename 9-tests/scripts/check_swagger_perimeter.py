"""check_swagger_perimeter.py — описание API не должно быть доступно вне периметра.

Критерий @AR `chat/ar.md` `[2026-11-05 18:12]`: `/swagger*` — часть дрейф-сверки, и проверка
«снаружи `401`/`404`, изнутри `200`» ловит самый дешёвый класс дефекта: **спека открыта миру
при формально закрытом API**. Снаружи `200` — это `rc=1` с текстом «описание API доступно
вне периметра», а не предупреждение.

Три условия @AR, все машинные:

1. снаружи `/swagger*` **не** отвечает `200` (deny на шлюзе, а не «порт не опубликован случайно»);
2. изнутри периметра — `200` и валидный JSON;
3. нарушение (1) — `rc=1`, а не тишина.

Коды возврата: `0` — измерено и в норме; `1` — измерено и нарушено; `2` — не измерено
(нет адресов, сеть недоступна, ответ не разобрать). `rc=2` не «плохо» и не «зелено».

Использование:
    python 9-tests/scripts/check_swagger_perimeter.py \
        --public-base https://<публичный вход> --internal-base http://<внутри контура>
    python 9-tests/scripts/check_swagger_perimeter.py --selftest
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
except Exception:
    pass

PATHS = ("/swagger/v1/swagger.json", "/swagger/index.html", "/swagger")
PUBLIC_ALLOWED = (401, 403, 404)


def probe(url, timeout=5.0):
    """(код, тело) или (None, причина). Не бросает — «не измерено» это тоже результат."""
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.getcode(), resp.read(200_000).decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, ""
    except Exception as e:  # noqa: BLE001 — сеть: любая ошибка = не измерено
        return None, f"{type(e).__name__}: {e}"


def evaluate(public, internal):
    """public/internal — {путь: (код, тело)}. Возвращает (issues, unmeasured, checked)."""
    issues, unmeasured, checked = [], [], 0
    for path in PATHS:
        code, body = public.get(path, (None, "не запрашивался"))
        if code is None:
            unmeasured.append(f"снаружи {path}: {body}")
        else:
            checked += 1
            if code == 200:
                issues.append(f"описание API доступно вне периметра: снаружи {path} → 200 "
                              f"(ожидали 401/403/404)")
            elif code not in PUBLIC_ALLOWED:
                issues.append(f"снаружи {path} → {code}: ожидали 401/403/404 или «не измерено»")

    code, body = internal.get(PATHS[0], (None, "не запрашивался"))
    if code is None:
        unmeasured.append(f"изнутри {PATHS[0]}: {body}")
    else:
        checked += 1
        if code != 200:
            issues.append(f"изнутри {PATHS[0]} → {code}: описание API должно быть доступно "
                          f"внутри периметра")
        else:
            try:
                json.loads(body)
            except ValueError:
                issues.append(f"изнутри {PATHS[0]} → 200, но тело не JSON — сверять нечего")
    return issues, unmeasured, checked


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--public-base", help="публичный адрес (вне периметра)")
    ap.add_argument("--internal-base", help="адрес внутри периметра (podman/локально)")
    ap.add_argument("--timeout", type=float, default=5.0)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if not (a.public_base or a.internal_base):
        print("::error::check_swagger_perimeter: не задан ни --public-base, ни --internal-base "
              "— измерять нечего (rc=2)")
        return 2

    public, internal = {}, {}
    if a.public_base:
        for p in PATHS:
            public[p] = probe(a.public_base.rstrip("/") + p, a.timeout)
    if a.internal_base:
        internal[PATHS[0]] = probe(a.internal_base.rstrip("/") + PATHS[0], a.timeout)

    issues, unmeasured, checked = evaluate(public, internal)
    for msg in issues:
        print(f"::error::check_swagger_perimeter: {msg}")
    for msg in unmeasured:
        print(f"::warning::check_swagger_perimeter: не измерено: {msg}")
    print(f"квитанция: проверено {checked} из {len(PATHS) + 1} | "
          f"нарушено: {len(issues)} | не засчитано: {len(unmeasured)}")
    if issues:
        return 1
    return 0 if not unmeasured else 2


def selftest():
    """Красный контроль на локальном сервере: каждый кейс падает по своей причине."""
    ok = total = 0

    def case(name, public, internal, want_rc, must=(), must_not=()):
        nonlocal ok, total
        total += 1
        issues, unmeasured, checked = evaluate(public, internal)
        text = "\n".join(issues + unmeasured) + (
            f"\nквитанция: проверено {checked} из {len(PATHS) + 1} | "
            f"нарушено: {len(issues)} | не засчитано: {len(unmeasured)}")
        rc = 1 if issues else (0 if not unmeasured else 2)
        miss = [m for m in must if m not in text]
        bad = [m for m in must_not if m in text]
        if rc == want_rc and not miss and not bad:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, лишнее {bad}")

    j = '{"openapi": "3.0.0"}'
    deny = {p: (404, "") for p in PATHS}
    case("норма: снаружи 404, изнутри JSON", deny, {PATHS[0]: (200, j)}, 0, ["проверено"])
    case("спека открыта миру: снаружи 200", {**deny, PATHS[0]: (200, j)},
         {PATHS[0]: (200, j)}, 1, ["описание API доступно вне периметра"])
    case("снаружи 401 — норма", {p: (401, "") for p in PATHS}, {PATHS[0]: (200, j)}, 0)
    case("снаружи 500 — не норма", {**deny, PATHS[0]: (500, "")},
         {PATHS[0]: (200, j)}, 1, ["ожидали 401/403/404"])
    case("изнутри 404 — описание недоступно", deny, {PATHS[0]: (404, "")}, 1,
         ["должно быть доступно внутри периметра"])
    case("изнутри 200, но не JSON", deny, {PATHS[0]: (200, "<html>")}, 1, ["тело не JSON"])
    case("ничего не измерено", {}, {}, 2, ["не запрашивался"], ["описание API доступно"])

    # живой сервер: ДВА разных (снаружи всё закрыто, изнутри JSON) — иначе кейс бессмыслен
    class Closed(http.server.BaseHTTPRequestHandler):
        def do_GET(self):  # noqa: N802
            self.send_response(404)
            self.end_headers()

        def log_message(self, *a):
            pass

    class Open(http.server.BaseHTTPRequestHandler):
        def do_GET(self):  # noqa: N802
            if self.path == PATHS[0]:
                body = j.encode()
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
    internal = {PATHS[0]: probe(intr + PATHS[0])}
    issues, unmeasured, checked = evaluate(got, internal)
    if not issues and not unmeasured and checked == len(PATHS) + 1:
        ok += 1
    else:
        print(f"  [FAIL] живой сервер: issues={issues}, unmeasured={unmeasured}, checked={checked}")
    srv_pub.shutdown()
    srv_int.shutdown()

    # кейс на ТЕКСТ: квитанция сходится и не содержит None
    total += 1
    issues, unmeasured, checked = evaluate({p: (404, "") for p in PATHS}, {PATHS[0]: (200, j)})
    line = (f"квитанция: проверено {checked} из {len(PATHS) + 1} | "
            f"нарушено: {len(issues)} | не засчитано: {len(unmeasured)}")
    if "проверено 4 из 4" in line and "None" not in line and len(issues) + len(unmeasured) == 0:
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_swagger_perimeter --selftest: {ok}/{total} "
          f"(спека вне периметра = rc=1, каждый кейс краснеет своей причиной)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
