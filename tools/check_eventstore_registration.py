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
    """('ok'|'no-typed'|'many-typed'|'typed-after'|'no-df', детали)."""
    if line_df is None:
        return 'no-df', ''
    if not typed:
        return 'no-typed', ''
    if len(typed) > 1:
        return 'many-typed', 'строки %s' % typed
    # DO-097: канон — типизированная регистрация ДО `AddEventStorePostgres`.
    # `DbContextOptions<T>` регистрируется через `TryAdd` внутри `AddDbContext`, поэтому
    # выигрывает ПЕРВАЯ регистрация; вызванный после `AddEventStorePostgres` её не перекрывает
    # (de5507f0: 42P01 36 -> 0, migrations=AddEventStore).
    if typed[0] < line_df:
        return 'ok', 'typed %d -> DF %d (канон: typed раньше)' % (typed[0], line_df)
    return 'typed-after', 'DF %d, typed %d' % (line_df, typed[0])


STORE = re.compile(r'Add(?:Event|Read)StorePostgres\s*<([^;]*?)>\s*\(')
MANUAL_CTX = re.compile(r'AddDbContext\s*<\s*([A-Za-z0-9_.]+)\s*>\s*\(')


def analyze_read_contexts(text):
    """Контексты, которыми владеет `AddReadStorePostgres`, не должны перерегистрироваться вручную.

    Второй случай той же природы (2026-11-05 03:2x): `AddReadStorePostgres<IJournal, JournalReadModel,
    ReadDbContext>` регистрирует контекст **вместе с маппингами** read-моделей, а ручной
    `AddDbContext<ReadDbContext>(…)` ниже перерегистрирует его **без** маппингов — побеждает последний,
    и запрос падает `InvalidOperationException: Cannot create a DbSet for 'JournalReadModel' because
    this type is not included in the model for the context`.

    Для `EventStoreDbContext` правило ОБРАТНОЕ (нужна типизированная регистрация + мост), поэтому
    оба случая проверяются разными правилами, а не одним.
    """
    owned = {}
    for i, line in enumerate(text.splitlines(), 1):
        m = STORE.search(line)
        if not m:
            continue
        args = [a.strip() for a in m.group(1).split(',')]
        if len(args) >= 3:
            owned.setdefault(args[2].split('.')[-1], []).append(i)
    manual = {}
    for i, line in enumerate(text.splitlines(), 1):
        for m in MANUAL_CTX.finditer(line):
            manual.setdefault(m.group(1).split('.')[-1], []).append(i)
    return [(ctx, lines[0], manual[ctx]) for ctx, lines in owned.items() if ctx in manual]


def manual_after_library(text, owned_contexts):
    """DO-083: ручной `AddDbContext<T>` ПОСЛЕ библиотечной регистрации, где T — контекст библиотеки.

    Ловушка: библиотека регистрирует контекст со своими опциями (`MigrationsAssembly`,
    провайдер), а приложение ниже добавляет свой `AddDbContext<T>` и **перекрывает** его.
    При `Database.Migrate()` это даёт тихий отказ: миграции не применяются, ошибки нет.
    История: так уже было с `EventStoreDbContext` (b9173f09, затем BE-066).

    Исключения (осознанные, объяснены в шапке файла):
      * `EventStoreDbContext` — наоборот, требует ручной типизированной регистрации для моста
        `DbContextOptions` (BE-067), причём **ДО** `AddEventStorePostgres` (DO-097: `TryAdd`
        в `AddDbContext` ⇒ выигрывает первая регистрация); «после» здесь — дефект;
      * тестовые хосты (`*HostSmokeTest`, `*IntegrationTests`) — вне проверки.

    Возвращает [(контекст, строка_df, строка_ручная)].
    """
    library = {}
    for i, line in enumerate(text.splitlines(), 1):
        m = STORE.search(line)
        if not m:
            continue
        args = [a.strip() for a in m.group(1).split(',')]
        if len(args) >= 3:
            library.setdefault(args[2].split('.')[-1], []).append(i)
    manual = {}
    for i, line in enumerate(text.splitlines(), 1):
        for m in MANUAL_CTX.finditer(line):
            manual.setdefault(m.group(1).split('.')[-1], []).append(i)
    out = []
    for ctx, df_lines in library.items():
        if ctx == 'EventStoreDbContext':
            continue
        for line_no in manual.get(ctx, []):
            if line_no > df_lines[0]:
                out.append((ctx, df_lines[0], line_no))
    return out


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
    case(typed + df, 'ok', 'typed ДО DF — канон (DO-097)')
    case(df + typed, 'typed-after', 'typed после DF — дефект')
    case(df, 'no-typed', 'только DF')
    case(df + typed + typed, 'many-typed', 'две типизированные')
    case('// нет DF вовсе\n' + typed, 'no-df', 'нет DF')
    case('// ' + typed.strip() + '\n' + df, 'no-typed', 'закомментированная не считается')

    # вторая проверка: ручной AddDbContext для контекста, которым владеет AddReadStorePostgres
    read_ok = 'builder.Services.AddReadStorePostgres<IA, IRM, ReadDbContext>(cs, null);\n'
    read_bad = read_ok + 'builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(cs));\n'
    total += 2
    if not analyze_read_contexts(read_ok):
        ok += 1
    else:
        print('  [FAIL] чистая read-регистрация не должна считаться дефектом')
    if analyze_read_contexts(read_bad):
        ok += 1
    else:
        print('  [FAIL] ручной AddDbContext<ReadDbContext> после AddReadStorePostgres — дефект')

    # DO-083: порядок «ручная регистрация после библиотечной»
    total += 3
    df_read = 'builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(cs, null);\n'
    manual_read = 'builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(cs));\n'
    if manual_after_library(df_read + manual_read, None):
        ok += 1
    else:
        print('  [FAIL] ручной AddDbContext<ReadDbContext> после DF не пойман')
    if not manual_after_library(manual_read + df_read, None):
        ok += 1
    else:
        print('  [FAIL] ручная регистрация ДО библиотечной — не дефект (перекрывает библиотека)')
    if not manual_after_library(
            'builder.Services.AddEventStorePostgres<IA, IAM>(cs, null);\n'
            'builder.Services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql(cs));\n', None):
        ok += 1
    else:
        print('  [FAIL] типизированная регистрация EventStoreDbContext — канон, не дефект')

    print('check_eventstore_registration --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Канон регистрации EventStoreDbContext')
    ap.add_argument('--root', default='6-msa')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--warn-only', action='store_true',
                    help='печатать нарушения, но возвращать rc=0 (для известного дефекта с владельцем)')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    # DO-083: тестовые хосты (`*HostSmokeTest`, `*IntegrationTests`) — ВНЕ проверки,
    # и это сказано явно, а не следует из случайного пути: берём только `*.Api/Program.cs`.
    files = sorted(glob.glob(os.path.join(a.root, '**', '*.Api', 'Program.cs'), recursive=True))
    if not files:  # обратная совместимость: старые раскладки без *.Api
        files = sorted(glob.glob(os.path.join(a.root, '**', 'Program.cs'), recursive=True))
        files = [f for f in files if 'Tests' not in f and 'SmokeTest' not in f]
    checked = bad = 0
    for path in files:
        text = open(path, encoding='utf-8', errors='replace').read()
        line_df, typed = analyze(text)
        if line_df is None:
            continue
        checked += 1
        for ctx, df_line, manual_line in manual_after_library(text, None):
            print('  %-64s DBCTX %s: библиотека на %d, ручной AddDbContext на %d — '
                  'он перекроет библиотечные опции (MigrationsAssembly), Migrate() станет тихим'
                  % (path.replace(os.sep, '/'), ctx, df_line, manual_line))
            bad += 1
        for ctx, df_line, manual_lines in analyze_read_contexts(text):
            print('  %-64s READ-CTX %s: DF на %d, ручной AddDbContext на %s — контекст получит '
                  'модель без маппингов read-моделей'
                  % (path.replace(os.sep, '/'), ctx, df_line, manual_lines))
            bad += 1
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
        if a.warn_only:
            print('::warning::check_eventstore_registration: %d нарушений (warn-only: известный дефект '
                  'с владельцем — сделать strict после фикса)' % bad)
            return 0
        print('::error::check_eventstore_registration: %d нарушений канона (одна типизированная '
              'регистрация ПОСЛЕ AddEventStorePostgres; для read-контекстов — без ручного '
              'AddDbContext)' % bad)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
