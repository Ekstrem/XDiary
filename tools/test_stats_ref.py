#!/usr/bin/env python3
"""Тесты режима `stats.py --ref` (DO: сверка с origin, а не только с деревом).

Проверяются две инварианты, которые не зависят от формы истории:
  1) `--check --ref HEAD` даёт тот же вердикт, что `--check` по дереву
     (для чистого дерева — оба 0);
  2) `--write --ref` **отказывает** с кодом 1: в git-ref писать нельзя.

Зачем: `stats --check` без `--ref` сверяет README с деревом, поэтому может говорить
«актуален», когда правка ещё локальная, а `origin` живёт со старой таблицей. Именно
так в 02.11 разошлись 176/177 и 5/6.

Запуск: python tools/test_stats_ref.py
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PY = sys.executable
STATS = os.path.join(HERE, "stats.py")


def run(*args):
    return subprocess.run([PY, STATS, *args], capture_output=True, text=True,
                          encoding="utf-8", errors="replace", cwd=ROOT)


def main():
    failures = []
    print("stats: тесты режима --ref")

    # Инвариант «--check (дерево) == --check --ref HEAD» верен ТОЛЬКО на чистом дереве:
    # при незакоммиченных правках README/задачи в дереве отличаются от HEAD, и тест
    # давал ложный красный на общем рабочем дереве (@TL 02:00). Проверяем чистоту.
    status = subprocess.run(["git", "status", "--porcelain"], capture_output=True,
                            text=True, encoding="utf-8", errors="replace", cwd=ROOT)
    dirty = bool((status.stdout or "").strip())
    if dirty:
        print("  [SKIP] --check vs --check --ref HEAD: дерево грязное "
              "(незакоммиченные правки) — сравнение с HEAD не имеет смысла")
    else:
        tree = run("--check")
        head = run("--check", "--ref", "HEAD")
        ok = (tree.returncode == head.returncode)
        if not ok:
            failures.append("--check vs --check --ref HEAD")
        print("  [%s] --check rc=%d / --check --ref HEAD rc=%d (должны совпадать)" % (
            "OK " if ok else "FAIL", tree.returncode, head.returncode))

    ref_write = run("--write", "--ref", "HEAD")
    ok = ref_write.returncode == 1 and "несовместимы" in (ref_write.stderr + ref_write.stdout)
    if not ok:
        failures.append("--write --ref должен отказывать")
    print("  [%s] --write --ref → rc=%d, отказ в тексте: %s" % (
        "OK " if ok else "FAIL", ref_write.returncode,
        "да" if "несовместимы" in (ref_write.stderr + ref_write.stdout) else "нет"))

    bad_ref = run("--check", "--ref", "refs/__нет_такого__")
    ok = bad_ref.returncode != 0
    if not ok:
        failures.append("несуществующий ref должен падать")
    print("  [%s] --check --ref несуществующий → rc=%d (должен быть != 0)" % (
        "OK " if ok else "FAIL", bad_ref.returncode))

    print()
    if failures:
        print("ПРОВАЛЕНО: %s" % ", ".join(failures))
        return 1
    print("все проверки прошли")
    return 0


if __name__ == "__main__":
    sys.exit(main())
