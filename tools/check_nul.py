#!/usr/bin/env python3
"""Guard: markdown-файлы не должны содержать NUL-байты.

Причина NUL — запись md через `Add-Content`/here-string в pwsh: git видит файл
бинарным (`Bin … bytes`), диффы нечитаемы, `chat_lint`/`_dialogue.md` могут терять строки.
Этот гейт ловит регресс на месте (DO-030).

Запуск: python tools/check_nul.py
"""
import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATTERNS = ["5-tech-backlog/**/*.md", "README.md", "CHANGELOG.md"]


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    files = []
    for pat in PATTERNS:
        files.extend(glob.glob(os.path.join(ROOT, pat), recursive=True))
    files = sorted(set(files))

    bad = []
    for f in files:
        try:
            with open(f, "rb") as fh:
                data = fh.read()
        except OSError as e:
            print(f"  ! {f}: не читается ({e})", file=sys.stderr)
            continue
        n = data.count(b"\x00")
        if n:
            bad.append((os.path.relpath(f, ROOT).replace("\\", "/"), n))

    if bad:
        for rel, n in bad:
            print(f"  ! {rel}: {n} NUL-байт — git считает файл бинарным", file=sys.stderr)
        print(f"check_nul: FAIL ({len(bad)} md-файлов с NUL-байтами)")
        return 1

    print(f"check_nul: OK ({len(files)} md-файлов, NUL нет)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
