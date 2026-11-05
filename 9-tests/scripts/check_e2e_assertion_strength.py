"""check_e2e_assertion_strength.py — утверждения проверяют поведение, а не «что-то истинное».

Зачем: `expect(x).toBeTruthy()` зеленеет и на `{}`, и на `"undefined"`, и на неправильном объекте.
Такие проверки создают ощущение покрытия, но не ловят регресс. Гейт считает долю «слабых»
утверждений (`toBeTruthy`, `not.toBeNull`, `toBeDefined`, `not.toBeFalsy`) против «сильных»
(`toBe`, `toEqual`, `toMatch`, `toContain`, `toBeVisible`, `toHaveText`, `toHaveCount`,
`toHaveValue`, `toHaveClass`, `toMatchObject`, `toBeTypeOf`) в каждой спеке и в наборе целиком.

Порог: в спеке — не больше `MAX_PER_SPEC` слабых от всех; в наборе — не больше `MAX_TOTAL`.
LEGACY-спеки (`describe.skip`) не считаются: они не исполняются.

Коды: `0` — в пределах порога; `1` — превышение; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

WEAK = ("toBeTruthy()", "not.toBeNull()", "toBeDefined()", "not.toBeFalsy()")
STRONG = ("toBe(", "toEqual(", "toMatch(", "toMatchObject(", "toBeTypeOf(", "toContain(",
          "toContainEqual(", "toBeVisible", "toHaveText", "toContainText", "toHaveCount",
          "toHaveValue", "toHaveClass", "toHaveAttribute", "toBeHidden", "toBeEnabled")

MAX_PER_SPEC = 0.30
MAX_TOTAL = 0.15


def is_legacy(text):
    head = "\n".join(text.splitlines()[:12])
    return "LEGACY" in head or "describe.skip" in text


def count(text):
    weak = sum(text.count(k) for k in WEAK)
    # «сильные» считаем без пересечения со слабыми: `toBeTruthy()` не содержит `toBe(`,
    # `not.toBeNull()` не содержит `toBe(`, поэтому двойного счёта нет.
    strong = sum(text.count(k) for k in STRONG)
    return weak, strong


def evaluate(specs):
    """(issues, stats)."""
    issues = []
    rows = []
    tw = ts = 0
    for name, text in sorted(specs.items()):
        if is_legacy(text):
            continue
        w, s = count(text)
        tw += w
        ts += s
        total = w + s
        ratio = (w / total) if total else 0.0
        rows.append((name, w, s, ratio))
        if total and ratio > MAX_PER_SPEC:
            issues.append(f"{name}: слабых утверждений {w} из {total} ({ratio * 100:.0f}%) — "
                          f"больше порога {MAX_PER_SPEC * 100:.0f}%; `toBeTruthy()` зеленеет на "
                          f"`{{}}` и `'undefined'`")
    total_ratio = (tw / (tw + ts)) if (tw + ts) else 0.0
    if total_ratio > MAX_TOTAL:
        issues.append(f"набор: слабых {tw} из {tw + ts} ({total_ratio * 100:.0f}%) — "
                      f"больше порога {MAX_TOTAL * 100:.0f}%")
    stats = {"specs": len(rows), "weak": tw, "strong": ts,
             "ratio": round(total_ratio * 100, 1), "worst": max(rows, key=lambda r: r[3])[0] if rows else "—"}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | слабых {stats['weak']} | сильных {stats['strong']} | "
            f"доля слабых {stats['ratio']}% | хуже всех {stats['worst']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_assertion_strength: нет папки спек — не измерено (rc=2)")
        return 2
    try:
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_assertion_strength: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::warning::check_e2e_assertion_strength: {m}")
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

    case("сильные утверждения", {"a.spec.js": "expect(x).toBe(1); expect(y).toEqual({});"}, 0,
         ["слабых 0", "нарушено: 0"])
    case("слабое большинство", {"b.spec.js": "expect(a).toBeTruthy(); expect(b).toBeTruthy(); expect(c).toBe(1);"}, 1,
         ["b.spec.js", "больше порога"])
    case("LEGACY не считается", {"c.spec.js": "// LEGACY\ndescribe.skip('x', () => { expect(x).toBeTruthy(); });"}, 0,
         ["нарушено: 0"])
    case("одна слабая среди многих сильных — в пределах порога",
         {"d.spec.js": "expect(a).toBeTruthy(); expect(b).toBe(1); expect(c).toBe(2); expect(d).toBe(3); "
                       "expect(e).toBe(4); expect(f).toBe(5); expect(g).toBe(6); expect(h).toBe(7); "
                       "expect(i).toBe(8); expect(j).toBe(9);"}, 0,
         ["нарушено: 0", "доля слабых 10.0%"])

    total += 1
    line = receipt_line({"specs": 2, "weak": 1, "strong": 9, "ratio": 10.0, "worst": "a.spec.js"}, [])
    if "слабых 1" in line and "доля слабых 10.0%" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_assertion_strength --selftest: {ok}/{total} "
          f"(доля слабых выше порога = rc=1, LEGACY исключается)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
