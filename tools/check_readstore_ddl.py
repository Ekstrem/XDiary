#!/usr/bin/env python
"""check_readstore_ddl.py — канон: поля read-модели ⊆ колонки DDL read-таблицы.

Причина (@AR 2026-11-05 15:00): `CREATE TABLE IF NOT EXISTS journal_read` в
`Journal.Api/Program.cs` объявляет **14** колонок, а в `JournalReadModel` — **24** поля.
EF выбирает все маппированные колонки, поэтому после создания таблицы ошибка меняется
с `42P01 (таблицы нет)` на `42703 (колонки нет)` — дефект переезжает, а не закрывается.
На dev это видно как `column j.ContraceptiveCount does not exist`.

Что проверяется:
  1. из `Program.cs` берутся `CREATE TABLE IF NOT EXISTS <table> (...)` и список колонок;
  2. из файла модели `<Модель>.cs` — публичные свойства (`public T Name { get; ... }`),
     кроме помеченных `[NotMapped]`;
  3. таблица для модели ищется по имени: `JournalReadModel` → `journal_read`,
     `PartnersReadModel` → `partners_read`, `PartnersDirectoryReadModel` → `partners_directory`;
  4. считается **разность множеств**: поля модели, которых нет в DDL → дефект (`::error::`).
     Колонки DDL без поля модели — предупреждение (может быть служебная).

    python tools/check_readstore_ddl.py --root 6-msa
    python tools/check_readstore_ddl.py --selftest

rc: 0 — расхождений нет; 1 — есть; 2 — проверить нечего (фильтр сломан).
"""
import argparse
import glob
import os
import re
import sys

REG = re.compile(
    r'AddReadStorePostgres\s*<\s*[^,>]+(?:\s*<[^>]*>)?\s*,\s*([A-Za-z0-9_.]+)\s*,\s*([A-Za-z0-9_.]+)\s*>')
# DDL лежит в C#-строке, поэтому кавычки экранированы: \"Id\" — допускаем оба варианта.
DDL = re.compile(r'CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+\\?"?([A-Za-z0-9_]+)\\?"?\s*\((.*?)\)\s*;',
                 re.IGNORECASE | re.DOTALL)
COL = re.compile(r'\\?"([A-Za-z0-9_]+)\\?"\s+[A-Za-z]')
PROP = re.compile(r'public\s+(?:virtual\s+)?[A-Za-z0-9_<>\[\],\.\?\s]+?\s+([A-Za-z0-9_]+)\s*\{\s*get')


def snake(model):
    """JournalReadModel -> journal_read; PartnersDirectoryReadModel -> partners_directory."""
    # срезается только «Model»: «JournalRead» -> journal_read, «PartnersRead» -> partners_read
    base = model[:-len('Model')] if model.endswith('Model') else model
    return re.sub(r'(?<!^)(?=[A-Z])', '_', base).lower()


def ddl_tables(text):
    """{таблица: {колонки}} из текста Program.cs."""
    out = {}
    for m in DDL.finditer(text):
        table = m.group(1).lower()
        cols = {c.lower() for c in COL.findall(m.group(2))}
        if cols:
            out[table] = cols
    return out


def model_props(text):
    """{свойства} из текста модели, кроме [NotMapped]."""
    props, skip_next = set(), False
    for line in text.splitlines():
        if '[NotMapped]' in line:
            skip_next = True
            continue
        m = PROP.search(line)
        if m:
            if skip_next:
                skip_next = False
                continue
            props.add(m.group(1).lower())
        elif line.strip() and not line.strip().startswith('//'):
            skip_next = False
    return props


def resolve_table(model, tables):
    """Имя таблицы для модели: точное snake, затем без суффикса `_read` (правило неоднородно)."""
    full = snake(model)
    candidates = [full]
    if full.endswith('_read'):
        candidates.append(full[:-len('_read')])
    for c in candidates:
        if c in tables:
            return c
    return None


def compare(model, props, ddl_cols):
    """(missing, extra) — разности множеств; missing приоритетнее."""
    missing = sorted(props - ddl_cols)
    extra = sorted(ddl_cols - props)
    return missing, extra


def selftest():
    ok = total = 0

    def case(props, ddl_cols, want_missing, label):
        nonlocal ok, total
        total += 1
        missing, _extra = compare('M', set(props), set(ddl_cols))
        if len(missing) == want_missing:
            ok += 1
        else:
            print('  [FAIL] %s: missing=%s, ждали %d' % (label, missing, want_missing))

    case({'id', 'clientid', 'partners'}, {'id', 'clientid', 'partners'}, 0, 'равные множества')
    case({'id', 'contraceptivecount'}, {'id', 'contraceptives'}, 1,
         'journal-случай: ContraceptiveCount нет в DDL')
    case({'id', 'a', 'b', 'c'}, {'id'}, 3, 'три поля без колонок')
    case({'id'}, {'id', 'служебная'}, 0, 'лишняя колонка DDL — не дефект')

    # разрешение имени таблицы по кандидатам (правило неоднородно: partners_directory vs partners_read)
    total += 3
    if resolve_table('JournalReadModel', {'journal_read', 'partners'}) == 'journal_read':
        ok += 1
    else:
        print('  [FAIL] resolve JournalReadModel:', resolve_table('JournalReadModel', {'journal_read'}))
    if resolve_table('PartnersDirectoryReadModel', {'partners_directory'}) == 'partners_directory':
        ok += 1
    else:
        print('  [FAIL] resolve PartnersDirectoryReadModel')
    if resolve_table('UnknownModel', {'x'}) is None:
        ok += 1
    else:
        print('  [FAIL] неизвестная модель не должна разрешаться')

    # разбор DDL и свойств
    total += 2
    t = ddl_tables('CREATE TABLE IF NOT EXISTS journal_read ("Id" uuid PRIMARY KEY, "ClientId" uuid NULL);')
    if t.get('journal_read') == {'id', 'clientid'}:
        ok += 1
    else:
        print('  [FAIL] разбор DDL:', t)
    p = model_props('public Guid Id { get; set; }\n[NotMapped]\npublic string Skip { get; set; }')
    if p == {'id'}:
        ok += 1
    else:
        print('  [FAIL] разбор свойств (NotMapped):', p)

    print('check_readstore_ddl --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Канон: поля read-модели ⊆ колонки DDL')
    ap.add_argument('--root', default='6-msa')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    checked = bad = 0
    for prog in glob.glob(os.path.join(a.root, '**', 'Program.cs'), recursive=True):
        if 'obj' + os.sep in prog or 'bin' + os.sep in prog:
            continue
        text = open(prog, encoding='utf-8', errors='replace').read()
        tables = ddl_tables(text)
        for m in REG.finditer(text):
            model = m.group(1).split('.')[-1]
            table = resolve_table(model, set(tables))
            if table is None:
                continue
            checked += 1
            files = [f for f in glob.glob(os.path.join(a.root, '**', model + '.cs'), recursive=True)
                     if 'obj' + os.sep not in f and 'bin' + os.sep not in f]
            if not files:
                print('::warning::check_readstore_ddl: модель %s не найдена — сверка не выполнена' % model)
                continue
            props = set()
            for f in files:
                props |= model_props(open(f, encoding='utf-8', errors='replace').read())
            missing, extra = compare(model, props, tables[table])
            if missing:
                bad += 1
                print('  %s: DDL таблицы %s не содержит колонок для полей %s'
                      % (prog.replace(os.sep, '/'), table, ', '.join(missing)))
                print('::error::check_readstore_ddl: %s — %s: в DDL нет колонок %s '
                      '(EF сгенерирует запрос по ним → 42703)' %
                      (prog.replace(os.sep, '/'), table, ', '.join(missing)))
            elif extra:
                print('  %s: таблица %s имеет колонки без полей модели: %s (не дефект)'
                      % (prog.replace(os.sep, '/'), table, ', '.join(extra)))

    print('сверено моделей с DDL: %d | расхождений: %d' % (checked, bad))
    if checked == 0:
        print('::error::check_readstore_ddl: ни одной модели с DDL не найдено — фильтр сломан')
        return 2
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
