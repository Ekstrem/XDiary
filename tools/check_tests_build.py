#!/usr/bin/env python
"""Проверка (10) @AR 2026-11-14 28:55: каждый тестовый проект ДОЛЖЕН СОБИРАТЬСЯ.

Зачем: тест, который не собирается, — это **молча потерянное покрытие**: в отчёте он
выглядит существующим, приёмка по раскладке зелёная, а `dotnet test` падает на сборке
(класс `CS2001` после переименования папки). Статические правила (ж)/(з) в
`check_test_isolation.py` ловят причину, но не факт сборки.

Почему не в CI на каждый пуш: 31 проект × restore/build — это десятки минут; в CI сборка
уже происходит в per-service workflow и в `ci-backend` по зоне. Этот скрипт — локальная
приёмка перед закрытием задачи и аргумент в квитанции.

Использование:
    python tools/check_tests_build.py                 # все *Tests*.csproj в 6-msa
    python tools/check_tests_build.py --changed       # только те, что в зоне изменённых BC
    python tools/check_tests_build.py --project 6-msa/BC-Journal/src/Journal.ApiTests/Journal.ApiTests.csproj
    python tools/check_tests_build.py --dry-run       # показать список, не собирая
"""
import argparse
import os
import re
import subprocess
import sys


def find_projects(root):
    out = []
    for dirpath, _dirs, names in os.walk(os.path.join(root, "6-msa")):
        for name in names:
            if re.search(r"Tests?\.csproj$", name):
                out.append(os.path.join(dirpath, name))
    return sorted(out)


def build(path, timeout=600):
    try:
        r = subprocess.run(["dotnet", "build", path, "--nologo", "-v", "q"],
                           capture_output=True, text=True, timeout=timeout)
    except FileNotFoundError:
        return 2, "dotnet не найден в PATH"
    except subprocess.TimeoutExpired:
        return 3, "таймаут сборки"
    tail = [l for l in ((r.stdout or "") + (r.stderr or "")).splitlines() if "error" in l.lower()]
    return r.returncode, (tail[0] if tail else "")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--project", action="append", default=[])
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    root = os.path.abspath(a.root)
    projects = ([os.path.join(root, p.replace("/", os.sep)) for p in a.project]
                if a.project else find_projects(root))
    print("тестовых проектов: %d" % len(projects))
    if a.dry_run:
        for p in projects:
            print("  " + os.path.relpath(p, root).replace(os.sep, "/"))
        return 0
    bad = 0
    for p in projects:
        rel = os.path.relpath(p, root).replace(os.sep, "/")
        rc, msg = build(p)
        if rc != 0:
            bad += 1
            print("::error::(10) %s не собирается (rc=%d)%s" % (rel, rc, (": " + msg[:160]) if msg else ""))
        else:
            print("ok %s" % rel)
    print("check_tests_build: проектов %d, не собирается %d" % (len(projects), bad))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
