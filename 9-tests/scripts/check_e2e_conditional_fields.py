"""check_e2e_conditional_fields.py — поля, скрытые по условию, заполняют только когда они видны.

Реальный дефект (три падения `catalog` в CI): `app.js:1404/1407` показывает `#ce-brand` только при
`kind === "toy"`, а `#ce-partner` — только при `kind === "fantasy"`. Спека заполняла `#ce-brand` для всех
видов; у остальных поле лежит в скрытой обёртке, `page.fill` ждёт видимости и падает по общему лимиту
(«не дождались»), хотя продукт исправен.

Правило: если спека делает `fill`/`selectOption`/`click` по элементу, чья обёртка `*-wrap` скрывается
приложением (`classList.toggle("hidden", …)`), то рядом (в 8 строках выше) обязана быть проверка
видимости (`isVisible()`). Иначе — `rc=1`.

Коды: `0` — все обращения защищены; `1` — есть незащищённые; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# обёртки, которые приложение скрывает по условию (найдены по `classList.toggle("hidden"` в app.js)
WRAPPERS = ("ce-brand-wrap", "ce-partner-wrap")
ACTION = re.compile(r"\b(fill|selectOption|click)\(\s*['\"`]#([\w-]+)['\"`]")
GUARD = re.compile(r"isVisible\(\)")


def evaluate(specs, wrappers=WRAPPERS):
    """(issues, stats)."""
    issues, checked = [], 0
    for name, text in sorted(specs.items()):
        lines = text.splitlines()
        for i, line in enumerate(lines):
            m = ACTION.search(line)
            if not m:
                continue
            field = m.group(2)
            # ищем обёртку, в которой живёт поле: `<prefix>-wrap`
            wrap = next((w for w in wrappers if field in w.replace("-wrap", "")), None)
            if not wrap:
                continue
            checked += 1
            window = "\n".join(lines[max(0, i - 8):i + 1])
            if not GUARD.search(window):
                issues.append(f"{name}:{i + 1}: `{m.group(1)}('#{field}')` без проверки видимости "
                              f"(`#{wrap}` скрывается по условию — app.js `classList.toggle(\"hidden\")`)")
    stats = {"specs": len(specs), "actions": checked}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | обращений к условным полям {stats['actions']} | "
            f"без проверки видимости: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_conditional_fields: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_conditional_fields: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:170]}")

    case("заполнение под проверкой видимости",
         {"a.spec.js": "if (await page.locator('#ce-brand-wrap').isVisible()) {\n  await page.fill('#ce-brand', 'X');\n}"},
         0, ["без проверки видимости: 0"])

    case("заполнение без проверки",
         {"b.spec.js": "await page.fill('#ce-brand', 'X');"},
         1, ["b.spec.js", "без проверки видимости"])

    case("поле без условной обёртки не считается",
         {"c.spec.js": "await page.fill('#ce-name', 'X');"},
         0, ["обращений к условным полям 0"])

    total += 1
    line = receipt_line({"specs": 3, "actions": 2}, [])
    if "обращений к условным полям 2" in line and "без проверки видимости: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_conditional_fields --selftest: {ok}/{total} "
          f"(поле в скрытой обёртке без isVisible = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
