#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_run_receipt.py` — превращает лог прогона `front-e2e` в квитанцию с `REF` и числами.

Зачем: четыре карточки (`TE-050`, `TE-053`, `TE-054`, `TE-104`) закрываются «строкой прогона» (`REF` +
что исполнено + сколько упало). Сейчас эта строка собирается руками по логу, и в чате уже были случаи,
когда «прогон зелёный» читалось с лога, где `front-e2e` был `skipped`. Гейт делает это машинно.

Вход: файл лога (`gh run view <id> --log > run.log`) или `--stdin`.
Выход: одна строка квитанции + `rc`.

Коды: `0` — прогон исполнялся и падений нет; `1` — исполнялся, есть падения; `2` — лог не содержит
признаков прогона (не измерено: «зелёный на пустоте» здесь был бы ошибкой).

Примеры:
    python 9-tests/scripts/check_run_receipt.py --log run.log --run 1099 --sha bf5fc002
    gh run view 1099 --log | python 9-tests/scripts/check_run_receipt.py --stdin --run 1099
"""
from __future__ import annotations

import argparse
import io
import re
import sys

# Playwright печатает `✓`/`✘` (и `×` в некоторых версиях) перед названием теста.
PASS = re.compile(r"[✓✔]\s+(.+)")
FAIL = re.compile(r"[✘✗×]\s+(.+)")
SPEC_FILE = re.compile(r"([\w.-]+\.spec\.js)")
SUMMARY_RUN = re.compile(r"Running\s+(\d+)\s+tests?", re.I)
SUMMARY_RESULT = re.compile(r"(\d+)\s+(passed|failed|flaky|skipped)", re.I)

# VSTest/`dotnet test` (TE-050): `Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
VSTEST = re.compile(r"(Passed|Failed)!\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),"
                    r"\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)", re.I)
# шаг CI: «Front E2E» исполняется, а «front-e2e … skipped» — нет
STEP_SKIPPED = re.compile(r"front-e2e.*(?:skipped|Skipped)", re.I)


def parse(text: str) -> dict:
    """Вытаскивает из лога: исполнялся ли прогон, сколько упало, какие спеки упомянуты."""
    passed, failed = [], []
    specs = set()
    running = None
    counts = {}
    vstest = None
    for line in text.splitlines():
        mv = VSTEST.search(line)
        if mv:
            vstest = {"failed": int(mv.group(2)), "passed": int(mv.group(3)),
                      "skipped": int(mv.group(4)), "total": int(mv.group(5))}
        m = PASS.search(line)
        if m:
            passed.append(m.group(1).strip()[:120])
        m = FAIL.search(line)
        if m:
            failed.append(m.group(1).strip()[:120])
        for s in SPEC_FILE.findall(line):
            specs.add(s)
        m = SUMMARY_RUN.search(line)
        if m:
            running = int(m.group(1))
        for m in SUMMARY_RESULT.finditer(line):
            # «1 passed» / «2 failed» — берём максимум по классу: у Playwright итог идёт последним
            n, kind = int(m.group(1)), m.group(2).lower()
            counts[kind] = max(counts.get(kind, 0), n)
    measured = bool(vstest or passed or failed or running or counts.get("passed")
                    or counts.get("failed"))
    return {
        "measured": measured,
        "passed": len(passed),
        "failed": len(failed),
        "running": running,
        "counts": counts,
        "specs": sorted(specs),
        "failed_names": failed[:10],
        "vstest": vstest,
        "skipped_step": bool(STEP_SKIPPED.search(text)),
    }


def totals(result: dict) -> tuple[int, int]:
    """(passed, failed) — по сводке Playwright, если она есть; иначе по строкам `✓`/`✘`.

    Сводка авторитетнее: `1 failed` печатается и тогда, когда подробностей в логе меньше
    (retry, `--reporter=dot`). Но если сводки нет (усечённый лог) — считаем по строкам.
    """
    if result.get("vstest"):
        return result["vstest"]["passed"], result["vstest"]["failed"]
    passed = result["counts"].get("passed") or result["passed"]
    failed = result["counts"].get("failed") or result["failed"]
    return passed, failed


def receipt(result: dict, run: str, sha: str) -> str:
    passed, failed = totals(result)
    src = "dotnet-test" if result.get("vstest") else "playwright"
    parts = [
        "REF=%s" % (sha or "нет-ref"),
        "run=%s" % (run or "локальный"),
        "спек в логе %d" % len(result["specs"]),
        "тестов исполнено %s" % (result["running"] if result["running"] is not None else "?"),
        "источник %s" % src,
        "passed %d" % passed,
        "failed %d" % failed,
    ]
    return "квитанция прогона: " + " | ".join(parts)


def verdict(result: dict, run: str, sha: str) -> tuple[int, list[str]]:
    lines = []
    vs = result.get("vstest")
    if vs and vs["total"] == 0:
        lines.append("::warning::НЕ ИЗМЕРЕНО: `dotnet test` не исполнил ни одного теста (Total: 0)")
        lines.append(receipt(result, run, sha))
        return 2, lines
    if not result["measured"]:
        lines.append("::warning::не измерено: в логе нет признаков прогона (ни ✓/✘, ни «Running N tests»)")
        if result["skipped_step"]:
            lines.append("::warning::в логе видно, что шаг `front-e2e` **skipped** — это не прогон")
        lines.append("квитанция: НЕ ИЗМЕРЕНО | " + receipt(result, run, sha).replace("квитанция прогона: ", ""))
        return 2, lines
    if result["skipped_step"] and not (result["passed"] or result["failed"]):
        lines.append("::warning::шаг `front-e2e` skipped, тестов нет — зелёным это не считать")
        lines.append(receipt(result, run, sha))
        return 2, lines
    _passed, failed_total = totals(result)
    for name in result["failed_names"]:
        lines.append("::error::упал: %s" % name)
    if failed_total and not result["failed_names"]:
        lines.append("::error::падений %d (имена в логе не найдены — смотри сводку)" % failed_total)
    lines.append(receipt(result, run, sha))
    return (1 if failed_total else 0), lines


FIXTURE_PASS = """
Running 3 tests using 1 worker
✓  E2E: FE-014 — дефолты записи (9.1s)
✓  E2E: FE-016 — окно после клика (4.0s)
✓  E2E: TE-104 — ошибка API на старте (2.0s)
3 passed (6.2s)
"""

FIXTURE_FAIL = """
Running 3 tests using 1 worker
✓  E2E: FE-014 — дефолты записи (9.1s)
✘  E2E: FE-016 — окно после клика (30.0s)
2 failed
"""

FIXTURE_VSTEST_PASS = """
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 1 s
"""

FIXTURE_VSTEST_FAIL = """
Failed!  - Failed:     2, Passed:     2, Skipped:     0, Total:     4, Duration: 1 s
"""

FIXTURE_VSTEST_ZERO = """
Passed!  - Failed:     0, Passed:     0, Skipped:     0, Total:     0, Duration: 0 s
"""

FIXTURE_JUNK = """
Set up job
Run actions/checkout@v4
front-e2e  skipped
Post Run actions/checkout@v4
"""


def selftest() -> int:
    ok = total = 0

    def case(name, text, want_rc, must=()):
        nonlocal ok, total
        total += 1
        res = parse(text)
        rc, lines = verdict(res, "1", "abc1234")
        flat = "\n".join(lines)
        miss = [m for m in must if m not in flat]
        if rc == want_rc and not miss:
            ok += 1
            print("  [OK]   %s (rc=%d)" % (name, rc))
        else:
            print("  [FAIL] %s: rc=%d (ждали %d), нет %s" % (name, rc, want_rc, miss))

    case("норма: три теста зелёные", FIXTURE_PASS, 0, ["passed 3", "failed 0", "спек в логе"])
    case("дефект: один упал", FIXTURE_FAIL, 1, ["упал:", "failed 2"])
    case("пустой лог (skipped) — не измерено", FIXTURE_JUNK, 2, ["НЕ ИЗМЕРЕНО", "skipped"])
    case("dotnet test: 4 passed", FIXTURE_VSTEST_PASS, 0, ["passed 4", "источник dotnet-test"])
    case("dotnet test: 2 failed", FIXTURE_VSTEST_FAIL, 1, ["failed 2", "падений 2"])
    case("dotnet test: Total 0 - не измерено", FIXTURE_VSTEST_ZERO, 2, ["НЕ ИЗМЕРЕНО", "Total: 0"])
    print("check_run_receipt --selftest: %d/%d (playwright: норма/падение/skipped; dotnet test: 0/1/2)"
          % (ok, total))
    return 0 if ok == total else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="квитанция прогона front-e2e из лога")
    ap.add_argument("--log", help="файл лога (`gh run view <id> --log > run.log`)")
    ap.add_argument("--stdin", action="store_true", help="читать лог из stdin")
    ap.add_argument("--run", default="", help="номер прогона")
    ap.add_argument("--sha", default="", help="REF (короткий SHA коммита прогона)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)

    if a.selftest:
        return selftest()
    if a.stdin:
        text = sys.stdin.read()
    elif a.log:
        text = io.open(a.log, encoding="utf-8", errors="replace").read()
    else:
        print("::error::check_run_receipt: нужен --log <файл> или --stdin — не измерено (rc=2)")
        return 2
    result = parse(text)
    rc, lines = verdict(result, a.run, a.sha)
    for line in lines:
        print(line)
    return rc


if __name__ == "__main__":
    sys.exit(main())
