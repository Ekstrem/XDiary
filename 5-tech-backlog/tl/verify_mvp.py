#!/usr/bin/env python3
"""verify_mvp.py — одна команда: «можно ли пользоваться MVP прямо сейчас».

Проверяет ЧЕТЫРЕ вещи, каждая — с доказательством, а не с оценкой:
  1. прод-фронт опубликован и версия кэша в паре с версией приложения;
  2. прод-API жив и в APIM (health 200, защищённый путь 401, неизвестный 404);
  3. CORS: preflight и обычный POST с Origin отдают Access-Control-Allow-Origin
     (без этого браузер не даст приложению работать — это была причина «Нет связи с сервером»);
  4. images.lock не отстал от git (иначе деплой катит старые образы).

Запуск:  python 5-tech-backlog/tl/verify_mvp.py
Код возврата: 0 — всё зелёное; 1 — есть красное (печатается, что именно).
"""
import json
import re
import subprocess
import sys
import urllib.error
import urllib.request

PROD = "https://xdiary-vps1.taile0b6ee.ts.net"
DEV = "https://xdiary-vps2.taile0b6ee.ts.net"
PAGES = "https://ekstrem.github.io/XDiary/"
ORIGIN = "https://ekstrem.github.io"
TIMEOUT = 20


def http(url, method="GET", headers=None):
    h = {"User-Agent": "verify-mvp/1"}
    if headers:
        h.update(headers)
    req = urllib.request.Request(url, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
            return r.status, dict(r.headers), r.read(200000).decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, dict(e.headers), ""
    except Exception as e:  # noqa: BLE001
        return None, {}, f"{type(e).__name__}: {e}"


def hdr(headers, name):
    for k, v in headers.items():
        if k.lower() == name:
            return v
    return None


def check(name, ok, detail):
    print(("  PASS  " if ok else "  FAIL  ") + name + " — " + detail)
    return ok


def main():
    results = []

    print("[1/4] прод-фронт")
    st, _, app = http(PAGES + "js/app.js")
    ver = re.search(r'APP_VERSION\s*=\s*"([\d.]+)"', app)
    st2, _, sw = http(PAGES + "sw.js")
    cache = re.search(r'CACHE\s*=\s*"([^"]+)"', sw)
    app_ver = ver.group(1) if ver else None
    sw_cache = cache.group(1) if cache else None
    results.append(check("app.js отдаётся", st == 200, "HTTP %s" % st))
    results.append(check("версия известна", bool(app_ver), "APP_VERSION=%s" % app_ver))
    digits = app_ver.replace(".", "").lstrip("0") if app_ver else ""
    paired = bool(digits and sw_cache and sw_cache.replace("-v", "").replace("xdiary", "").lstrip("v") == digits)
    results.append(check("версия и кэш в паре", paired, "%s ↔ %s" % (app_ver, sw_cache)))

    print("[2/4] прод-API и APIM")
    st, h, _ = http(PROD + "/health")
    results.append(check("/health = 200", st == 200, "HTTP %s" % st))
    st, h, _ = http(PROD + "/api/v1/catalog")
    results.append(check("/api/v1/catalog = 401", st == 401, "HTTP %s" % st))
    via = hdr(h, "x-gravitee")
    print("  ИНФО  x-gravitee=%s (заголовок может отсутствовать на 401; APIM проверяем по неизвестному пути)" % via)
    st, _, _ = http(PROD + "/zzz-verify-probe")
    results.append(check("неизвестный путь = 404", st == 404, "HTTP %s" % st))

    print("[3/4] CORS (без него браузер не работает)")
    pre = http(PROD + "/api/v1/clients", "OPTIONS", {
        "Origin": ORIGIN,
        "Access-Control-Request-Method": "POST",
        "Access-Control-Request-Headers": "content-type",
    })
    pre_code, pre_h = pre[0], pre[1]
    acao = hdr(pre_h, "access-control-allow-origin")
    results.append(check("preflight OPTIONS 2xx", isinstance(pre_code, int) and 200 <= pre_code < 300, "HTTP %s" % pre_code))
    results.append(check("preflight отдаёт ACAO", bool(acao), "ACAO=%s" % acao))
    post = http(PROD + "/api/v1/clients", "POST", {"Origin": ORIGIN, "Content-Type": "application/json"})
    results.append(check("POST с Origin отдаёт ACAO", bool(hdr(post[1], "access-control-allow-origin")),
                         "ACAO=%s (HTTP %s; 400 = телефон уже есть, это не дефект)"
                         % (hdr(post[1], "access-control-allow-origin"), post[0])))

    print("[4/4] образы: images.lock")
    rc, out = 0, ""
    try:
        p = subprocess.run([sys.executable, "tools/gen_images_lock.py", "--check"],
                           capture_output=True, text=True)
        rc, out = p.returncode, (p.stdout + p.stderr).strip().splitlines()
    except Exception as e:  # noqa: BLE001
        rc, out = 2, [str(e)]
    results.append(check("images.lock соответствует git", rc == 0, (out[-1] if out else "rc=%s" % rc)))

    ok = sum(1 for r in results if r)
    print("\nИТОГ: зелёных %d из %d" % (ok, len(results)))
    if ok != len(results):
        print("Красные пункты — это НЕ «мелочи»: каждый из них блокирует пользователя.")
        return 1
    print("MVP доступен пользователю по всем проверенным признакам.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
