#!/usr/bin/env python3
"""check_front_specs_base.py — ни одна спека `front-e2e` не должна открывать приложение
без E2E-фикстуры.

Зачем: после FE-024 (`0.57`) фронт читает `localStorage.xdiary.apiBase` **только** если
`window.__XDIARY_E2E__ === true`; иначе ключ вычищается и `baseUrl()` отдаёт `DEFAULT_BASE`
(прод, VPS-1 — реальный дневник владельца). Флаг ставит `installBase()` через
`page.addInitScript`. Значит спека, которая делает `page.goto('/')`, не пройдя через
`installBase()` (сама или через `loginWithPin`/`seedSession`), уводит **браузерные** запросы
на прод. `global-setup.js` проверяет только `XDIARY_E2E_BASE_URL` (env) и такой случай
не ловит — он про API-базу теста, а не про то, куда смотрит страница.

Правило проверки: если в `*.spec.js` есть `page.goto(`, то в том же файле обязано быть
одно из: `installBase(`, `loginWithPin(`, `seedSession(`.

Использование (из корня репозитория):
    python 9-tests/scripts/check_front_specs_base.py           # origin/main
    REF=HEAD python 9-tests/scripts/check_front_specs_base.py
Код возврата: 0 — все спеки с навигацией используют фикстуру; 1 — есть нарушители.
"""
from __future__ import annotations

import os
import re
import subprocess
import sys

TESTS_DIR = "9-tests/RnD/front-e2e/tests"
FIXTURE_MARKERS = ("installBase(", "loginWithPin(", "seedSession(")


def git(*args: str) -> str:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else ""


def main() -> int:
    ref = (os.environ.get("REF") or "origin/main").strip()
    worktree_mode = ref.upper() in ("WORKTREE", ".")
    if not worktree_mode and not git("rev-parse", "--verify", f"{ref}^{{commit}}"):
        print(f"REF={ref} | FAIL: ревизия не резолвится")
        return 2
    if worktree_mode:
        import pathlib
        specs = sorted(str(x).replace("\\", "/") for x in pathlib.Path(TESTS_DIR).glob("*.spec.js"))
    else:
        specs = [p for p in git("ls-tree", "-r", "--name-only", ref, "--", TESTS_DIR).splitlines()
                 if p.endswith(".spec.js")]
    if not specs:
        print(f"REF={ref} | FAIL: не найдено ни одной спеки в {TESTS_DIR}")
        return 1

    offenders = []
    navigated = 0
    for spec in sorted(specs):
        if worktree_mode:
            import pathlib
            body = pathlib.Path(spec).read_text(encoding="utf-8", errors="replace")
        else:
            body = git("show", f"{ref}:{spec}")
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

    print(f"REF={ref} | PASS: все спеки с навигацией проходят через E2E-фикстуру (флаг __XDIARY_E2E__)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
