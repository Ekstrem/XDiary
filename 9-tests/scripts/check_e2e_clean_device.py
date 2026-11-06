"""check_e2e_clean_device.py — спеки «чистое устройство» не должны получать засеянную сессию.

Зачем: фикстура кэширует сессию на процесс (`SEED_SESSION` в `tests/api.js`) и подкладывает токен
в `localStorage` до старта приложения — иначе 29 тестов проходят 29 онбордингов, а это ровно то время,
которое на прогоне `18 failed` (REF `73f59ba7`) съедало лимит теста. Но у этого ускорения есть цена:
спека, которая ПРОВЕРЯЕТ первый вход (онбординг, установку PIN на новом устройстве, путь «без PIN»),
с засеянной сессией теряет смысл — она проверит «уже вошли», а не «вход».

Правило: спеки из списка `CLEAN_DEVICE` обязаны стартовать с чистого устройства, то есть НЕ вызывать
`loginWithPin` / `seedSession` / `installBase`-путь с сессией. Им разрешено `installBase` (он ставит
только адрес API) и собственный онбординг.

Коды: `0` — правило соблюдено; `1` — нарушено; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# Спеки, которые проверяют ПЕРВЫЙ вход; засеянная сессия ломает их смысл.
CLEAN_DEVICE = ("onboarding.spec.js", "pin-first-login.spec.js", "no-pin.spec.js")
FORBIDDEN = ("loginWithPin(", "seedSession(")
EXEMPT = {
    "no-pin.spec.js": "LEGACY-спека (describe.skip): проверяет убранный контракт «PIN выдаёт токен»",
}


def evaluate(specs):
    """(issues, stats)."""
    issues, checked, exempt = [], 0, 0
    for name in CLEAN_DEVICE:
        text = specs.get(name)
        if text is None:
            issues.append(f"{name}: спека из списка «чистое устройство» отсутствует — правило не проверено")
            continue
        if name in EXEMPT:
            exempt += 1
            continue
        hits = [f for f in FORBIDDEN if f in text]
        checked += 1
        if hits:
            issues.append(f"{name}: спека проверяет первый вход, но вызывает {', '.join(hits)} — "
                          f"с засеянной сессией сценарий теряет смысл; нужен чистый контекст")
    stats = {"clean": len(CLEAN_DEVICE), "checked": checked, "exempt": exempt}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек «чистое устройство» {stats['clean']} | проверено {stats['checked']} | "
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
        print("::error::check_e2e_clean_device: нет папки спек — не измерено (rc=2)")
        return 2
    try:
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_clean_device: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_clean_device: {m}")
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

    GOOD = {
        "onboarding.spec.js": "await installBase(page); // свой онбординг",
        "pin-first-login.spec.js": "await installBase(page); await page.reload();",
        "no-pin.spec.js": "// LEGACY\ndescribe.skip('x', () => { await installBase(page); });",
    }
    case("правило соблюдено", GOOD, 0, ["нарушено: 0", "исключений 1"])

    BAD = dict(GOOD)
    BAD["onboarding.spec.js"] = "await loginWithPin(page);"
    case("онбординг с засеянной сессией", BAD, 1, ["onboarding.spec.js", "теряет смысл"])

    BAD2 = dict(GOOD)
    BAD2["pin-first-login.spec.js"] = "await seedSession(page);"
    case("первый вход с seedSession", BAD2, 1, ["pin-first-login.spec.js", "seedSession("])

    MISSING = {"onboarding.spec.js": "await installBase(page);"}
    case("спеки нет — правило не проверено", MISSING, 1, ["отсутствует"])

    total += 1
    line = receipt_line({"clean": 3, "checked": 2, "exempt": 1}, [])
    if "спек «чистое устройство» 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_clean_device --selftest: {ok}/{total} "
          f"(первый вход с loginWithPin/seedSession = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
