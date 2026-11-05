#!/usr/bin/env python
"""check_eventstore_registration.py — канон регистрации `EventStoreDbContext` (@AR 22:40/23:25).

Правило (из практики 2026-11-04/05): в каждом `Program.cs`, где вызывается
`AddEventStorePostgres`, должна быть **ровно одна** типизированная регистрация

    builder.Services.AddDbContext<…EventStoreDbContext>(o => o.UseNpgsql(…))

и она обязана идти **ПОСЛЕ** вызова `AddEventStorePostgres` (последняя регистрация
побеждает). Если поставить её **до** — приоритет остаётся у библиотечной, и контекст
получает необобщённые `DbContextOptions` → `InvalidOperationException` на старте,
сервис падает до `Bind` (так легли `journal` и `partners`).

Критерий машинный, потому что «grep → 0» был отменён @AR: удаление типизированной
регистрации делает хуже, а не лучше. Проверяем **число** и **взаимный порядок строк**.

    python tools/check_eventstore_registration.py
    python tools/check_eventstore_registration.py --selftest

rc: 0 — канон соблюдён; 1 — нарушение (нет регистрации / больше одной / не после DF);
2 — проверять нечего (файлов с `AddEventStorePostgres` не найдено).
"""
import argparse
import glob
import hashlib
import os
import re
import sys

DF_CALL = re.compile(r'AddEventStorePostgres\s*<')
TYPED = re.compile(r'AddDbContext\s*<\s*[A-Za-z0-9_.]*EventStoreDbContext\s*>')


def analyze(text):
    """Возвращает (line_df, [line_typed...]) — номера строк (1-based)."""
    line_df = None
    typed = []
    for i, line in enumerate(text.splitlines(), 1):
        if line_df is None and DF_CALL.search(line):
            line_df = i
        if TYPED.search(line) and not line.strip().startswith('//'):
            typed.append(i)
    return line_df, typed


def verdict(line_df, typed):
    """('ok'|'no-typed'|'many-typed'|'typed-before'|'no-df', детали)."""
    if line_df is None:
        return 'no-df', ''
    if not typed:
        return 'no-typed', ''
    if len(typed) > 1:
        return 'many-typed', 'строки %s' % typed
    if typed[0] < line_df:
        return 'typed-before', 'typed на %d, DF на %d' % (typed[0], line_df)
    return 'ok', 'DF %d → typed %d' % (line_df, typed[0])


def selftest():
    ok = total = 0

    def case(text, want, label):
        nonlocal ok, total
        total += 1
        got, _ = verdict(*analyze(text))
        if got == want:
            ok += 1
        else:
            print('  [FAIL] %s: получили %s, ждали %s' % (label, got, want))

    df = 'builder.Services.AddEventStorePostgres<IA, IAM>(cs, null);\n'
    typed = ('builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.'
             'EventStoreDbContext>(o => o.UseNpgsql(cs));\n')
    case(df + typed, 'ok', 'typed после DF (канон)')
    case(typed + df, 'typed-before', 'typed до DF — дефект')
    case(df, 'no-typed', 'только DF')
    case(df + typed + typed, 'many-typed', 'две типизированные')
    case('// нет DF вовсе\n' + typed, 'no-df', 'нет DF')
    case('// ' + typed.strip() + '\n' + df, 'no-typed', 'закомментированная не считается')

    print('check_eventstore_registration --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Канон регистрации EventStoreDbContext')
    ap.add_argument('--root', default='6-msa')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    files = sorted(glob.glob(os.path.join(a.root, '**', 'Program.cs'), recursive=True))
    checked = bad = 0
    for path in files:
        text = open(path, encoding='utf-8', errors='replace').read()
        line_df, typed = analyze(text)
        if line_df is None:
            continue
        checked += 1
        state, detail = verdict(line_df, typed)
        print('  %-64s %s %s' % (path.replace(os.sep, '/'), state, ('(%s)' % detail) if detail else ''))
        if state != 'ok':
            bad += 1
    if checked == 0:
        print('::error::check_eventstore_registration: не найдено файлов с AddEventStorePostgres в %s' % a.root)
        return 2
    digest = hashlib.sha256(''.join(open(p, 'rb').read().decode('utf-8', 'replace')
                                    for p in files).encode()).hexdigest()[:12]
    print('проверено файлов: %d | нарушений: %d | tool=check_eventstore_registration.py@%s'
          % (checked, bad, digest))
    if bad:
        print('::error::check_eventstore_registration: %d нарушений канона (одна типизированная '
              'регистрация ПОСЛЕ AddEventStorePostgres)' % bad)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
