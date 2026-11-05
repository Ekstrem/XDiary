#!/usr/bin/env python3
"""XDiary: «метка существует?» — проверка по ИСТОРИИ, а не по текущему состоянию.

Зачем (правило `chat/tl.md` `[2026-11-05 14:05]`, случай `fe.md` `2026-11-16 11:45`):
проверка «метки нет в `origin`» **не доказывает**, что метки не было. Если роль правила метки
(например, приводила штампы к реальному времени), исправление **стирает улику**: сообщение
остаётся в истории, но в текущем файле его нет. Я на этом ошибся ровно так — объявил метку
«фантомом», хотя она была реальной и адресована мне.

Правильная проверка — pickaxe по истории:

    git log --all -S"<метка>" -- <файл>        # в каких коммитах метка появлялась/исчезала
    git show <commit>^:<файл>                  # как файл выглядел ДО правки метки

Использование:
    python 9-tests/scripts/find_stamp.py --stamp "2026-11-16 11:45" --file 5-tech-backlog/chat/fe.md
    python 9-tests/scripts/find_stamp.py --stamp "2026-11-05 15:45"          # по всем чатам
    python 9-tests/scripts/find_stamp.py --selftest

Вердикты: `СЕЙЧАС` (есть в текущем файле), `ТОЛЬКО В ИСТОРИИ` (было, потом исправлено),
`НЕТ НИГДЕ` (действительно не существует).
Коды возврата: 0 — метка существует (сейчас или в истории); 1 — не найдена нигде; 2 — не git-репозиторий.
"""
from __future__ import annotations

import argparse
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:                             # noqa: BLE001
    pass


def git(*args: str) -> tuple:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.returncode, r.stdout


def classify(now_hits: int, history_commits: int) -> str:
    """Вся логика вердикта — в чистой функции, чтобы проверить без git и без ФС."""
    if now_hits > 0:
        return "СЕЙЧАС"
    if history_commits > 0:
        return "ТОЛЬКО В ИСТОРИИ"
    return "НЕТ НИГДЕ"


def history_commits(stamp: str, path: str = "") -> list:
    args = ["log", "--all", "-S", stamp, "--format=%h|%ad|%s", "--date=short"]
    if path:
        args += ["--", path]
    rc, out = git(*args)
    if rc != 0:
        return []
    rows = []
    for line in out.splitlines()[:8]:
        parts = line.split("|", 2)
        if len(parts) == 3:
            rows.append(tuple(parts))
    return rows


def is_canonical_line(content: str, stamp: str) -> bool:
    """Штамп — это начало строки `- [<stamp>] …`, а НЕ любое упоминание метки в тексте.

    Различение критично: @FE в `fe.md` объяснял, что `2026-11-16 11:45` не фантом, и в тексте
    этих объяснений метка встречается — но **сообщения** с такой меткой в текущем файле нет.
    Счёт по подстроке дал бы ложное «СЕЙЧАС» (та же ошибка, что «122» против «59»).
    """
    return content.lstrip().startswith("- [" + stamp + "]")


def now_hits(stamp: str, path: str = "") -> int:
    args = ["grep", "-n", "-F", stamp]
    if path:
        args += ["--", path]
    rc, out = git(*args)
    total = 0
    for line in out.splitlines():
        parts = line.split(":", 2)
        if len(parts) == 3 and is_canonical_line(parts[2], stamp):
            total += 1
    return total


def selftest() -> int:
    line_cases = [
        ("- [2026-11-16 11:45] → @TE: текст", "2026-11-16 11:45", True, "штамп в начале строки"),
        ("- [2026-11-05 14:04] → @TE: метка `2026-11-16 11:45` не фантом", "2026-11-16 11:45", False,
         "упоминание в тексте — НЕ штамп"),
        ("  - [2026-11-16 11:45] → @TL: с отступом", "2026-11-16 11:45", True, "отступ допустим"),
        ("[2026-11-16 11:45] без дефиса", "2026-11-16 11:45", False, "нет канон-префикса"),
    ]
    cases = [
        (3, 0, "СЕЙЧАС", "есть в текущем файле"),
        (1, 5, "СЕЙЧАС", "есть и сейчас, и в истории"),
        (0, 3, "ТОЛЬКО В ИСТОРИИ", "исправлена, но была"),
        (0, 0, "НЕТ НИГДЕ", "не существует"),
        (0, 1, "ТОЛЬКО В ИСТОРИИ", "одного коммита достаточно"),
    ]
    ok = 0
    for content, stamp, want, label in line_cases:
        got = is_canonical_line(content, stamp)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    for now, hist, want, label in cases:
        got = classify(now, hist)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    total = len(line_cases) + len(cases)
    print(f"find_stamp --selftest: {ok}/{total} (история + различение «штамп / упоминание»)")
    return 0 if ok == total else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--stamp", help="метка целиком, например '2026-11-16 11:45'")
    ap.add_argument("--file", default="", help="ограничить одним файлом (по умолчанию все чаты)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    if not args.stamp:
        print("::error::find_stamp: нужен --stamp (rc=2)")
        return 2
    rc, _ = git("rev-parse", "--is-inside-work-tree")
    if rc != 0:
        print("::error::find_stamp: не git-репозиторий (rc=2)")
        return 2

    path = args.file or "5-tech-backlog/chat"
    now = now_hits(args.stamp, path)
    hist = history_commits(args.stamp, path)
    verdict = classify(now, len(hist))
    print(f"find_stamp: метка «{args.stamp}» в {path or 'репозитории'}")
    print(f"  текущее состояние: вхождений {now}")
    print(f"  история (git log -S): коммитов {len(hist)}")
    for h in hist:
        print(f"    {h[0]} {h[1]} {h[2][:80]}")
    print(f"  ВЕРДИКТ: {verdict}")
    if verdict == "ТОЛЬКО В ИСТОРИИ":
        print("  ::warning::find_stamp: метка была и исчезла (правка меток) — "
              "«нет в origin» ≠ «фантом»; смотреть `git show <commit>^:<file>`")
    if verdict == "НЕТ НИГДЕ":
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
