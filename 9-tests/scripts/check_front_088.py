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


def newest_ref(candidates=("origin/main", "gh/main", "HEAD")) -> str:
    """Самый СВЕЖИЙ ref, а не первый по имени (@AR `09:40`).

    Класс ошибки: гейт читал `origin/main`, который в общем дереве отстаёт от явно
    `fetch`-нутого `gh/main`, и краснел на уже исправленном (как пин `0.88` на `0.89`).
    Берём ref с максимальным временем коммита.
    """
    best, best_ts = "", -1
    for cand in candidates:
        if not git("rev-parse", "--verify", "--quiet", cand):
            continue
        raw = git("log", "-1", "--format=%ct", cand).strip()
        try:
            ts = int(raw or 0)
        except ValueError:
            ts = 0
        if ts > best_ts:
            best, best_ts = cand, ts
    return best or "HEAD"


def describe_ref(ref: str) -> str:
    sha = git("rev-parse", "--short", ref).strip()
    when = git("log", "-1", "--format=%ad", "--date=format:%H:%M", ref).strip()
    return f"{ref}@{sha} ({when})"


def selftest_ref() -> int:
    """Красный контроль выбора ref: свежий важнее «правильного по имени»."""
    cases = [
        ([("a", 100), ("b", 200)], "b", "поздний важнее первого"),
        ([("a", 300), ("b", 200)], "a", "первый остаётся, если он свежее"),
        ([("a", 5)], "a", "единственный кандидат"),
        ([], "HEAD", "нет кандидатов -> HEAD"),
    ]
    ok = 0
    for cands, want, name in cases:
        best, best_ts = "", -1
        for cand, ts in cands:
            if ts > best_ts:
                best, best_ts = cand, ts
        got = best or "HEAD"
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] ref: {name} -> {got}, ждали {want}")
    print(f"check_front_088 --selftest-ref: {ok}/{len(cases)} (свежесть важнее имени)")
    return 0 if ok == len(cases) else 1


def main() -> int:
    # единое имя у всех гейтов @TE: `--selftest`; `--selftest-ref` оставлен как алиас
    args = sys.argv[1:]
    if len(args) > 0 and args[0] in ("--selftest", "--selftest-ref"):
        return selftest_ref()

    # @DO `chat/do.md` `[2026-11-05 17:50]`: `--ref`/`--pre` не парсились — обёртка передавала
    # `--ref HEAD`, а инструмент читал это как имя ревизии.
    known = ("--ref", "--pre", "--post")
    positional = []
    ref_flag = ""
    i = 0
    while i < len(args):
        a = args[i]
        if a.startswith("--") and a.split("=")[0] not in known:
            print(f"::error::check_front_088: неизвестный флаг {a} "
                  f"(известны: {', '.join(known)}, --selftest) — rc=2")
            return 2
        if a == "--ref":
            if i + 1 < len(args):
                ref_flag = args[i + 1]
                i += 2
                continue
            print("::error::check_front_088: --ref без значения — rc=2")
            return 2
        if a.startswith("--ref="):
            ref_flag = a.split("=", 1)[1]
        elif a not in ("--pre", "--post"):
            positional.append(a)
        i += 1

    explicit = (ref_flag or (positional[0] if positional else os.environ.get("REF") or "")).strip()
    ref = explicit or newest_ref()
    print(f"   REF={describe_ref(ref)}" + (" (задан явно)" if explicit else " (выбран как самый свежий)"))
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
