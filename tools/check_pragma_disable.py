#!/usr/bin/env python3
"""check_pragma_disable: запрет `#pragma warning disable` в исходниках (владелец, 2026-11-24).

Причина (BE-148): QG-612 (sync-over-async) обходился локальным
`#pragma warning disable QG612` + `#pragma warning restore` вместо устранения
причины. Директива-подавление = обход гейта качества, поэтому запрещена
инструментально: любое вхождение `#pragma warning disable` в исходниках — красный.

    python tools/check_pragma_disable.py                 # скан всех исходников
    python tools/check_pragma_disable.py --report        # только показать (rc=0)
    python tools/check_pragma_disable.py --selftest

Удаление существующих вхождений — работа владельцев кода (не этого гейта):
гейт фиксирует факт подавления, не чинит причину.
"""
import argparse
import os
import sys

SKIP_DIRS = {".git", "bin", "obj", "node_modules", ".obsidian", ".loopmaker"}
EXTS = {".cs", ".csproj", ".props", ".targets"}
MARKER = "#pragma warning disable"


def is_generated(path):
    """EF-миграции (Designer/ModelSnapshot) — автоген с `disable 612, 618`, не подавление руками."""
    p = path.replace("\\", "/").lower()
    return "/migrations/" in p and ("designer.cs" in p or "modelsnapshot.cs" in p)


def iter_sources(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames
                       if d not in SKIP_DIRS and not d.startswith(".tmp-")]
        for name in filenames:
            if os.path.splitext(name)[1].lower() in EXTS:
                yield os.path.join(dirpath, name)


def scan(root):
    """Список (файл, строка, текст) всех вхождений подавления (кроме автогена EF)."""
    hits = []
    for path in iter_sources(root):
        rel = os.path.relpath(path, root).replace("\\", "/")
        if is_generated(rel):
            continue
        with open(path, encoding="utf-8-sig", errors="replace") as f:
            for number, line in enumerate(f, 1):
                if MARKER.lower() in line.lower():
                    hits.append((rel, number, line.strip()))
    return hits


def selftest():
    """Маркер ловится на синтетической строке; чистая строка — нет."""
    assert MARKER.lower() in "#pragma warning disable QG612".lower()
    assert MARKER.lower() not in "#pragma warning restore QG612".lower()
    assert MARKER.lower() not in "// обсуждаем pragma в доке"
    print("selftest: OK")
    return 0


def main():
    ap = argparse.ArgumentParser(description="Запрет #pragma warning disable в исходниках")
    ap.add_argument("--root", default=None)
    ap.add_argument("--report", action="store_true", help="только показать, rc=0")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if args.selftest:
        return selftest()

    root = args.root or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    hits = scan(root)
    print("check_pragma_disable: файлов с подавлением %d, вхождений %d"
          % (len({h[0] for h in hits}), len(hits)))
    for rel, number, text in hits[:40]:
        print("  %s:%d — %s" % (rel, number, text[:100]))
    if len(hits) > 40:
        print("  … и ещё %d" % (len(hits) - 40))
    if hits and not args.report:
        print("")
        print("Правило: предупреждения анализаторов не подавляются директивой —")
        print("устраняется причина (владелец 2026-11-24, после обхода QG-612 в BE-148).")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
