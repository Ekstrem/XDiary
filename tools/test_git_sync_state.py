#!/usr/bin/env python3
"""Тесты разбора состояния rebase в tools/git_sync.py (DO: --fix-state).

Проверяется ЧИСТАЯ функция classify_rebase_files — без файловой системы, потому что
решение опасно: если признать живой rebase "пустым", --fix-state его удалит.

Запуск: python tools/test_git_sync_state.py
"""
import importlib.util
import os
import sys


def load():
    here = os.path.dirname(os.path.abspath(__file__))
    spec = importlib.util.spec_from_file_location("git_sync", os.path.join(here, "git_sync.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


DECIDE_CASES = [
    # (in_stash, applied, force, ожидаемое решение, почему)
    (True, False, False, True, "автостэш в stash — снимаем"),
    (False, True, False, False, "применён, но без --force — отказ с подсказкой"),
    (False, True, True, True, "применён + --force-drop-autostash — снимаем"),
    (False, False, True, False, "не в stash и не в дереве — отказ даже с force"),
]

CASES = [
    ("только autostash (боевой инцидент)", "rebase-merge", ["autostash"], "empty-autostash"),
    ("autostash + head-name (живой)", "rebase-merge", ["autostash", "head-name"], "other"),
    ("autostash + onto (живой)", "rebase-merge", ["autostash", "onto"], "other"),
    ("head-name без autostash (живой)", "rebase-merge", ["head-name", "onto"], "other"),
    ("пустой каталог", "rebase-merge", [], "other"),
    ("лишние файлы рядом", "rebase-merge", ["autostash", "done"], "other"),
    ("rebase-apply всегда other", "rebase-apply", ["autostash"], "other"),
]


def main():
    mod = load()
    failures = []
    print("git_sync: тест классификации состояния rebase (--fix-state)")
    for name, marker, names, expect in CASES:
        got = mod.classify_rebase_files(marker, list(names))
        ok = got == expect
        if not ok:
            failures.append(name)
        print("  [%s] %-34s kind=%s (ждали %s)" % ("OK " if ok else "FAIL", name, got, expect))
    # corner-case (@TL 10:10): автостэш уже применён, но записи в stash нет
    print()
    print("  -- решение о снятии пустого состояния (decide_fix_state) --")
    for in_stash, applied, force, expect, why in DECIDE_CASES:
        got, reason = mod.decide_fix_state(in_stash, applied, force)
        ok = got == expect
        if not ok:
            failures.append("decide %s/%s/%s" % (in_stash, applied, force))
        print("  [%s] in_stash=%-5s applied=%-5s force=%-5s -> %-5s (%s)" % (
            "OK " if ok else "FAIL", in_stash, applied, force, got, why))

    print()
    if failures:
        print("ПРОВАЛЕНО: %d -> %s" % (len(failures), ", ".join(failures)))
        return 1
    print("все %d проверок прошли" % (len(CASES) + len(DECIDE_CASES)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
