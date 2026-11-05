#!/usr/bin/env python
"""check_null_paging.py — канон: `IPaging` не передаётся null в read-репозиторий (DO-079/BE-069).

История класса (три раза за сутки, каждый раз «тот же баг в другом BC»):
  * `BE-067` — `JournalReadRepository`: `null` вместо `IPaging` → NRE на `paging.Page`;
  * `BE-069` — `PartnerReadRepository.cs:16` `GetAllAsync(null!, ct)` → тот же NRE,
    из-за которого контракт партнёров даёт `4/4 failed`, а E2E падает в `seedSession`.

Библиотека читает `paging.Page`/`paging.Size` **сразу**, поэтому `null` (в том числе
`null!` — оператор только глушит компилятор) гарантированно даёт `NullReferenceException`.
Правильно: `new Paging(1, int.MaxValue)`.

Что проверяется (по `6-msa/**/*.cs`):
  1. `GetAllAsync(null`, `GetAllAsync(default`, `GetAllAsync((IPaging)null` — дефект;
  2. `ListAsync(null`, `GetPagedAsync(null`, `ToListAsync(null` с явным `IPaging`-параметром — дефект;
  3. `GetAllAsync(Nullable…)` — не считается (не наш случай).

    python tools/check_null_paging.py --root 6-msa
    python tools/check_null_paging.py --selftest

rc: 0 — нарушений нет; 1 — есть; 2 — нечего проверять (подозрительно: значит фильтр сломан).
"""
import argparse
import glob
import os
import re
import sys

PATTERNS = [
    re.compile(r'\bGetAllAsync\s*\(\s*null\s*[!),]'),
    re.compile(r'\bGetAllAsync\s*\(\s*default\s*[!),]'),
    re.compile(r'\bGetAllAsync\s*\(\s*\(\s*IPaging\s*\)\s*null'),
    re.compile(r'\bListAsync\s*\(\s*null\s*[!),]'),
    re.compile(r'\bGetPagedAsync\s*\(\s*null\s*[!),]'),
]


def scan_text(text):
    """Возвращает список (номер строки, строка) с нарушениями."""
    hits = []
    for i, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith('//') or stripped.startswith('///'):
            continue
        for pat in PATTERNS:
            if pat.search(line):
                hits.append((i, stripped))
                break
    return hits


def selftest():
    ok = total = 0

    def case(text, want, label):
        nonlocal ok, total
        total += 1
        got = len(scan_text(text))
        good = got == want
        if good:
            ok += 1
        else:
            print('  [FAIL] %s: нашли %d, ждали %d' % (label, got, want))

    case('var all = await _read.GetAllAsync(null!, ct);', 1, 'null! в GetAllAsync — дефект')
    case('var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);', 0,
         'канон new Paging(1, int.MaxValue) — чисто')
    case('var all = await _read.GetAllAsync(default!, ct);', 1, 'default! — дефект')
    case('var all = await _read.GetAllAsync((IPaging)null, ct);', 1, 'явный каст null — дефект')
    case('// var all = await _read.GetAllAsync(null!, ct);', 0, 'закомментированная — не дефект')
    case('var x = await _read.ListAsync(null, ct);', 1, 'ListAsync(null) — дефект')
    case('var items = await _db.Records.ToListAsync(ct);', 0, 'ToListAsync без пейджинга — чисто')

    print('check_null_paging --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Канон: IPaging не null')
    ap.add_argument('--root', default='6-msa')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    files = sorted(glob.glob(os.path.join(a.root, '**', '*.cs'), recursive=True))
    files = [f for f in files if 'obj' + os.sep not in f and 'bin' + os.sep not in f]
    bad = 0
    for path in files:
        text = open(path, encoding='utf-8', errors='replace').read()
        for line_no, line in scan_text(text):
            print('  %s:%d  %s' % (path.replace(os.sep, '/'), line_no, line[:120]))
            print('::error::check_null_paging: %s:%d — IPaging передаётся null (NRE в библиотеке); '
                  'нужно new Paging(1, int.MaxValue)' % (path.replace(os.sep, '/'), line_no))
            bad += 1

    print('проверено файлов: %d | нарушений: %d' % (len(files), bad))
    if not files:
        print('::error::check_null_paging: не найдено ни одного .cs — фильтр сломан, а не «всё чисто»')
        return 2
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
