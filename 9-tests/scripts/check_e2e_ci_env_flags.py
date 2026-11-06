"""check_e2e_ci_env_flags.py — флаг, без которого спека скипается, обязан быть выставлен в CI.

Реальный факт (2026-11-06): `change-pin.spec.js` выполняется только при `XDIARY_CHANGE_PIN=1`, а в
`ci-front.yml` (джоба `front-e2e`) этого флага **нет** — значит в браузерном прогоне тест **всегда**
пропускается, и гейт физически не может подтвердить PIN-путь. Это «зелено на пустоте», только с другой
стороны: не гейт без шагов, а **тест без запуска**.

Правило: если спека содержит skip-guard вида `test.skip(!process.env.XDIARY_X, …)` /
`process.env.XDIARY_X === '1'`, то в `ci-front.yml` (или в env браузерной джобы) должен быть `XDIARY_X`.
Исключения — только через `KNOWN_GAPS` с причиной и **печатаются в квитанции числом**.

Коды: `0` — все нужные флаги выставлены (или исключения объявлены); `1` — есть флаг, который спека требует,
а CI не ставит; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

FLAG = re.compile(r"XDIARY_[A-Z0-9_]+")
# флаг «включён», если он в guard'е на пропуск: !process.env.F или process.env.F === '1'
REQUIRE = re.compile(r"(?:!process\.env\.(XDIARY_[A-Z0-9_]+)|process\.env\.(XDIARY_[A-Z0-9_]+)\s*===\s*['\"]1['\"])")

# Исключения: флаг нужен спеке, но в CI не выставлен — признано владельцем зоны (@DO), видно числом.
KNOWN_GAPS = {
    "XDIARY_CHANGE_PIN":
        "не задан в ci-front.yml (HANDOFF блокер #4, зона @DO): без него `change-pin` скипается в каждом прогоне",
}


def required_flags(specs):
    """{flag: [спеки, которые его требуют]}."""
    need = {}
    for name, text in specs.items():
        for m in REQUIRE.finditer(text):
            flag = m.group(1) or m.group(2)
            need.setdefault(flag, []).append(name)
    return need


def evaluate(specs, workflow_text):
    need = required_flags(specs)
    set_flags = set(FLAG.findall(workflow_text))
    issues, known = [], []
    for flag in sorted(need):
        if flag in set_flags:
            continue
        if flag in KNOWN_GAPS:
            known.append(flag)
            continue
        issues.append(f"флаг `{flag}` нужен спекам ({', '.join(sorted(set(need[flag])))}), "
                      f"но в `ci-front.yml` не выставлен — эти тесты будут пропущены в каждом прогоне")
    unused = sorted(f for f in set_flags if f.startswith("XDIARY_") and f not in need
                    and f not in ("XDIARY_E2E_BASE_URL", "XDIARY_E2E_PIN", "XDIARY_E2E_TEST_OTP"))
    stats = {"required": len(need), "set": len(set_flags), "known": len(known), "unused": len(unused)}
    return issues, stats, unused


def receipt_line(stats, issues):
    return (f"квитанция: флагов требуется {stats['required']} | выставлено в CI {stats['set']} | "
            f"без флага: {len(issues)} | известных исключений: {stats.get('known', 0)} | "
            f"выставлено, но не требуется: {stats.get('unused', 0)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    root = pathlib.Path(a.root)
    tests = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    wf = root / ".github" / "workflows" / "ci-front.yml"
    if not tests.is_dir():
        print("::error::check_e2e_ci_env_flags: нет папки спек — не измерено (rc=2)")
        return 2
    if not wf.is_file():
        print("::error::check_e2e_ci_env_flags: нет ci-front.yml — не с чем сверять флаги (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats, _unused = evaluate(specs, wf.read_text(encoding="utf-8", errors="replace"))
    for m in issues:
        print(f"::error::check_e2e_ci_env_flags: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, specs, wf, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats, _u = evaluate(specs, wf)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:210]}")

    specs = {"a.spec.js": "test.skip(!process.env.XDIARY_BUSY, 'нужен XDIARY_BUSY=1');"}
    case("флаг есть в CI — зелёный", specs, "env:\n  XDIARY_BUSY: '1'\n", 0,
         ["флагов требуется 1", "без флага: 0"])

    case("флага нет в CI — красный", specs, "env:\n  XDIARY_OTHER: '1'\n", 1,
         ["XDIARY_BUSY", "пропущены в каждом прогоне"])

    case("=== '1' тоже считается требованием",
         {"b.spec.js": "const on = process.env.XDIARY_READPARITY === '1';"},
         "env: {}\n", 1, ["XDIARY_READPARITY"])

    total += 1
    line = receipt_line({"required": 3, "set": 2, "known": 1, "unused": 0}, [])
    if "требуется 3" in line and "известных исключений: 1" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_ci_env_flags --selftest: {ok}/{total} "
          f"(флаг, без которого спека скипается, но которого нет в CI = rc=1: «тест без запуска»)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
