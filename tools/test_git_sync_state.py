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
    print()
    if failures:
        print("ПРОВАЛЕНО: %d -> %s" % (len(failures), ", ".join(failures)))
        return 1
    print("все %d проверок прошли" % len(CASES))
    return 0


if __name__ == "__main__":
    sys.exit(main())
