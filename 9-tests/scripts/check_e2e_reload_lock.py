"""check_e2e_reload_lock.py — после `reload` замок снимают или проверяют, а не «ждут журнал вслепую».

Класс дефекта (реальный, из прогона `REF 73f59ba7`): спека входит (`loginWithPin` ставит локальный
PIN), затем перезагружает страницу и ждёт `#view-journal`. Но после `reload` приложение показывает
`#view-lock` при наличии PIN (`app.js`: `Pin.hasPin() = true` → `showLockScreen()`), поэтому журнал
остаётся `hidden`, и тест падает на ожидании — а в аннотации это выглядит как «не дождались», хотя
причина в порядке шагов. Именно так падал `wizard-record` (три теста одного спека).

Правило: если следующих 12 строках после `page.reload()`/`page.goto('/')` нет ни `unlockIfLocked`,
ни работы с `#view-lock`, ни `waitAppReady` — это подозрительное место. Исключения допустимы, но
должны быть записаны с причиной (например, чистый профиль без PIN).

Коды: `0` — все `reload` обрабатывают замок; `1` — есть «слепые» reload; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

RELOAD = re.compile(r"page\.(reload\(\)|goto\(\s*['\"]/['\"]\s*\))")
HANDLES_LOCK = re.compile(r"unlockIfLocked|view-lock|waitAppReady")

# Спеки, где «слепой» reload допустим по смыслу, с причиной.
EXEMPT = {
    "onboarding.spec.js": "чистый профиль без локального PIN: замка после reload не бывает (ADR-021)",
    "no-pin.spec.js": "LEGACY describe.skip: контракт «PIN выдаёт токен» отменён (ADR-021)",
    "pin-first-login.spec.js": "чистое устройство: до онбординга локального PIN нет, замок проверяется "
                               "внутри того же шага (строки с `#view-lock` дальше 12 строк)",
}


def evaluate(specs):
    """(issues, stats)."""
    issues, checked, exempt = [], 0, 0
    for name, text in sorted(specs.items()):
        lines = text.splitlines()
        local_hits = 0
        for i, line in enumerate(lines):
            if not RELOAD.search(line):
                continue
            window = " ".join(lines[i + 1:i + 13])
            if HANDLES_LOCK.search(window):
                continue
            local_hits += 1
        if not local_hits:
            continue
        if name in EXEMPT:
            exempt += 1
            continue
        checked += 1
        issues.append(f"{name}: {local_hits} `reload` без снятия/проверки замка — после входа приложение "
                      f"покажет `#view-lock`, и ожидание `#view-journal` упадёт «не дождались» "
                      f"(нужен `unlockIfLocked(page)` или явная проверка `#view-lock`)")
    stats = {"specs": len(specs), "flagged": checked, "exempt": exempt}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | со «слепым» reload {stats['flagged']} | "
            f"исключений {stats['exempt']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_reload_lock: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_reload_lock: {m}")
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

    case("замок снимают",
         {"a.spec.js": "await page.reload();\nawait unlockIfLocked(page);\nawait expect(page.locator('#view-journal')).toBeVisible();"},
         0, ["нарушено: 0"])

    case("слепой reload",
         {"b.spec.js": "await page.reload();\nawait expect(page.locator('#view-journal')).toBeVisible();"},
         1, ["b.spec.js", "не дождались"])

    case("явная проверка замка тоже считается",
         {"c.spec.js": "await page.reload();\nawait expect(page.locator('#view-lock')).toBeVisible();"},
         0, ["нарушено: 0"])

    case("чистый профиль в исключении",
         {"onboarding.spec.js": "await page.reload();\nawait expect(page.locator('#view-journal')).toBeVisible();"},
         0, ["исключений 1"])

    case("goto('/') тоже проверяется",
         {"d.spec.js": "await page.goto('/');\nawait expect(page.locator('#view-journal')).toBeVisible();"},
         1, ["d.spec.js"])

    total += 1
    line = receipt_line({"specs": 3, "flagged": 1, "exempt": 1}, [])
    if "со «слепым» reload 1" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_reload_lock --selftest: {ok}/{total} (reload без снятия замка = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
