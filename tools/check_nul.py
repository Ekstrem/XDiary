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
PATTERNS = ["5-tech-backlog/**/*.md", "04-architecture/**/*.md",
            "README.md", "CHANGELOG.md"]

# C0 control chars, кроме таба/перевода строки/возврата каретки.
FORBIDDEN = [b for b in range(0x00, 0x20) if b not in (0x09, 0x0A, 0x0D)]
NAMES = {0x00: "NUL", 0x07: "BEL(\\a)", 0x08: "BS(\\b)", 0x0B: "VT(\\v)", 0x0C: "FF(\\f)"}


MARKERS = (b"<<<<<<<", b"=======", b">>>>>>>")


def scan_markers(data):
    """Строки-маркеры конфликта: (номер строки, маркер)."""
    out = []
    for i, line in enumerate(data.split(b"\n"), 1):
        line = line.lstrip(b"\xef\xbb\xbf")  # BOM: Set-Content пишет его первой строкой
        for m in MARKERS:
            if line.startswith(m):
                out.append((i, m.decode()))
                break
    return out


def selftest():
    ok = 0
    cases = [
        (b"- [2026-11-12 10:00] ok\n", 0),
        (b"<<<<<<< Updated upstream\ntext\n", 1),
        (b"text\n=======\n", 1),
        (b">>>>>>> Stashed changes\n", 1),
    ]
    for data, expect in cases:
        got = len(scan_markers(data))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] ожидалось %d маркер(ов), получено %d" % (expect, got))
    print("check_nul --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main():
    if "--selftest" in sys.argv:
        return selftest()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    files = []
    for pat in PATTERNS:
        files.extend(glob.glob(os.path.join(ROOT, pat), recursive=True))
    files = sorted(set(files))
    # _dialogue.md генерируется из outbox-ов ролей — чинится регенерацией, в гейте не участвует.
    files = [x for x in files if not x.endswith("_dialogue.md")]

    bad = []
    for f in files:
        try:
            with open(f, "rb") as fh:
                data = fh.read()
        except OSError as e:
            print(f"  ! {f}: не читается ({e})", file=sys.stderr)
            continue
        rel = os.path.relpath(f, ROOT).replace("\\", "/")
        found = {b: data.count(bytes([b])) for b in FORBIDDEN if data.count(bytes([b]))}
        if found:
            desc = ", ".join(f"{NAMES.get(b, hex(b))}×{n}" for b, n in sorted(found.items()))
            bad.append((rel, desc))
        marks = scan_markers(data)
        if marks:
            where = ", ".join("стр.%d %s" % (n, m) for n, m in marks[:5])
            bad.append((rel, "маркеры конфликта: " + where))

    if bad:
        for rel, desc in bad:
            print(f"  ! {rel}: {desc} — управляющие символы ломают md", file=sys.stderr)
        print(f"check_nul: FAIL ({len(bad)} md-файлов: управляющие символы или маркеры конфликта)")
        return 1

    print(f"check_nul: OK ({len(files)} md-файлов, управляющих символов и маркеров конфликта нет)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
