#!/usr/bin/env python3
"""Тест предохранителя `tools/git_sync.py` (инцидент 02.11, баг найден @TE 07:18).

Проверяется ровно тот дефект, который был в проде: при untracked-файлах и без `-u`
предохранитель **должен отказать (код 1)** и напечатать список файлов, а не упасть
с `TypeError` (было: `say("   ?", name)` при `def say(text="")`).

Побочный эффект: создаётся и удаляется один untracked-файл в корне репозитория.
Пуша нет — предохранитель срабатывает до `fetch`/`rebase`/`push`.

Запуск: python tools/test_git_sync.py
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROBE = os.path.join(ROOT, "probe_git_sync_untracked.txt")


def main():
    with open(PROBE, "w", encoding="utf-8") as fh:
        fh.write("probe\n")
    try:
        res = subprocess.run(
            [sys.executable, os.path.join("tools", "git_sync.py"), "--role", "do"],
            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    finally:
        try:
            os.remove(PROBE)
        except OSError:
            pass

    out = (res.stdout or "") + (res.stderr or "")
    problems = []
    if "TypeError" in out:
        problems.append("TypeError в предохранителе (тот самый баг)")
    if res.returncode != 1:
        problems.append("код возврата %s, ожидался 1" % res.returncode)
    if "ВНИМАНИЕ" not in out:
        problems.append("нет предупреждения «ВНИМАНИЕ» в выводе")
    if "probe_git_sync_untracked.txt" not in out:
        problems.append("untracked-файл не попал в список")

    print("git_sync: тест предохранителя untracked")
    if problems:
        for p in problems:
            print("  FAIL:", p)
        print("  --- вывод git_sync ---")
        for line in out.splitlines()[:15]:
            print("   ", line)
        return 1
    print("  OK: отказ с кодом 1, файл назван, TypeError нет")
    return 0


if __name__ == "__main__":
    sys.exit(main())
