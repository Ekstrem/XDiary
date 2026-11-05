"""check_e2e_assertions.py — у спеки есть хотя бы одно поведенческое утверждение.

Зачем: «экран виден» — слабое утверждение. Спека, которая проверяет только видимость, зеленеет
и на пустом состоянии, и на протухшей сессии (`pin-fallback` и `pin-first-login` так и было:
0 «сильных» утверждений из 9 и 8). Поведенческое утверждение — это сравнение данных:
`toBe(value)`, `toContainText`, `toHaveValue`, `toHaveCount`, `toHaveAttribute`, `toMatch`…
То есть проверка, которая **упадёт**, если поведение изменится, а экран останется.

Правило: каждая спека содержит ≥1 сильное утверждение, либо объявлена как LEGACY с причиной
(legacy не выполняется и не притворяется покрытием).

Коды: `0` — все спеки с поведенческим утверждением; `1` — есть спеки только про видимость;
`2` — не измерено (нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

STRONG = re.compile(
    r"toContainText|toHaveText|toHaveValue|toHaveCount|toHaveAttribute|toHaveClass|"
    r"toBe\(|toEqual\(|toMatch\(|toHaveLength\(|toBeTruthy\(|toBeFalsy\(|toBeGreaterThan"
)
LEGACY = re.compile(r"LEGACY")


def evaluate(specs):
    """(issues, stats). specs — {имя: текст}."""
    issues = []
    strong = weak = legacy = 0
    for name, text in sorted(specs.items()):
        head = "\n".join(text.splitlines()[:12])
        has_strong = bool(STRONG.search(text))
        if has_strong:
            strong += 1
            continue
        if LEGACY.search(head):
            legacy += 1
            continue
        weak += 1
        issues.append(f"{name}: нет ни одного поведенческого утверждения "
                      f"(только видимость) — добавьте проверку данных или пометьте LEGACY с причиной")
    stats = {"specs": len(specs), "strong": strong, "legacy": legacy, "weak": weak}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | с поведенческим утверждением {stats['strong']} | "
            f"LEGACY {stats['legacy']} | только видимость {stats['weak']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_assertions: нет каталога спек — не измерено (rc=2)")
        return 2
    try:
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_assertions: не читается спека: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_assertions: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("сильное утверждение есть",
         {"a.spec.js": "await expect(locator).toHaveValue('45');"}, 0,
         ["с поведенческим утверждением 1", "нарушено: 0"])

    case("только видимость",
         {"b.spec.js": "await expect(page.locator('#view')).toBeVisible();"}, 1,
         ["b.spec.js", "только видимость"])

    case("LEGACY с причиной не нарушает",
         {"c.spec.js": "// LEGACY: снят по ADR-021\nawait expect(page.locator('#x')).toBeVisible();"},
         0, ["LEGACY 1", "нарушено: 0"])

    case("сравнение через expect(...).toBe в строке",
         {"d.spec.js": "expect(me.status).toBe(200);"}, 0, ["нарушено: 0"])

    total += 1
    line = receipt_line({"specs": 3, "strong": 2, "legacy": 1, "weak": 0}, [])
    if "спек 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_assertions --selftest: {ok}/{total} (спека только про видимость = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
