#!/usr/bin/env python3
"""check_front_specs_base.py — спека `front-e2e` не открывает приложение без E2E-фикстуры,
и фронт обязан поддерживать её контракт.

Зачем. После FE-024 (`0.57`) фронт читает `localStorage.xdiary.apiBase` **только** если
`window.__XDIARY_E2E__ === true`; иначе ключ вычищается и `baseUrl()` отдаёт `DEFAULT_BASE`
(`8-front/js/api.js:12` — прод VPS-1, реальный дневник владельца). Флаг ставит `installBase()`
через `page.addInitScript` (`9-tests/RnD/front-e2e/tests/api.js:72-78`), а `seedSessionOnce`
зовёт её **первым шагом** до `page.goto` (`:145`). Значит:

  1) спека, которая делает `page.goto('/')`, не пройдя через `installBase()` (сама или через
     `loginWithPin`/`seedSession`), уводит **браузерные** запросы на прод;
  2) если фронт перестанет читать `__XDIARY_E2E__`, `installBase` станет бесполезной —
     тесты молча уедут на `DEFAULT_BASE`, а `global-setup.js` этого не увидит
     (он валидирует `XDIARY_E2E_BASE_URL`, то есть API-базу теста, а не адрес страницы).

Использование (из корня репозитория):
    python 9-tests/scripts/check_front_specs_base.py                # по origin/main
    REF=<sha> python 9-tests/scripts/check_front_specs_base.py      # по ревизии
    REF=WORKTREE python 9-tests/scripts/check_front_specs_base.py   # по рабочему дереву
Код возврата: 0 — обе части в порядке; 1 — нарушение; 2 — ревизия не резолвится.
"""
from __future__ import annotations

import os
import pathlib
import re
import subprocess
import sys

TESTS_DIR = "9-tests/RnD/front-e2e/tests"
FRONT_API = "8-front/js/api.js"
FIXTURE_MARKERS = ("installBase(", "loginWithPin(", "seedSession(")
FLAG = "__XDIARY_E2E__"


def git(*args: str) -> str:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else ""


def read_file(ref: str, path: str, worktree_mode: bool) -> str:
    if worktree_mode:
        p = pathlib.Path(path)
        return p.read_text(encoding="utf-8", errors="replace") if p.exists() else ""
    return git("show", f"{ref}:{path}")


def main() -> int:
    ref = (os.environ.get("REF") or "origin/main").strip()
    worktree_mode = ref.upper() in ("WORKTREE", ".")
    if not worktree_mode and not git("rev-parse", "--verify", f"{ref}^{{commit}}"):
        print(f"REF={ref} | FAIL: ревизия не резолвится")
        return 2

    if worktree_mode:
        specs = sorted(str(x).replace("\\", "/") for x in pathlib.Path(TESTS_DIR).glob("*.spec.js"))
    else:
        specs = [p for p in git("ls-tree", "-r", "--name-only", ref, "--", TESTS_DIR).splitlines()
                 if p.endswith(".spec.js")]
    if not specs:
        print(f"REF={ref} | FAIL: не найдено ни одной спеки в {TESTS_DIR}")
        return 1

    # Часть 1: спека с навигацией обязана проходить через фикстуру.
    offenders: list[str] = []
    navigated = 0
    for spec in sorted(specs):
        body = read_file(ref, spec, worktree_mode)
        if not re.search(r"page\.goto\(", body):
            continue
        navigated += 1
        if not any(m in body for m in FIXTURE_MARKERS):
            offenders.append(spec)

    print(f"REF={ref} | спек всего: {len(specs)}, из них с page.goto: {navigated}")
    if offenders:
        for s in offenders:
            print(f"::error::front-e2e: спека открывает приложение без E2E-фикстуры (риск прода) — {s}")
        print(f"REF={ref} | FAIL: {len(offenders)} спек(и) без installBase/loginWithPin/seedSession")
        return 1

    # Часть 2: контракт фикстуры обязан жить во фронте (FE-024, подтверждён @FE 2026-11-15 06:15).
    front_body = read_file(ref, FRONT_API, worktree_mode)
    if FLAG not in front_body:
        print(f"::error::front-e2e: контракт фикстуры снят во фронте — {FRONT_API} не читает {FLAG}")
        print(f"REF={ref} | FAIL: фронт не поддерживает E2E-флаг (тесты уйдут на DEFAULT_BASE = прод)")
        return 1

    print(f"REF={ref} | PASS: все спеки с навигацией через E2E-фикстуру, контракт {FLAG} во фронте на месте")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
