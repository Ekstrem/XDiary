"""check_e2e_globalsetup_behaviour.py — `global-setup.js` проверяется ПОВЕДЕНИЕМ, а не текстом.

Зачем: `check_e2e_prod_guard.py` смотрит, что в файле есть `assertNotProd` и `ALLOWED_HOSTS`. Но это
статическая проверка: она не доказывает, что предохранитель реально **срабатывает**, что ожидание
поднятия стенда **повторяет** запросы на 502/503 и что детектор «стенд старой сборки» (TE-039) падает
на неверном контракте. Все эти ветки выполняются до спеков, в CI, и стоили проекту нескольких
многочасовых разборов.

Метод: берём **настоящий** `tests/global-setup.js` и запускаем его в песочнице с подставным
`@playwright/test` (`request.newContext()` отвечает по сценарию из переменной окружения). Браузер и сеть
не нужны — проверяется логика сетапа.

Сценарии (каждый — с ожидаемым результатом):
    1. прод в `XDIARY_E2E_BASE_URL`           → падение с «только против dev/локального» (DO-017);
    2. здоровый dev-стенд                     → успех;
    3. `/health`: 502, 502, 200              → успех (ожидание поднятия, TE-035);
    4. `POST /clients/refresh` → 405          → падение `[infra] stale-image` (TE-039);
    5. `GET /clients/me` без токена → 200     → падение `stale-image` (BE-038);
    6. `/health` всегда 500                   → падение (не инфраструктурный статус, ждать бессмысленно).

Коды: `0` — все сценарии отработали как ожидалось; `1` — поведение разошлось; `2` — не измерено
(нет `global-setup.js` или `node`).
"""

import argparse
import json
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

DEV = "https://xdiary-vps2.taile0b6ee.ts.net"
PROD = "https://xdiary-vps1.taile0b6ee.ts.net"

# Сценарий = словарь: путь -> список ответов [status, body], которые выдаются по очереди
# (последний повторяется). Отдельно: пути, которые проверяются в контракте.
HEALTHY = {
    "/health": [[200, {"status": "ok"}]],
    "/api/v1/clients": [[405, {"error": "method not allowed"}]],
    "/api/v1/partners": [[401, {"error": "unauthorized"}]],
    "POST /api/v1/clients/refresh": [[401, {"error": "bad token"}]],
    "POST /api/v1/clients": [[200, {"id": "client-1"}]],
    "POST /api/v1/clients/client-1/verify": [[200, {"accessToken": "a", "refreshToken": "r"}]],
    "/api/v1/clients/me": [[401, {"error": "unauthorized"}, {"www-authenticate": "Bearer"}]],
}

CASES = [
    ("прод в адресе стенда", PROD, HEALTHY, False, "только против dev"),
    ("здоровый dev-стенд", DEV, HEALTHY, True, ""),
    ("/health 502,502,200 — ожидание поднятия", DEV,
     {**HEALTHY, "/health": [[502, {}], [502, {}], [200, {"status": "ok"}]]}, True, ""),
    ("refresh 405 — стенд старой сборки", DEV,
     {**HEALTHY, "POST /api/v1/clients/refresh": [[405, {}]]}, False, "stale-image"),
    ("/clients/me без токена 200 — нет авторизации", DEV,
     {**HEALTHY, "/api/v1/clients/me": [[200, {}]]}, False, "stale-image"),
    ("/health всегда 500 — не инфраструктурный статус", DEV,
     {**HEALTHY, "/health": [[500, {}]]}, False, "не инфраструктурное состояние"),
]

STUB_PW = r"""// Подставной `@playwright/test`: только то, что использует global-setup.js.
const scenario = JSON.parse(process.env.GS_SCENARIO || '{}');
const counters = {};
function pick(key) {
  const list = scenario[key] || [[404, {}]];
  const n = counters[key] || 0;
  counters[key] = n + 1;
  const item = list[Math.min(n, list.length - 1)];
  return item;
}
function makeResponse(status, body, headers) {
  return {
    status: () => status,
    ok: () => status >= 200 && status < 300,
    headers: () => headers || {},
    json: async () => body,
    text: async () => JSON.stringify(body),
  };
}
async function newContext() {
  return {
    get: async (url) => {
      const path = String(url).replace(/^https?:\/\/[^/]+/, '');
      const [status, body, headers] = pick(path);
      return makeResponse(status, body, headers);
    },
    post: async (url, opts) => {
      const path = String(url).replace(/^https?:\/\/[^/]+/, '');
      const [status, body, headers] = pick('POST ' + path);
      return makeResponse(status, body, headers);
    },
    dispose: async () => {},
  };
}
module.exports = { request: { newContext } };
"""

STUB_API = r"""// Подставной tests/api.js: адрес стенда берётся из окружения.
const API_BASE = (process.env.XDIARY_E2E_BASE_URL || '').replace(/\/+$/, '');
const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
module.exports = { API_BASE, TEST_OTP };
"""

RUNNER = (
    "require('./tests/global-setup.js')().then("
    "() => { console.log('GS_OK'); process.exit(0); }).catch((e) => {"
    " console.log('GS_FAIL:' + String((e && e.message) || e)); process.exit(1); })"
)


def build_harness(tmp, setup_src):
    (tmp / "tests").mkdir(parents=True, exist_ok=True)
    pkg = tmp / "node_modules" / "@playwright" / "test"
    pkg.mkdir(parents=True, exist_ok=True)
    (pkg / "index.js").write_text(STUB_PW, encoding="utf-8")
    (tmp / "tests" / "api.js").write_text(STUB_API, encoding="utf-8")
    shutil.copyfile(setup_src, tmp / "tests" / "global-setup.js")


def run_case(tmp, base_url, scenario):
    env = {
        **os.environ,
        "XDIARY_E2E_BASE_URL": base_url,
        "XDIARY_E2E_TEST_OTP": "000000",
        "GS_SCENARIO": json.dumps(scenario),
    }
    node = shutil.which("node")
    if not node:
        return None, "нет node"
    proc = subprocess.run(
        [node, "-e", RUNNER],
        cwd=str(tmp),
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=180,
    )
    out = (proc.stdout or "") + (proc.stderr or "")
    return proc.returncode, out


def evaluate(tmp, setup_src):
    """(issues, stats)."""
    build_harness(tmp, setup_src)
    issues, passed = [], 0
    for name, base_url, scenario, should_pass, needle in CASES:
        rc, out = run_case(tmp, base_url, scenario)
        if rc is None:
            print(f"::error::check_e2e_globalsetup_behaviour: {out} — не измерено (rc=2)")
            return None, None
        ok = (rc == 0) if should_pass else (rc != 0 and needle in out)
        if ok:
            passed += 1
        else:
            expect = "успех" if should_pass else f"падение с «{needle}»"
            issues.append(f"сценарий «{name}»: ждали {expect}, получили rc={rc}; "
                          f"вывод: {out.strip().splitlines()[-1][:180] if out.strip() else '(пусто)'}")
    stats = {"cases": len(CASES), "passed": passed}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: сценариев global-setup {stats['cases']} | как ожидалось {stats['passed']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    setup = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests" / "global-setup.js"
    if not setup.exists():
        print("::error::check_e2e_globalsetup_behaviour: нет global-setup.js — не измерено (rc=2)")
        return 2
    if not shutil.which("node"):
        print("::error::check_e2e_globalsetup_behaviour: нет node — не измерено (rc=2)")
        return 2
    tmp = pathlib.Path(tempfile.gettempdir()) / f"gs-behaviour-{os.getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    try:
        issues, stats = evaluate(tmp, setup)
        if issues is None:
            return 2
    except subprocess.TimeoutExpired:
        print("::error::check_e2e_globalsetup_behaviour: сетап не завершился за 180 с — не измерено (rc=2)")
        return 2
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    for m in issues:
        print(f"::error::check_e2e_globalsetup_behaviour: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    """Самопроверка: подложный сетап, который всегда падает, обязан дать нарушение."""
    ok = total = 0
    tmp = pathlib.Path(tempfile.gettempdir()) / f"gs-selftest-{os.getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    try:
        good = tmp / "good-setup.js"
        good.write_text(
            "const { request } = require('@playwright/test');\n"
            "const { API_BASE } = require('./api');\n"
            "module.exports = async function () {\n"
            "  if (!/vps2|localhost|127\\.0\\.0\\.1/.test(API_BASE)) throw new Error('только против dev');\n"
            "  const ctx = await request.newContext();\n"
            "  await ctx.get(API_BASE + '/health');\n"
            "  await ctx.dispose();\n"
            "};\n",
            encoding="utf-8",
        )
        issues, stats = evaluate(tmp, good)
        total += 1
        if stats and stats["passed"] >= 2:
            ok += 1
        else:
            print(f"  [FAIL] самопроверка на корректном сетапе: {stats}, {issues}")

        bad = tmp / "bad-setup.js"
        bad.write_text("module.exports = async function () {};\n", encoding="utf-8")
        issues2, stats2 = evaluate(tmp, bad)
        total += 1
        if issues2:
            ok += 1
        else:
            print("  [FAIL] пустой сетап не дал нарушений (проверка не ловит отсутствие защиты)")

        total += 1
        line = receipt_line({"cases": 6, "passed": 6}, [])
        if "сценариев global-setup 6" in line and "нарушено: 0" in line and "None" not in line:
            ok += 1
        else:
            print(f"  [FAIL] текст квитанции: {line}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    print(f"check_e2e_globalsetup_behaviour --selftest: {ok}/{total} "
          f"(пустой сетап = нарушения, корректный = проходит)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
