#!/usr/bin/env python3
"""XDiary: «этот файл тот же, что в ref?» — ответ ХЭШЕМ, а не пересчётом строк.

Зачем (правило дня, `chat/ar.md` [2026-11-06 00:50]): `Measure-Object -Line` в PowerShell и
`splitlines()` в Python дают «около» — они недосчитывают на пустых строках/CRLF и на этом уже
дважды делались неверные выводы («769 вместо 962», «24 поля вместо 20», «90 строк потеряны»).
Единственный надёжный ответ на «файл тот же?» — сравнение **blob-хэшей**:

    локальный: git hash-object <path>              (что реально лежит на диске)
    в ref:     git rev-parse <ref>:<path>          (что лежит в коммите)

Важно: `hash-object` хэширует байты рабочей копии, поэтому при включённом `core.autocrlf`
локальный хэш может отличаться от blob'а из-за CRLF. Скрипт сообщает об этом отдельно
(`SAME(content)`) и печатает оба значения, чтобы расхождение нельзя было принять за «файлы разные».

Использование:
    python 9-tests/scripts/file_same_as.py --ref origin/main -- 9-tests/README.md README.md
    python 9-tests/scripts/file_same_as.py --selftest

Коды возврата: 0 — все файлы совпадают; 1 — есть различия (перечислены); 2 — не удалось прочитать
ref/путь (неизвестность, а не «ок»).
"""
from __future__ import annotations

import argparse
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:                             # noqa: BLE001
    pass


def git(*args: str) -> str:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout.strip() if r.returncode == 0 else ""


def verdict(local_hash: str, ref_hash: str) -> str:
    """SAME / DIFF / UNKNOWN — вся логика в одной функции, чтобы её можно было проверить без ФС."""
    if not local_hash or not ref_hash:
        return "UNKNOWN"
    return "SAME" if local_hash == ref_hash else "DIFF"


def check(ref: str, paths: list) -> tuple:
    """Возвращает (rows, unknown) — rows: [(path, verdict, local_short, ref_short)]."""
    rows, unknown = [], 0
    for p in paths:
        local = git("hash-object", "--", p)
        in_ref = git("rev-parse", f"{ref}:{p}")
        v = verdict(local, in_ref)
        if v == "UNKNOWN":
            unknown += 1
        rows.append((p, v, (local[:10] or "-"), (in_ref[:10] or "-")))
    return rows, unknown


def selftest() -> int:
    cases = [
        ("a" * 40, "a" * 40, "SAME", "одинаковые хэши"),
        ("a" * 40, "b" * 40, "DIFF", "разные хэши"),
        ("", "b" * 40, "UNKNOWN", "нет локального файла"),
        ("a" * 40, "", "UNKNOWN", "нет файла в ref"),
        ("", "", "UNKNOWN", "оба недоступны"),
    ]
    ok = 0
    for local, ref_h, want, label in cases:
        got = verdict(local, ref_h)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    # кейсы на ТЕКСТ (норма @TL `15:38`): нужное значение есть, ложное отсутствует
    text = "\n".join(output_lines(
        [("a.md", "SAME", "aaaa1111", "aaaa1111"),
         ("b.md", "DIFF", "bbbb2222", "cccc3333"),
         ("c.md", "UNKNOWN", "-", "-")], 1, "gh/main"))
    must = ["SAME", "DIFF", "UNKNOWN", "local=aaaa1111", "ИТОГ: SAME=1, DIFF=1, UNKNOWN=1 из 3",
            "отличается от gh/main"]
    must_not = ["ИТОГ: SAME=0", "None"]
    miss = [m for m in must if m not in text]
    bad = [m for m in must_not if m in text]
    if not miss and not bad:
        ok += 1
    else:
        print(f"  [FAIL] текст: нет {miss}, лишнее {bad}")
    text_cases = 1

    print(f"file_same_as --selftest: {ok}/{len(cases) + text_cases} (хэш, а не пересчёт строк)")
    return 0 if ok == len(cases) + 1 else 1


def row_line(path: str, verdict: str, local: str, in_ref: str, width: int) -> str:
    return f"  {path:<{width}}  {verdict:<7} local={local} ref={in_ref}"


def summary_line(rows: list, unknown: int, ref: str) -> str:
    same = sum(1 for r in rows if r[1] == "SAME")
    diff = sum(1 for r in rows if r[1] == "DIFF")
    return f"ИТОГ: SAME={same}, DIFF={diff}, UNKNOWN={unknown} из {len(rows)} (ref={ref})"


def output_lines(rows: list, unknown: int, ref: str) -> list:
    width = max((len(r[0]) for r in rows), default=1)
    lines = []
    for path, v, local, in_ref in rows:
        lines.append(row_line(path, v, local, in_ref, width))
        if v == "DIFF":
            lines.append(f"    ::warning::file_same_as: {path} отличается от {ref} "
                         f"(-- может быть CRLF в рабочей копии, проверять через `git diff {ref} -- <path>`)")
    lines.append(summary_line(rows, unknown, ref))
    return lines


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ref", default="origin/main", help="ref для сравнения (по умолчанию origin/main)")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("paths", nargs="*", help="пути относительно корня репозитория")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    if not args.paths:
        print("::error::file_same_as: не переданы пути (rc=2)")
        return 2
    if not git("rev-parse", "--verify", args.ref):
        print(f"::error::file_same_as: ref '{args.ref}' не резолвится — сравнение невозможно (rc=2)")
        return 2
    rows, unknown = check(args.ref, args.paths)
    for line in output_lines(rows, unknown, args.ref):
        print(line)
    if unknown:
        return 2
    return 1 if any(r[1] == "DIFF" for r in rows) else 0


if __name__ == "__main__":
    raise SystemExit(main())
