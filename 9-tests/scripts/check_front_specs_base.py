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


def resolve_default_ref() -> str:
    """Ревизия по умолчанию: первый существующий из кандидатов.

    Зачем не «origin/main жёстко»: в CI (`actions/checkout` без `fetch-depth`) и на
    `pull_request` remote-tracking `origin/main` может отсутствовать — тогда гейт падал бы
    `rc=2` на каждом PR, то есть **ложным красным** (ровно тот класс, который мы устраняем).
    Явно заданный `REF` проверяется строго: неверный — `rc=2`, без тихого фолбэка.
    """
    for cand in ("origin/main", "gh/main", "HEAD"):
        if git("rev-parse", "--verify", f"{cand}^{{commit}}"):
            return cand
    return "origin/main"


def offenders_of(specs: dict) -> tuple:
    """По содержанию спек: (число с навигацией, [нарушители]).

    Вынесено из `main`, чтобы `--selftest` проверял **логику** без репозитория
    (норма @TL `14:30`: кейс не должен зависеть от состояния дерева).
    """
    navigated, bad = 0, []
    for name, body in sorted(specs.items()):
        if not re.search(r"page\.goto\(", body):
            continue
        navigated += 1
        if not any(m in body for m in FIXTURE_MARKERS):
            bad.append(name)
    return navigated, bad


def selftest() -> int:
    cases = [
        # (спеки, ожидаемое число с goto, ожидаемые нарушители, метка)
        ({"a.spec.js": "page.goto('/')"}, 1, ["a.spec.js"], "навигация без фикстуры — дефект"),
        ({"a.spec.js": "installBase(); page.goto('/')"}, 1, [], "фикстура перед goto — норма"),
        ({"a.spec.js": "seedSession(); page.goto('/')"}, 1, [], "seedSession — тоже фикстура"),
        ({"a.spec.js": "test('x', () => {})"}, 0, [], "без навигации — не считается"),
        ({"a.spec.js": "page.goto('/')", "b.spec.js": "loginWithPin(); page.goto('/')"}, 2, ["a.spec.js"],
         "смешанный набор: нарушитель ровно один"),
    ]
    ok = 0
    for specs, want_nav, want_bad, label in cases:
        nav, bad = offenders_of(specs)
        if (nav, bad) == (want_nav, want_bad):
            ok += 1
        else:
            print(f"  [FAIL] {label}: got=({nav}, {bad}), want=({want_nav}, {want_bad})")
    print(f"check_front_specs_base --selftest: {ok}/{len(cases)} (красный контроль на каждом кейсе)")
    return 0 if ok == len(cases) else 1


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "--selftest":
        return selftest()
    explicit = (os.environ.get("REF") or "").strip()
    ref = explicit or resolve_default_ref()
    worktree_mode = ref.upper() in ("WORKTREE", ".")
    if not worktree_mode and not git("rev-parse", "--verify", f"{ref}^{{commit}}"):
        hint = "" if explicit else " (и ни один из origin/main, gh/main, HEAD не резолвится)"
        print(f"REF={ref} | FAIL: ревизия не резолвится{hint}")
        return 2
    if not explicit and not worktree_mode:
        print(f"ревизия по умолчанию: {ref} (REF не задан)")

    if worktree_mode:
        specs = sorted(str(x).replace("\\", "/") for x in pathlib.Path(TESTS_DIR).glob("*.spec.js"))
    else:
        specs = [p for p in git("ls-tree", "-r", "--name-only", ref, "--", TESTS_DIR).splitlines()
                 if p.endswith(".spec.js")]
    if not specs:
        print(f"REF={ref} | FAIL: не найдено ни одной спеки в {TESTS_DIR}")
        return 1

    # Часть 1: спека с навигацией обязана проходить через фикстуру.
    navigated, offenders = offenders_of({sp: read_file(ref, sp, worktree_mode) for sp in sorted(specs)})

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
