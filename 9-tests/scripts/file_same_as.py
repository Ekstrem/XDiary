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
    print(f"file_same_as --selftest: {ok}/{len(cases)} (хэш, а не пересчёт строк)")
    return 0 if ok == len(cases) else 1


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
    width = max(len(p) for p, *_ in rows)
    for path, v, local, in_ref in rows:
        print(f"  {path:<{width}}  {v:<7} local={local} ref={in_ref}")
        if v == "DIFF":
            print(f"    ::warning::file_same_as: {path} отличается от {args.ref} "
                  f"(-- может быть CRLF в рабочей копии, проверять через `git diff {args.ref} -- <path>`)")
    same = sum(1 for *_, v in ((r[0], r[1]) for r in rows) if v == "SAME")
    diff = sum(1 for r in rows if r[1] == "DIFF")
    print(f"ИТОГ: SAME={same}, DIFF={diff}, UNKNOWN={unknown} из {len(rows)} (ref={args.ref})")
    if unknown:
        return 2
    return 1 if diff else 0


if __name__ == "__main__":
    raise SystemExit(main())
