"""check_e2e_local_parity.py — локальный прогон и CI-прогон не разъезжаются.

Зачем: у набора два конфига — `playwright.config.js` (CI/живой стенд) и `playwright.local.config.js`
(двойник API + системный браузер). Разъезд между ними даёт самый неприятный класс: «локально зелено,
в CI красно» (или наоборот), и разбираться начинают не с того места. Проверяются только те свойства,
которые обязаны совпадать и не зависят от стенда:

1. фильтры по названиям (`--grep`, `--grep-invert`, `--project`, `--shard`) — запрещены в обоих
   (иначе часть набора молча не выполняется; см. `check_e2e_ci_filter.py`);
2. страж `reporters/executed-guard.js` подключён в обоих (иначе «всё пропущено» = зелёный прогон);
3. `globalSetup` подключён в обоих (предохранитель DO-017 «не прод» + ожидание стенда);
4. локальный конфиг включает **все** флаги, которые CI ставит в `env:` (кроме заведомо деструктивных
   вроде `XDIARY_CHANGE_PIN`) — иначе локально сценарии просто пропускаются;
5. `testDir` совпадает.

Коды: `0` — паритет; `1` — расхождение; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

FORBIDDEN = ("--grep-invert", "--grep", "--project", "--shard")
# флаги, которые НЕ обязаны повторяться локально: сценарий деструктивный (меняет PIN)
LOCAL_EXEMPT = {"XDIARY_CHANGE_PIN"}
FLAG = re.compile(r"XDIARY_[A-Z0-9_]+")


def flags_in(text):
    return set(FLAG.findall(text))


def skip_flags(specs_text):
    """Флаги, которые реально управляют `test.skip` в спеках."""
    out = set()
    for line in specs_text.splitlines():
        if "test.skip" in line:
            out.update(FLAG.findall(line))
    return out


def evaluate(ci_cfg, local_cfg, workflow_text, specs_text=""):
    """(issues, stats)."""
    issues = []
    for name, text in (("playwright.config.js", ci_cfg), ("playwright.local.config.js", local_cfg)):
        hits = [f for f in FORBIDDEN if f in text]
        if hits:
            issues.append(f"в `{name}` есть фильтр {', '.join(hits)} — часть набора не выполняется молча")
        if "executed-guard" not in text:
            issues.append(f"в `{name}` не подключён страж `reporters/executed-guard.js` — "
                          f"прогон, где все тесты пропущены, выглядел бы зелёным")
        if "globalSetup" not in text:
            issues.append(f"в `{name}` не подключён `globalSetup` — предохранитель DO-017 не выполнится")
    ta = re.search(r"testDir:\s*'([^']+)'", ci_cfg)
    tb = re.search(r"testDir:\s*'([^']+)'", local_cfg)
    if ta and tb and ta.group(1) != tb.group(1):
        issues.append(f"`testDir` различается: CI `{ta.group(1)}`, локально `{tb.group(1)}`")

    ci_flags = flags_in(workflow_text)
    local_flags = flags_in(local_cfg)
    # сверяем только флаги, которые ЗАСТАВЛЯЮТ спеку пропуститься: остальные (PIN, BASE_URL)
    # локально не нужны, у двойника свои значения
    skipping = skip_flags(specs_text) if specs_text else ci_flags
    required = {f for f in (ci_flags & skipping) if f not in LOCAL_EXEMPT}
    missing = sorted(required - local_flags)
    for f in missing:
        issues.append(f"локальный конфиг не включает `{f}`, который CI ставит в `env:` — "
                      f"локально сценарии будут пропущены")
    extra = sorted(local_flags - ci_flags - LOCAL_EXEMPT)
    stats = {"ci_flags": len(ci_flags), "local_flags": len(local_flags),
             "missing": len(missing), "extra": len(extra)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: флагов CI {stats['ci_flags']} | локальных {stats['local_flags']} | "
            f"не включено локально {stats['missing']} | лишних локальных {stats['extra']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    ci, local = base / "playwright.config.js", base / "playwright.local.config.js"
    wf = pathlib.Path(a.root) / ".github" / "workflows" / "ci-front.yml"
    if not ci.exists() or not local.exists():
        print("::error::check_e2e_local_parity: нет одного из конфигов — не измерено (rc=2)")
        return 2
    try:
        ci_text = ci.read_text(encoding="utf-8", errors="replace")
        local_text = local.read_text(encoding="utf-8", errors="replace")
        wf_text = wf.read_text(encoding="utf-8", errors="replace") if wf.exists() else ""
        specs_text = "\n".join(p.read_text(encoding="utf-8", errors="replace")
                               for p in sorted((base / "tests").glob("*.spec.js")))
    except OSError as e:
        print(f"::error::check_e2e_local_parity: не читается вход: {e} — не измерено (rc=2)")
        return 2
    if not wf_text:
        print("::error::check_e2e_local_parity: нет ci-front.yml — не с чем сверять флаги (rc=2)")
        return 2
    issues, stats = evaluate(ci_text, local_text, wf_text, specs_text)
    for m in issues:
        print(f"::error::check_e2e_local_parity: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    CI = ("testDir: './tests',\n"
          "globalSetup: './tests/global-setup.js',\n"
          "reporter: [['list'], ['./reporters/executed-guard.js']],\n")
    LOCAL = ("testDir: './tests',\n"
             "for (const flag of ['XDIARY_BUSY', 'XDIARY_STALENOTE']) {}\n"
             "globalSetup: './tests/global-setup.js',\n"
             "reporter: [['list'], ['./reporters/executed-guard.js']],\n")
    WF = ('env:\n  XDIARY_BUSY: "1"\n  XDIARY_STALENOTE: "1"\n  XDIARY_E2E_PIN: "1234"\n'
          '  XDIARY_CHANGE_PIN: "1"\n')
    SPECS = ("test.skip(!process.env.XDIARY_BUSY, 'нужен XDIARY_BUSY=1');\n"
             "test.skip(!process.env.XDIARY_STALENOTE, 'нужен XDIARY_STALENOTE=1');\n")

    def case(name, ci, local, wf, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(ci, local, wf, SPECS)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("паритет при локальных флагах", CI, LOCAL, WF, 0, ["нарушено: 0", "не включено локально 0"])
    case("локально не включён флаг CI", CI, LOCAL.replace("'XDIARY_BUSY', ", ""), WF, 1,
         ["не включает `XDIARY_BUSY`"])
    case("деструктивный флаг не требуется локально", CI, LOCAL, WF, 0, ["нарушено: 0"])
    case("нет стража в локальном конфиге", CI,
         LOCAL.replace(", ['./reporters/executed-guard.js']", ""), WF, 1, ["executed-guard"])
    case("нет globalSetup в CI", CI.replace("globalSetup: './tests/global-setup.js',\n", ""), LOCAL, WF, 1,
         ["globalSetup"])
    case("grep в локальном конфиге", CI, LOCAL + "// --grep x\n", WF, 1, ["--grep"])

    total += 1
    line = receipt_line({"ci_flags": 2, "local_flags": 2, "missing": 0, "extra": 0}, [])
    if "флагов CI 2" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_local_parity --selftest: {ok}/{total} "
          f"(локальный конфиг не повторяет CI = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
