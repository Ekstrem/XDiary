#!/usr/bin/env python
"""check_readstore_mapping.py — канон: read-модель из `AddReadStorePostgres` должна быть в маппинге контекста.

История класса (три BC за сутки, каждый раз «тот же баг, другая папка»):
  1. `journal` — `AddDbContext<ReadDbContext>` перебивал регистрацию DF → `Cannot create a DbSet
     for 'JournalReadModel'` (устранено удалением ручной регистрации, `BE-066`);
  2. `journal` — после этого маппинга не было вовсе → `1d01433`/`BE-068` добавил `DbSet` +
     JSON-конвертеры в `Journal.Storage/ReadDbContext.cs`;
  3. `partners` — то же самое, но правку не сделали: `Partners.Storage/ReadDbContext.cs` содержит
     только `DbSet<PartnerEntity>`, поэтому `PostgresReadRepository.GetAllAsync` падает
     `Cannot create a DbSet for 'PartnersReadModel'` (проверено на dev, образ `26a2937a`).

Правило: если сервис регистрирует read-store как
`AddReadStorePostgres<I<...>, <МОДЕЛЬ>, <КОНТЕКСТ>>`, то в файле `<КОНТЕКСТ>.cs` должна быть
строка `DbSet<МОДЕЛЬ>` **или** `modelBuilder.Entity<МОДЕЛЬ>` / `.Entity<МОДЕЛЬ>()` в маппинге.

    python tools/check_readstore_mapping.py --root 6-msa
    python tools/check_readstore_mapping.py --selftest

rc: 0 — все модели смапплены; 1 — есть немаршенные; 2 — нечего проверять (фильтр сломан).
"""
import argparse
import glob
import os
import re
import sys

# AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(...)
REG_RE = re.compile(
    r'AddReadStorePostgres\s*<\s*[^,>]+(?:\s*<[^>]*>)?\s*,\s*([A-Za-z0-9_.]+)\s*,\s*([A-Za-z0-9_.]+)\s*>',
    re.MULTILINE)


def registrations(text):
    """[(модель, контекст)] из кода регистрации."""
    out = []
    for m in REG_RE.finditer(text):
        out.append((m.group(1).split('.')[-1], m.group(2).split('.')[-1]))
    return out


def mapped(context_text, model):
    """Есть ли маппинг модели в тексте контекста."""
    patterns = [
        r'DbSet\s*<\s*%s\s*>' % re.escape(model),
        r'Entity\s*<\s*%s\s*>' % re.escape(model),
        r'\.Entity\s*<\s*%s\s*>' % re.escape(model),
    ]
    return any(re.search(p, context_text) for p in patterns)


def find_context_file(root, context):
    hits = glob.glob(os.path.join(root, '**', context + '.cs'), recursive=True)
    hits = [h for h in hits if 'obj' + os.sep not in h and 'bin' + os.sep not in h]
    return hits


def selftest():
    ok = total = 0

    def case(reg_text, ctx_text, want_missing, label):
        nonlocal ok, total
        total += 1
        regs = registrations(reg_text)
        if not regs:
            print('  [FAIL] %s: регистрация не распознана' % label)
            return
        missing = [(m, c) for (m, c) in regs if not mapped(ctx_text, m)]
        got = len(missing)
        if got == want_missing:
            ok += 1
        else:
            print('  [FAIL] %s: немаршенных %d, ждали %d' % (label, got, want_missing))

    reg = 'builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(cs, null);'
    case(reg, 'public DbSet<JournalReadModel> JournalRead => Set<JournalReadModel>();', 0,
         'DbSet есть — чисто')
    case(reg, 'public DbSet<RecordEntity> Records => Set<RecordEntity>();', 1,
         'только чужая сущность — дефект')
    case(reg, 'protected override void OnModelCreating(ModelBuilder b) { b.Entity<JournalReadModel>(); }',
         0, 'Entity<> в OnModelCreating — чисто')
    case('builder.Services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(cs, null);',
         'public DbSet<PartnerEntity> Partners => Set<PartnerEntity>();', 1,
         'partners-случай (реальный дефект) — ловится')
    case('builder.Services.AddReadStorePostgres<IX, XRead, Ctx>(cs, null);', 'class Ctx {}', 1,
         'пустой контекст — дефект')

    print('check_readstore_mapping --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Канон: read-модель смаплена в контексте')
    ap.add_argument('--root', default='6-msa')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    programs = glob.glob(os.path.join(a.root, '**', 'Program.cs'), recursive=True)
    programs = [p for p in programs if 'obj' + os.sep not in p and 'bin' + os.sep not in p]
    checked = bad = 0
    for prog in programs:
        text = open(prog, encoding='utf-8', errors='replace').read()
        for model, context in registrations(text):
            checked += 1
            files = find_context_file(a.root, context)
            if not files:
                print('::error::check_readstore_mapping: %s — контекст %s не найден' %
                      (prog.replace(os.sep, '/'), context))
                bad += 1
                continue
            ctx_text = '\n'.join(open(f, encoding='utf-8', errors='replace').read() for f in files)
            if not mapped(ctx_text, model):
                print('  %s: AddReadStorePostgres<%s> без маппинга в %s'
                      % (prog.replace(os.sep, '/'), model, context))
                print('::error::check_readstore_mapping: %s — модель %s не в модели контекста %s '
                      '(сервис упадёт `Cannot create a DbSet for %s`)' %
                      (prog.replace(os.sep, '/'), model, context, model))
                bad += 1

    print('регистраций read-store: %d | немаршенных моделей: %d' % (checked, bad))
    if checked == 0:
        print('::error::check_readstore_mapping: ни одной регистрации не найдено — фильтр сломан')
        return 2
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
