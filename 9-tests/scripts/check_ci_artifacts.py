#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_ci_artifacts.py` — артефакты падения не могут стоять под `if: failure()`, если падение
«проглатывает» шаг с `continue-on-error: true`.

Зачем: `TE-105` — красный `front-e2e-repeat` не оставлял ни лога (403), ни артефакта, потому что шаг
«Repeat run (list)» имеет `continue-on-error: true` (статус шага остаётся success), а шаг артефактов
стоит до шага «Итог по спекам» с `if: failure()`. К моменту артефактов ни один шаг не failed ⇒ `skipped`
⇒ диагностики нет. Замер: **0 артефактов у шести прогонов подряд**, включая красные.

Правило: если в workflow есть шаг с `continue-on-error: true` **раньше** шага `upload-artifact`
с `if: failure()` — предупредить (по умолчанию) или упасть (`--strict`), потому что падение тестов
не пометит прогон failed, и артефакт не сохранится.

Коды: `0` — нарушений нет (или только отчёт); `1` — есть нарушение в `--strict`; `2` — не измерено
(нет каталога `.github/workflows`).
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

UPLOAD = re.compile(r"uses:\s*actions/upload-artifact", re.I)
CONTINUE = re.compile(r"continue-on-error:\s*true", re.I)
FAIL_IF = re.compile(r"if:\s*.*failure\(\)", re.I)


def evaluate(files: dict[str, str]) -> list[str]:
    issues = []
    for name, text in sorted(files.items()):
        lines = text.splitlines()
        first_continue, upload_rows = None, []
        for i, line in enumerate(lines):
            if first_continue is None and CONTINUE.search(line):
                first_continue = i + 1
            if UPLOAD.search(line):
                # `if:` стоит ПЕРЕД `uses:` (так пишут в step-блоках) — смотрим назад и немного вперёд
                window = "\n".join(lines[max(0, i - 3):i + 4])
                if FAIL_IF.search(window):
                    upload_rows.append(i + 1)
        for row in upload_rows:
            if first_continue is not None and first_continue < row:
                issues.append(
                    "%s:%d — `upload-artifact` под `if: failure()`, но раньше стоит "
                    "`continue-on-error: true` (строка %d): падение тестов не пометит шаг failed, "
                    "артефакт не сохранится (класс TE-105)" % (name, row, first_continue)
                )
    return issues


def load(dirpath: pathlib.Path):
    if not dirpath.is_dir():
        return None, "нет каталога .github/workflows"
    out = {}
    for p in sorted(list(dirpath.glob("*.yml")) + list(dirpath.glob("*.yaml"))):
        out[p.name] = p.read_text(encoding="utf-8", errors="replace")
    return out, None


FIXTURES = {
    "ok.yml": """
jobs:
  test:
    steps:
      - name: Run tests
        run: dotnet test
      - name: Upload on failure
        if: failure()
        uses: actions/upload-artifact@v4
""",
    "defect.yml": """
jobs:
  repeat:
    steps:
      - name: Repeat run
        continue-on-error: true
        run: npx playwright test
      - name: Артефакты падения
        if: failure()
        uses: actions/upload-artifact@v4
      - name: Итог по спекам
        if: always()
        run: python summary.py
""",
    "no-artifacts.yml": """
jobs:
  build:
    steps:
      - name: build
        continue-on-error: true
        run: dotnet build
""",
}


def selftest() -> int:
    issues = evaluate(FIXTURES)
    ok = 0
    total = 3
    if len(issues) == 1 and "defect.yml" in issues[0]:
        ok += 1
        print("  [OK]   дефект (continue-on-error раньше артефактов) поймали")
    else:
        print("  [FAIL] ожидали одно нарушение в defect.yml, получили: %s" % issues)
    if not any("ok.yml" in i for i in issues):
        ok += 1
        print("  [OK]   норма не нарушает")
    else:
        print("  [FAIL] ложное срабатывание на ok.yml")
    if not any("no-artifacts.yml" in i for i in issues):
        ok += 1
        print("  [OK]   workflow без артефактов не нарушает")
    else:
        print("  [FAIL] ложное срабатывание на no-artifacts.yml")
    print("check_ci_artifacts --selftest: %d/%d (дефект / норма / без артефактов)" % (ok, total))
    return 0 if ok == total else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="артефакты падения должны срабатывать на реальном падении")
    ap.add_argument("--root", default=".")
    ap.add_argument("--strict", action="store_true", help="блокирующий режим (rc=1)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    files, err = load(pathlib.Path(a.root) / ".github" / "workflows")
    if err:
        print("::warning::check_ci_artifacts: %s — не измерено (rc=2)" % err)
        return 2
    issues = evaluate(files)
    level = "::error::" if a.strict else "::warning::"
    for m in issues:
        print("%scheck_ci_artifacts: %s" % (level, m))
    print("квитанция: workflow %d | нарушений %d%s"
          % (len(files), len(issues), " — блокирует (--strict)" if a.strict else " — отчёт, не блокирует"))
    return 1 if (issues and a.strict) else 0


if __name__ == "__main__":
    sys.exit(main())
