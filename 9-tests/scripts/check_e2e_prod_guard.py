"""check_e2e_prod_guard.py — предохранитель «E2E только против dev» на месте.

Зачем: `DO-017` — до 01.11 E2E гонялся против **прода**, и UI-тесты писали тестовые записи и меняли
PIN в реальном дневнике владельца. Предохранитель живёт в `global-setup.js`: прогон падает до первого
теста, если стенд не dev/локальный. Убери или ослабь его — и ни один статический гейт не заметит,
а последствия необратимы (это данные, а не код).

Проверки:
1. `playwright.config.js` подключает `globalSetup` (иначе предохранитель не выполняется);
2. `global-setup.js` содержит проверку хоста (`assertNotProd` + список `ALLOWED_HOSTS`);
3. в списке разрешённых хостов **нет** прода (`vps1`);
4. базовый адрес по умолчанию (`api.js`) не указывает на прод (`vps1`).

Коды: `0` — предохранитель на месте; `1` — ослаблен/отсутствует; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PROD_MARK = "vps1"
ALLOWED = re.compile(r"ALLOWED_HOSTS\s*=\s*\[(.*?)\]", re.S)
HOST = re.compile(r"'([^']+)'|\"([^\"]+)\"")


def evaluate(config_text, setup_text, api_text):
    """(issues, stats)."""
    issues = []
    if "globalSetup" not in config_text:
        issues.append("`playwright.config.js` не подключает `globalSetup` — предохранитель DO-017 "
                      "не выполнится ни в одном прогоне")
    has_guard = "assertNotProd" in setup_text
    if not has_guard:
        issues.append("`global-setup.js` не содержит проверку `assertNotProd` — прогон может уйти "
                      "на прод и испортить реальный дневник (DO-017)")
    m = ALLOWED.search(setup_text)
    hosts = []
    if m:
        hosts = [a or b for a, b in HOST.findall(m.group(1))]
    if not hosts:
        issues.append("в `global-setup.js` нет списка `ALLOWED_HOSTS` — нечем ограничить стенд")
    prod_hosts = [h for h in hosts if PROD_MARK in h]
    if prod_hosts:
        issues.append("в `ALLOWED_HOSTS` попал прод: " + ", ".join(prod_hosts) +
                      " — E2E писали бы в реальный дневник (DO-017)")
    default_prod = False
    for line in api_text.splitlines():
        if "XDIARY_E2E_BASE_URL" in line and PROD_MARK in line:
            default_prod = True
    if default_prod:
        issues.append("базовый адрес по умолчанию в `api.js` указывает на прод — прогон без переменной "
                      "окружения пошёл бы в реальный дневник (DO-017)")
    stats = {"guard": 1 if has_guard else 0, "hosts": len(hosts),
             "global_setup": 1 if "globalSetup" in config_text else 0,
             "prod_hosts": len(prod_hosts), "default_prod": 1 if default_prod else 0}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: globalSetup подключён {'да' if stats['global_setup'] else 'НЕТ'} | "
            f"проверка хоста {'есть' if stats['guard'] else 'НЕТ'} | разрешённых хостов {stats['hosts']} | "
            f"прода в списке {stats['prod_hosts']} | прод по умолчанию "
            f"{'ДА' if stats['default_prod'] else 'нет'} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    cfg, setup, api = base / "playwright.config.js", base / "tests" / "global-setup.js", base / "tests" / "api.js"
    if not cfg.exists() or not setup.exists() or not api.exists():
        print("::error::check_e2e_prod_guard: нет конфига, global-setup или api.js — не измерено (rc=2)")
        return 2
    try:
        issues, stats = evaluate(
            cfg.read_text(encoding="utf-8", errors="replace"),
            setup.read_text(encoding="utf-8", errors="replace"),
            api.read_text(encoding="utf-8", errors="replace"),
        )
    except OSError as e:
        print(f"::error::check_e2e_prod_guard: не читается вход: {e} — не измерено (rc=2)")
        return 2
    for m in issues:
        print(f"::error::check_e2e_prod_guard: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    GOOD_CFG = "globalSetup: './tests/global-setup.js',"
    GOOD_SETUP = ("const ALLOWED_HOSTS = ['xdiary-vps2.taile0b6ee.ts.net', 'localhost', '127.0.0.1'];\n"
                  "function assertNotProd() {}\n")
    GOOD_API = "const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps2.taile0b6ee.ts.net');"

    def case(name, cfg, setup, api, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(cfg, setup, api)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("предохранитель на месте", GOOD_CFG, GOOD_SETUP, GOOD_API, 0, ["нарушено: 0", "прода в списке 0"])
    case("globalSetup не подключён", "", GOOD_SETUP, GOOD_API, 1, ["не подключает `globalSetup`"])
    case("проверки хоста нет", GOOD_CFG, "module.exports = async function () {};", GOOD_API, 1,
         ["не содержит проверку `assertNotProd`"])
    case("прод в списке разрешённых", GOOD_CFG,
         "const ALLOWED_HOSTS = ['xdiary-vps1.taile0b6ee.ts.net'];\nfunction assertNotProd() {}\n",
         GOOD_API, 1, ["в `ALLOWED_HOSTS` попал прод"])
    case("прод по умолчанию в api.js", GOOD_CFG, GOOD_SETUP,
         "const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps1.taile0b6ee.ts.net');",
         1, ["указывает на прод"])

    total += 1
    line = receipt_line({"guard": 1, "hosts": 3, "global_setup": 1, "prod_hosts": 0, "default_prod": 0}, [])
    if "разрешённых хостов 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_prod_guard --selftest: {ok}/{total} "
          f"(прод в списке/по умолчанию = rc=1, DO-017)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
