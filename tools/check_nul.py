#!/usr/bin/env python3
"""Guard: markdown-файлы не должны содержать управляющих символов.

Почему: запись md через `Add-Content`/here-string в pwsh превращает `\\f`, `\\b`, `\\a`,
`\\v` в управляющие байты — текст тихо портится («front-e2e» → «<FF>ront-e2e»), git
считает файл бинарным (`Bin … bytes`), диффы нечитаемы, `chat_lint`/`_dialogue.md`
могут терять строки. Гейт ловит это на месте (DO-030; расширен 07.11 после того, как
`\\f`/`\\b` просочились в чат, а NUL-проверка их не видела).

Разрешены только `\\t` (0x09), `\\n` (0x0A), `\\r` (0x0D).

Запуск: python tools/check_nul.py
"""
import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATTERNS = ["5-tech-backlog/**/*.md", "README.md", "CHANGELOG.md"]

# C0 control chars, кроме таба/перевода строки/возврата каретки.
FORBIDDEN = [b for b in range(0x00, 0x20) if b not in (0x09, 0x0A, 0x0D)]
NAMES = {0x00: "NUL", 0x07: "BEL(\\a)", 0x08: "BS(\\b)", 0x0B: "VT(\\v)", 0x0C: "FF(\\f)"}


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
        found = {b: data.count(bytes([b])) for b in FORBIDDEN if data.count(bytes([b]))}
        if found:
            desc = ", ".join(f"{NAMES.get(b, hex(b))}×{n}" for b, n in sorted(found.items()))
            bad.append((os.path.relpath(f, ROOT).replace("\\", "/"), desc))

    if bad:
        for rel, desc in bad:
            print(f"  ! {rel}: {desc} — управляющие символы ломают md", file=sys.stderr)
        print(f"check_nul: FAIL ({len(bad)} md-файлов с управляющими символами)")
        return 1

    print(f"check_nul: OK ({len(files)} md-файлов, управляющих символов нет)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
