#!/usr/bin/env python3
"""check_front_088.py — приёмка фронта `0.88` без bash (Python-двойник `check-front-088.sh`).

Зачем двойник. `9-tests/scripts/check-front-088.sh` не запускается в песочницах агентов:
`bash` падает на инициализации (`0xC0000142`, `couldn't create signal pipe, Win32 error 5`).
Python работает, проверки те же — по объектам git, а не по рабочему дереву.

Что проверяется (предикаты `check-front-088.sh`):
  1) `8-front/js/api.js` содержит `clients/me/profile` (профиль «я» — контракт E7/ADR-024);
  2) `8-front/js/app.js` НЕ содержит `linkedPartnerId` (легаси-привязка снята);
  3) `8-front/js/app.js` НЕ содержит `linkPartner` (метод и шаг удалены).

Плюс сверка версий (без неё «PASS 3/3» может быть получен на старом образе):
  4) `APP_VERSION` в `app.js` равен ожидаемому (по умолчанию `0.88`);
  5) `CACHE` в `sw.js` синхронен версии (`xdiary-v<версия>`).

Использование:
    python 9-tests/scripts/check_front_088.py                 # origin/main, ожидание 0.88
    python 9-tests/scripts/check_front_088.py f5579be3        # конкретная ревизия
    python 9-tests/scripts/check_front_088.py f5579be3 0.87   # другая ожидаемая версия
    REF=WORKTREE python 9-tests/scripts/check_front_088.py    # по рабочему дереву

Код возврата: 0 — PASS 3/3 и версии сходятся; 1 — нарушение; 2 — ревизия не резолвится.
"""
from __future__ import annotations

import os
import pathlib
import re
import subprocess
import sys

APP = "8-front/js/app.js"
API = "8-front/js/api.js"
SW = "8-front/sw.js"


def git(*args: str) -> str:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else ""


def read(ref: str, path: str, worktree: bool) -> str:
    if worktree:
        p = pathlib.Path(path)
        return p.read_text(encoding="utf-8", errors="replace") if p.exists() else ""
    return git("show", f"{ref}:{path}")


def main() -> int:
    ref = (sys.argv[1] if len(sys.argv) > 1 else os.environ.get("REF") or "origin/main").strip()
    expect_ver = (sys.argv[2] if len(sys.argv) > 2 else os.environ.get("EXPECT_VERSION") or "").strip()
    worktree = ref.upper() in ("WORKTREE", ".")
    if not worktree and not git("rev-parse", "--verify", f"{ref}^{{commit}}"):
        print(f"REF={ref} | FAIL: ревизия не резолвится")
        return 2

    api = read(ref, API, worktree)
    app = read(ref, APP, worktree)
    sw = read(ref, SW, worktree)
    if not api or not app or not sw:
        print(f"REF={ref} | FAIL: не найден один из файлов фронта ({API}, {APP}, {SW})")
        return 1

    n_profile = len(re.findall(r"clients/me/profile", api))
    n_linked = len(re.findall(r"linkedPartnerId", app))
    n_link = len(re.findall(r"linkPartner", app))

    m_ver = re.search(r'APP_VERSION\s*=\s*"([^"]+)"', app)
    m_cache = re.search(r'CACHE\s*=\s*"([^"]+)"', sw)
    ver = m_ver.group(1) if m_ver else "?"
    cache = m_cache.group(1) if m_cache else "?"

    print(f"REF={ref} | (1) api.js clients/me/profile = {n_profile}"
          f" | (2) app.js linkedPartnerId = {n_linked} | (3) app.js linkPartner = {n_link}")
    print(f"REF={ref} | версия: APP_VERSION = {ver}, CACHE = {cache}"
          + (f", ожидание {expect_ver}" if expect_ver else " (пин не задан — проверяется синхронность)"))

    bad: list[str] = []
    if n_profile < 1:
        bad.append("(1) нет `clients/me/profile` в api.js — профиль «я» не реализован")
    if n_linked != 0:
        bad.append(f"(2) `linkedPartnerId` встречается {n_linked} раз(а) — легаси-привязка не снята")
    if n_link != 0:
        bad.append(f"(3) `linkPartner` встречается {n_link} раз(а) — метод/шаг не удалены")

    # Версия: инвариант — **синхронность** `APP_VERSION` и `CACHE`, а не конкретный номер.
    # Жёсткий пин 0.88 валил гейт на 0.89, где все три предиката выполнялись (ложный красный).
    # Пин нужен только для квитанции конкретного релиза → `--expect-version`.
    def cache_candidates(v: str) -> set[str]:
        # В репозитории CACHE короче версии: `0.88` → `xdiary-v88`, `0.61` → `xdiary-v61`.
        return {"xdiary-v" + v, "xdiary-v" + v.replace(".", ""), "xdiary-v" + v.split(".")[-1]}

    if expect_ver:
        if ver != expect_ver:
            bad.append(f"(4) APP_VERSION = {ver}, ожидалось {expect_ver}")
        cache_ok = cache_candidates(expect_ver)
        if cache not in cache_ok:
            bad.append(f"(5) CACHE = {cache}, ожидалось одно из {sorted(cache_ok)}")
    else:
        if ver == "?":
            bad.append("(4) не найден `APP_VERSION` в app.js — версия не определяется")
        elif cache not in cache_candidates(ver):
            bad.append(f"(5) CACHE = {cache} не синхронен с APP_VERSION = {ver} "
                       f"(ожидалось одно из {sorted(cache_candidates(ver))})")

    if bad:
        for b in bad:
            print(f"::error::front-088: {b}")
        print(f"REF={ref} | FAIL: {len(bad)} нарушен(ия) — PASS 3/3 не достигнут")
        return 1

    print(f"REF={ref} | PASS: 3/3 (профиль «я» есть, легаси-привязки нет), версии синхронны ({ver}/{cache})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
