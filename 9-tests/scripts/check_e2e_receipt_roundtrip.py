"""check_e2e_receipt_roundtrip.py — артефакт↔квитанция: страж пишет, квитанция читает.

Зачем: `reporters/executed-guard.js` пишет `test-results/receipt.txt` (чтобы числа прогона уезжали
в артефакте, а не в логе джобы), а `9-tests/scripts/e2e_receipt.py` его разбирает. Проверять эти две
вещи по отдельности недостаточно: любая правка формата с одной стороны (или опечатка в пути) ломает
цепочку, и числа «не измерено» появляются уже после прогона — то есть в самый неподходящий момент.

Здесь цепочка проверяется целиком, без браузера и без сети:
    страж (события passed/failed/skipped) → `test-results/receipt.txt` → `e2e_receipt.py --input`.

Сценарии:
    1. один упавший тест   → квитанция `rc=1` и называет упавшую спеку;
    2. все прошли          → квитанция `rc=0`, `failed 0`;
    3. все пропущены      → квитанция `rc=2` («не измерено»), а не «зелено».

Коды: `0` — все сценарии как ожидалось; `1` — расхождение; `2` — не измерено (нет node/python).
"""

import argparse
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PROBE = """const Guard = require('./reporters/executed-guard.js');
const g = new Guard();
const mk = (file, line, title) => ({ title, location: { file, line },
  parent: { project: () => ({ name: 'chromium' }) } });
const EVENTS = JSON.parse(process.env.PROBE_EVENTS || '[]');
for (const e of EVENTS) g.onTestEnd(mk(e.file, e.line, e.title), { status: e.status, retry: e.retry || 0 });
g.onEnd({ status: 'passed' });
console.log('PROBE_DONE');
"""


def run_probe(tmp, events):
    env = {"PROBE_EVENTS": json.dumps(events), "PATH": ""}
    import os

    env["PATH"] = os.environ.get("PATH", "")
    node = shutil.which("node")
    return subprocess.run(
        [node, "probe.js"],
        cwd=str(tmp),
        env={**os.environ, **env},
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def build_harness(tmp, guard_src):
    (tmp / "reporters").mkdir(parents=True, exist_ok=True)
    shutil.copyfile(guard_src, tmp / "reporters" / "executed-guard.js")
    (tmp / "probe.js").write_text(PROBE, encoding="utf-8")


def receipt_of(tmp, receipt_py):
    out = tmp / "test-results" / "receipt.txt"
    if not out.exists():
        return None, None, "файл test-results/receipt.txt не создан"
    r = subprocess.run(
        [sys.executable, str(receipt_py), "--input", str(out)],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return r.returncode, (r.stdout or ""), None


CASES = [
    ("один упавший тест", [
        {"file": "onboarding.spec.js", "line": 9, "title": "E2E: онбординг", "status": "passed"},
        {"file": "partner-edit.spec.js", "line": 12, "title": "E2E: партнёр", "status": "failed"},
        {"file": "change-pin.spec.js", "line": 8, "title": "E2E: смена PIN", "status": "skipped"},
    ], 1, ["упавшая спека: partner-edit.spec.js", "passed 1", "failed 1", "skipped 1"]),
    ("все прошли", [
        {"file": "a.spec.js", "line": 1, "title": "E2E: a", "status": "passed"},
        {"file": "b.spec.js", "line": 1, "title": "E2E: b", "status": "passed"},
    ], 0, ["failed 0", "passed 2"]),
    ("все пропущены", [
        {"file": "c.spec.js", "line": 1, "title": "E2E: c", "status": "skipped"},
    ], 2, ["ни один тест не исполнялся"]),
]


def evaluate(tmp, guard_src, receipt_py):
    """(issues, stats)."""
    build_harness(tmp, guard_src)
    issues, passed = [], 0
    for name, events, want_rc, must in CASES:
        shutil.rmtree(tmp / "test-results", ignore_errors=True)
        probe = run_probe(tmp, events)
        if "PROBE_DONE" not in (probe.stdout or ""):
            issues.append(f"сценарий «{name}»: страж не отработал ({probe.returncode}): "
                          f"{(probe.stderr or probe.stdout or '').strip()[:160]}")
            continue
        rc, out, err = receipt_of(tmp, receipt_py)
        if err:
            issues.append(f"сценарий «{name}»: {err}")
            continue
        miss = [m for m in must if m not in out]
        if rc == want_rc and not miss:
            passed += 1
        else:
            issues.append(f"сценарий «{name}»: rc={rc} (ждали {want_rc}), нет {miss}; вывод: "
                          f"{(out or '').strip().splitlines()[-1][:160] if out else '(пусто)'}")
    stats = {"cases": len(CASES), "passed": passed}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: сценариев артефакт→квитанция {stats['cases']} | как ожидалось {stats['passed']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    base = pathlib.Path(a.root) / "9-tests"
    guard = base / "RnD" / "front-e2e" / "reporters" / "executed-guard.js"
    receipt_py = base / "scripts" / "e2e_receipt.py"
    if not guard.exists() or not receipt_py.exists():
        print("::error::check_e2e_receipt_roundtrip: нет стража или квитанции — не измерено (rc=2)")
        return 2
    if not shutil.which("node"):
        print("::error::check_e2e_receipt_roundtrip: нет node — не измерено (rc=2)")
        return 2
    tmp = pathlib.Path(tempfile.gettempdir()) / f"receipt-roundtrip-{__import__('os').getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    try:
        issues, stats = evaluate(tmp, guard, receipt_py)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    for m in issues:
        print(f"::error::check_e2e_receipt_roundtrip: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    """Самопроверка: подложный страж без записи файла обязан дать нарушения."""
    ok = total = 0
    tmp = pathlib.Path(tempfile.gettempdir()) / f"roundtrip-self-{__import__('os').getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    try:
        receipt_py = pathlib.Path(__file__).resolve().parent / "e2e_receipt.py"
        good = tmp / "good-guard.js"
        good.write_text(
            "class G { constructor(){this.p=0;this.f=0;this.k=0;this.l=[];}"
            " onTestEnd(t,r){const m=r.status==='passed'?'\\u2713':r.status==='skipped'?'-':'\\u2717';"
            " this.l.push(m+'  1 [chromium] \\u203a '+t.location.file+':'+t.location.line+' \\u203a '+t.title);"
            " if(r.status==='passed')this.p++; else if(r.status==='skipped')this.k++; else this.f++;}"
            " onEnd(){const fs=require('fs'),p=require('path');const d=p.resolve(__dirname,'..','test-results');"
            " fs.mkdirSync(d,{recursive:true});"
            " fs.writeFileSync(p.join(d,'receipt.txt'), this.l.join('\\n')+'');}}"
            "module.exports=G;\n",
            encoding="utf-8",
        )
        tmp2 = tmp / "case1"
        tmp2.mkdir()
        issues, stats = evaluate(tmp2, good, receipt_py)
        total += 1
        if stats and stats["passed"] >= 2:
            ok += 1
        else:
            print(f"  [FAIL] корректный страж: {stats}, {issues}")

        bad = tmp / "bad-guard.js"
        bad.write_text("class G { onTestEnd(){} onEnd(){} }\nmodule.exports=G;\n", encoding="utf-8")
        tmp3 = tmp / "case2"
        tmp3.mkdir()
        issues2, stats2 = evaluate(tmp3, bad, receipt_py)
        total += 1
        if issues2 and stats2 and stats2["passed"] == 0:
            ok += 1
        else:
            print("  [FAIL] страж без записи файла не дал нарушений")

        total += 1
        line = receipt_line({"cases": 3, "passed": 3}, [])
        if "сценариев артефакт→квитанция 3" in line and "нарушено: 0" in line:
            ok += 1
        else:
            print(f"  [FAIL] текст квитанции: {line}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print(f"check_e2e_receipt_roundtrip --selftest: {ok}/{total} "
          f"(страж без записи файла = нарушения)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
