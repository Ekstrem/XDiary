"""check_e2e_local_stack.py — локальный стек набора проверяется поведением.

Зачем: `npm run test:local` (единственный способ прогнать браузер без VPS и секретов) опирается на
`serve.js` (статика `8-front`) и на репортёр `reporters/notice.js` (аннотации `::error::` в CI).
Оба до сих пор проверялись только глазами: если `serve.js` начнёт отдавать 200 на `..` или не тот
`Content-Type`, локальный прогон упадёт загадочно; если репортёр перестанет печатать шаги, разбор
падения в CI снова станет «просто упало».

Проверки:
1. `serve.js` отдаёт `/` как `text/html`, `/js/app.js` как `text/javascript`, неизвестный путь — 404,
   попытку выйти за корень (`/../`) — **не 200**;
2. `reporters/notice.js` на упавшем тесте печатает `::error::` с названием и шагами, а на прошедшем —
   ничего (иначе лог CI заполняется ложными ошибками).

Коды: `0` — всё как ожидалось; `1` — расхождение; `2` — не измерено (нет node или файлов).
"""

import argparse
import json
import os
import pathlib
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass


def free_port(start=4199):
    for port in range(start, start + 40):
        with socket.socket() as s:
            if s.connect_ex(("127.0.0.1", port)) != 0:
                return port
    return start


def http_get(url, timeout=5):
    try:
        with urllib.request.urlopen(url, timeout=timeout) as r:
            return r.status, r.headers.get("Content-Type", ""), r.read(64)
    except urllib.error.HTTPError as e:
        return e.code, e.headers.get("Content-Type", ""), b""
    except Exception as e:  # noqa: BLE001
        return None, str(e), b""


def check_serve(base):
    """(issues, checked)."""
    issues, checked = [], 0
    node = shutil.which("node")
    port = free_port()
    proc = subprocess.Popen(
        [node, "serve.js"],
        cwd=str(base),
        env={**os.environ, "PORT": str(port)},
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        url = f"http://127.0.0.1:{port}"
        status = None
        for _ in range(50):
            status, _ct, _ = http_get(url + "/", timeout=2)
            if status == 200:
                break
            time.sleep(0.2)
        checked += 1
        if status != 200:
            issues.append(f"serve.js не отдал `/` (последний статус {status}) — локальный прогон не стартует")
            return issues, checked

        for path, want_type in (("/", "text/html"), ("/js/app.js", "text/javascript")):
            checked += 1
            st, ct, _ = http_get(url + path)
            if st != 200 or want_type not in ct:
                issues.append(f"serve.js: `{path}` → статус {st}, Content-Type «{ct}», "
                              f"ожидали 200 и {want_type}")

        checked += 1
        st, _ct, _ = http_get(url + "/definitely-not-here")
        if st != 404:
            issues.append(f"serve.js: неизвестный путь отдал {st}, ожидали 404")

        # выход за корень: 403 (как в коде) или 404, но НЕ 200
        for evil in ("/%2e%2e/package.json", "/../package.json"):
            checked += 1
            st, _ct, body = http_get(url + evil)
            if st == 200:
                issues.append(f"serve.js отдаёт файл вне корня по `{evil}` (200) — это утечка, а не статика")
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
    return issues, checked


NOTICE_PROBE = r"""
const Notice = require('./reporters/notice.js');
const r = new Notice();
const test = { title: 'E2E: пример', expectedStatus: 'passed' };
const failed = {
  status: 'failed',
  steps: [
    { title: 'login по PIN', duration: 12000, error: null },
    { title: 'открыть журнал', duration: 30500, error: 'Timeout 20000ms' },
  ],
  errors: [{ message: 'TimeoutError: locator("#view-journal")' }],
};
r.onTestEnd(test, failed);
r.onTestEnd(test, { status: 'passed', steps: [], errors: [] });
r.onEnd({ status: 'failed' });
"""


def check_notice(base):
    """(issues, checked)."""
    issues, checked = [], 0
    node = shutil.which("node")
    proc = subprocess.run(
        [node, "-e", NOTICE_PROBE],
        cwd=str(base),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    out = (proc.stdout or "") + (proc.stderr or "")
    checked += 1
    if "::error::" not in out:
        issues.append("reporters/notice.js не печатает `::error::` на упавшем тесте — в CI падение "
                      "останется без аннотации")
        return issues, checked
    checked += 1
    if "login по PIN" not in out or "открыть журнал" not in out:
        issues.append("reporters/notice.js не печатает шаги падения — непонятно, на каком шаге упало")
    checked += 1
    if "12.0s" not in out and "30.5s" not in out:
        issues.append("reporters/notice.js не печатает длительность шагов — в логе не видно, "
                      "где ушло время (нужно после прогона с timedOut)")
    checked += 1
    errors = [l for l in out.splitlines() if "::error::" in l]
    checked += 1
    if len(errors) != 1:
        issues.append(f"reporters/notice.js напечатал {len(errors)} аннотаций `::error::` вместо одной — "
                      f"на прошедшем тесте он тоже шумит")
    checked += 1
    try:
        payload = json.loads(errors[0].split("::error::", 1)[1])
        if "title" not in payload or "status" not in payload:
            issues.append("в аннотации `::error::` нет title/status — GitHub не покажет, что упало")
    except (ValueError, IndexError) as e:  # noqa: BLE001
        issues.append(f"аннотация `::error::` не разбирается как JSON: {e}")
    return issues, checked


def receipt_line(checked, issues):
    return f"квитанция: проверок локального стека {checked} | нарушено: {len(issues)}"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    if not (base / "serve.js").exists() or not (base / "reporters" / "notice.js").exists():
        print("::error::check_e2e_local_stack: нет serve.js или notice.js — не измерено (rc=2)")
        return 2
    if not shutil.which("node"):
        print("::error::check_e2e_local_stack: нет node — не измерено (rc=2)")
        return 2
    issues, checked = check_serve(base)
    more, n2 = check_notice(base)
    issues += more
    checked += n2
    for m in issues:
        print(f"::error::check_e2e_local_stack: {m}")
    print(receipt_line(checked, issues))
    return 1 if issues else 0


def selftest():
    """Самопроверка: стаб-сервер, отдающий 200 на всё, обязан дать нарушения."""
    ok = total = 0
    tmp = pathlib.Path(tempfile.gettempdir()) / f"local-stack-{os.getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    try:
        # 1) плохой сервер: 200 на любой путь, неверный Content-Type
        bad = tmp / "bad"
        bad.mkdir()
        (bad / "serve.js").write_text(
            "const http=require('http');http.createServer((q,s)=>{s.writeHead(200,"
            "{'Content-Type':'text/plain'});s.end('x')}).listen(Number(process.env.PORT||0));\n",
            encoding="utf-8",
        )
        issues, checked = check_serve(bad)
        total += 1
        if issues and checked:
            ok += 1
        else:
            print(f"  [FAIL] плохой сервер не дал нарушений: {issues}")

        # 2) хороший сервер: корректные типы, 404 и защита от выхода за корень
        good = tmp / "good"
        good.mkdir()
        (good / "index.html").write_text("<html>ok</html>", encoding="utf-8")
        (good / "js").mkdir()
        (good / "js" / "app.js").write_text("console.log(1)", encoding="utf-8")
        (good / "serve.js").write_text(
            "const http=require('http'),fs=require('fs'),path=require('path');\n"
            "const ROOT=__dirname;\n"
            "http.createServer((q,s)=>{const u=decodeURIComponent((q.url||'/').split('?')[0]);\n"
            "const rel=u==='/'?'index.html':u.replace(/^\\/+/, '');const f=path.resolve(ROOT,rel);\n"
            "if(!f.startsWith(ROOT)){s.writeHead(403);return s.end('no')}\n"
            "fs.readFile(f,(e,d)=>{if(e){s.writeHead(404);return s.end('nf')}\n"
            "const t=f.endsWith('.html')?'text/html':f.endsWith('.js')?'text/javascript':'text/plain';\n"
            "s.writeHead(200,{'Content-Type':t});s.end(d)})}).listen(Number(process.env.PORT||0));\n",
            encoding="utf-8",
        )
        issues2, checked2 = check_serve(good)
        total += 1
        if not issues2 and checked2:
            ok += 1
        else:
            print(f"  [FAIL] корректный сервер не прошёл: {issues2}")

        total += 1
        line = receipt_line(5, [])
        if "проверок локального стека 5" in line and "нарушено: 0" in line and "None" not in line:
            ok += 1
        else:
            print(f"  [FAIL] текст квитанции: {line}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    print(f"check_e2e_local_stack --selftest: {ok}/{total} "
          f"(сервер с 200 на всё = нарушения, корректный = проходит)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
