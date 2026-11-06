"""check_e2e_or_first.py — `.or(...).first()` на экранах не возвращает «видимый» элемент.

Реальный дефект (два падения в прогоне #775): `page.locator('#view-lock').or(locator('#view-onboarding')).first()`
упорядочивает элементы **по DOM**, а не по видимости. `#view-lock` в `8-front/index.html` идёт раньше
остальных экранов, поэтому `.first()` всегда возвращал скрытый замок — и проверка падала, **хотя
приложение уже показало журнал**. В снимке `error-context.md` это видно дословно:
`locator resolved to <section id="view-lock" class="view hidden">`.

Правило: в спеках `.or(` не комбинируется с `.first()`/`.last()`/`.nth(` — для «любой из экранов»
есть `waitAnyScreen` (`tests/api.js`) или селектор с `:not(.hidden)`. Иначе — `rc=1`.

Коды: `0` — чисто; `1` — есть запрещённая комбинация; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

OR = re.compile(r"\.or\(")
PICK = re.compile(r"\.(first|last|nth)\(")
# разрешённые формы: селектор по видимости или helper
ALLOW = re.compile(r":not\(\.hidden\)|waitAnyScreen|filter\(\s*\{\s*visible")

# Исключения оформляются карточкой TE и содержат причину.
EXEMPT = {}


def evaluate(specs):
    issues, total = [], 0
    for name, text in sorted(specs.items()):
        if name in EXEMPT:
            continue
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if line.strip().startswith("//"):
                continue
            if not OR.search(line):
                continue
            total += 1
            # смотрим сам вызов и 3 строки ниже (цепочка может быть разбита)
            window = "\n".join(lines[i:i + 4])
            if PICK.search(window) and not ALLOW.search(window):
                issues.append(
                    f"{name}:{i + 1}: `.or(...)` с `{PICK.search(window).group(1)}()` — элемент выбирается "
                    f"по порядку в DOM, а не по видимости; используй `waitAnyScreen` (tests/api.js) "
                    f"или селектор с `:not(.hidden)`"
                )
    stats = {"specs": len(specs), "or_locators": total}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | `.or()` в спеках {stats['or_locators']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_or_first: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_or_first: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:180]}")

    case("or+first без видимости — красный",
         {"a.spec.js": "await expect(lock.or(onb).first()).toBeVisible();"},
         1, ["a.spec.js:1", "waitAnyScreen"])

    case("or+first через перенос строки — красный",
         {"b.spec.js": "const l = lock\n  .or(onb)\n  .first();"},
         1, ["b.spec.js:2"])

    case("waitAnyScreen — зелёный",
         {"c.spec.js": "const s = await waitAnyScreen(page, ['view-lock', 'view-journal'], 20000);"},
         0, ["нарушено: 0", "`.or()` в спеках 0"])

    case("селектор с :not(.hidden) — зелёный",
         {"d.spec.js": "const v = page.locator('#view-lock:not(.hidden), #view-journal:not(.hidden)');"},
         0, ["нарушено: 0"])

    case("or без first — зелёный",
         {"e.spec.js": "const both = lock.or(onb);"},
         0, ["`.or()` в спеках 1"])

    total += 1
    line = receipt_line({"specs": 2, "or_locators": 3}, [])
    if "`.or()` в спеках 3" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_or_first --selftest: {ok}/{total} "
          f"(`.or(...).first()` на экранах = rc=1, потому что выбирается по DOM, а не по видимости)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
