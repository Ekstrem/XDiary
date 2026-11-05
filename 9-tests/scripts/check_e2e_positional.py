"""check_e2e_positional.py — в спеках нет кликов «по номеру» в динамических списках.

Зачем: `locator('#wizard-progress button').nth(6)` держится на **порядке** элементов. Поменяли
порядок/число шагов мастера — спека молча кликает не туда: падение (или ложный зелёный) выглядит
как дефект продукта, хотя сломан тест. Ровно так было в `edit-delete-record` (`nth(6)` = «Длительность»,
`nth(8)` = «Итог»): заменили на `button[title="Длительность"]`.

Правило: литеральный `.nth(<цифра>)` на динамическом контейнере — нарушение. Индекс-переменная
(`nth(i)` внутри цикла по чипам) разрешена: там порядок и есть предмет проверки.

Коды: `0` — литеральных индексов нет; `1` — есть; `2` — не измерено (нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# динамические контейнеры: число и порядок детей меняются данными/шагами
DYNAMIC = (r"#wizard-progress", r"#trend-chips", r"#journal-list", r"#partners-list",
           r"#catalog-list", r"#onboarding-list", r"\.record-card")
INDEX = re.compile(r"\.nth\(\s*(\d+)\s*\)")


def evaluate(specs):
    """(issues, stats). specs — {имя: текст}."""
    issues = []
    dyn_re = re.compile("|".join(DYNAMIC))
    checked = 0
    for name, text in sorted(specs.items()):
        for i, line in enumerate(text.splitlines(), 1):
            if not INDEX.search(line):
                continue
            if not dyn_re.search(line):
                continue
            checked += 1
            n = INDEX.search(line).group(1)
            issues.append(f"{name}:{i}: клик по номеру `.nth({n})` в динамическом списке — "
                          f"выберите элемент по признаку (title/имя/данные)")
    stats = {"specs": len(specs), "positional": checked}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | индексов в динамических списках: "
            f"{stats['positional']} | нарушено: {len(issues)}")


def read_specs(tests_dir):
    """({имя: текст}, None) либо (None, причина). Ошибка чтения — «не измерено», не traceback."""
    specs = {}
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            specs[p.name] = p.read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        return None, f"не читается {getattr(e, 'filename', tests_dir)}: {e}"
    if not specs:
        return None, "каталог спек пуст"
    return specs, None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_positional: нет каталога спек — не измерено (rc=2)")
        return 2
    specs, err = read_specs(tests_dir)
    if err:
        print(f"::error::check_e2e_positional: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_positional: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:160]}")

    case("по признаку — норма",
         {"a.spec.js": 'await page.locator(\'#wizard-progress button[title="Итог"]\').click();'}, 0,
         ["индексов в динамических списках: 0", "нарушено: 0"])

    case("клик по номеру в динамическом списке",
         {"b.spec.js": "await page.locator('#wizard-progress button').nth(6).click();"}, 1,
         ["b.spec.js", ".nth(6)"])

    case("индекс-переменная в цикле разрешена",
         {"c.spec.js": "for (let i = 0; i < 3; i++) { await page.locator('#trend-chips button').nth(i).click(); }"},
         0, ["нарушено: 0"])

    case("номер в НЕдинамическом селекторе разрешён",
         {"d.spec.js": "await page.locator('#cards div').nth(2).click();"}, 0, ["нарушено: 0"])

    total += 1
    line = receipt_line({"specs": 2, "positional": 1}, [])
    if "спек 2" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_positional --selftest: {ok}/{total} (литеральный nth в динамическом списке = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
