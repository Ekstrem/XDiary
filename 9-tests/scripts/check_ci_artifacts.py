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
    """Артефакт под `if: failure()` бесполезен, если до него нет шага, который реально падает.

    `TE-105` (`f465e473`): шаг «Repeat run (list)» — `continue-on-error: true`, поэтому падение тестов его
    не помечает; детектор — следующий шаг «Итог по спекам» с `if: always()`. Артефакты должны стоять
    **после** детектора. Если `if: always()`-шага нет вовсе — падение никому не видно, тоже дефект.
    """
    issues = []
    for name, text in sorted(files.items()):
        lines = text.splitlines()
        continue_rows = [i for i, ln in enumerate(lines) if CONTINUE.search(ln)]
        always_rows = [i for i, ln in enumerate(lines) if re.search(r"if:\s*\**\s*always\(\)", ln, re.I)]
        for i, line in enumerate(lines):
            if not UPLOAD.search(line):
                continue
            window = "\n".join(lines[max(0, i - 3):i + 4])
            if not FAIL_IF.search(window):
                continue
            prev_continue = [c for c in continue_rows if c < i]
            if not prev_continue:
                continue  # падению есть что пометить - артефакт сработает
            last_continue = max(prev_continue)
            detector = [a for a in always_rows if last_continue < a < i]
            if detector:
                continue  # детектор между continue-on-error и артефактами - порядок верный
            issues.append(
                "%s:%d — `upload-artifact` под `if: failure()` стоит раньше детектора падений "
                "(`if: always()`); до него единственный «виновный» шаг имеет `continue-on-error: true` "
                "(строка %d), поэтому артефакт не сохранится (класс TE-105)"
                % (name, i + 1, last_continue + 1)
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
  repeat:
    steps:
      - name: Repeat run
        continue-on-error: true
        run: npx playwright test
      - name: Итог по спекам
        if: always()
        run: python summary.py
      - name: Артефакты падения
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
      - name: Итог
        if: always()
        run: echo done
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
