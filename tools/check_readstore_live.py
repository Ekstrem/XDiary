#!/usr/bin/env python
"""check_readstore_live.py — сверка read-модели с ЖИВОЙ базой контура.

Зачем (@TL 2026-11-05 09:14): «проверять не "колонка есть", а **тип** колонки». На dev
`journal_read` не хватает 6 колонок, и это выяснилось падением E2E-спеки — то есть
дороже, чем нужно. Гейт по коду (`check_readstore_ddl`) ловит расхождение DDL↔модель,
а этот инструмент — расхождение **фактической схемы** ↔ модель:

  1. берёт свойства модели из файла `<Модель>.cs` (минус `[NotMapped]`);
  2. читает колонки и типы из `information_schema` контура (через ssh + `podman exec psql`);
  3. разность множеств: поля без колонок → ошибка; типы `uuid/integer/boolean` → сверка.

    # прод: journal_read должна совпадать с JournalReadModel
    python tools/check_readstore_live.py --db journal --table journal_read \
        --model 6-msa/BC-Journal/src/Journal.InternalContracts/JournalReadModel.cs

    python tools/check_readstore_live.py --selftest

rc: 0 — схема полна и типы совпадают; 1 — расхождение; 2 — не измерено (нет доступа/таблицы).
"""
import argparse
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from check_readstore_ddl import EXPECTED_TYPES, TYPE_ALIASES, model_props  # noqa: E402

SQL_TYPES = {'int': 'integer', 'int4': 'integer', 'int8': 'integer', 'bigint': 'integer',
             'smallint': 'integer', 'bool': 'boolean', 'uuid': 'uuid'}


def compare_live(props, cols):
    """cols: {колонка: sqltype}. Возвращает (missing, type_bad)."""
    names = {c.lower() for c in cols}
    missing = sorted(p for p in props if p not in names)
    type_bad = []
    for p in sorted(props):
        want = EXPECTED_TYPES.get(p)
        if not want or p not in names:
            continue
        got = SQL_TYPES.get(cols[p.lower()].lower(), cols[p.lower()].lower())
        if got not in TYPE_ALIASES.get(want, {want}):
            type_bad.append((p, want, got))
    return missing, type_bad


# На проде podman rootless (deploy видит контейнеры сам), на dev — rootful (нужен sudo).
# Поэтому выбираем режим по ФАКТУ наличия контейнера, а не по «есть ли sudo»:
# на проде sudo есть, но root-podman контейнеры deploy не видит (@DO 2026-11-05).
REMOTE = r'''set -u
if podman container exists xdiary-pg 2>/dev/null; then SUDO="";
elif sudo -n podman container exists xdiary-pg 2>/dev/null; then SUDO="sudo -n";
else SUDO=""; fi
$SUDO podman exec xdiary-pg psql -U postgres -d {db} -Atc \
 "select column_name || ':' || data_type from information_schema.columns where table_schema='public' and table_name='{table}' order by ordinal_position;" 2>&1
'''


def selftest():
    ok = total = 0

    def case(props, cols, want_missing, want_types, label):
        nonlocal ok, total
        total += 1
        missing, type_bad = compare_live(props, cols)
        if len(missing) == want_missing and len(type_bad) == want_types:
            ok += 1
        else:
            print('  [FAIL] %s: missing=%s type_bad=%s' % (label, missing, type_bad))

    case({'id', 'durationmin'}, {'id': 'uuid', 'durationmin': 'integer'}, 0, 0, 'полная схема')
    case({'id', 'durationmin'}, {'id': 'uuid'}, 1, 0, 'нет колонки — ловится')
    case({'id'}, {'id': 'text'}, 0, 1, 'uuid объявлен text — ловится')
    case({'deleted'}, {'deleted': 'integer'}, 0, 1, 'boolean объявлен integer — ловится')
    case({'startedat'}, {'startedat': 'text'}, 0, 0, 'text не проверяется')

    print('check_readstore_live --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Сверка read-модели с живой БД')
    ap.add_argument('--db', required=False, default='journal')
    ap.add_argument('--table', required=False, default='journal_read')
    ap.add_argument('--model', required=False,
                    default='6-msa/BC-Journal/src/Journal.InternalContracts/JournalReadModel.cs')
    ap.add_argument('--remote', default='deploy@31.192.111.168')
    ap.add_argument('--key', default='~/.ssh/id_ed25519_xdiary')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    # имя класса = имя файла модели: без этого model_props захватывал вложенный класс
    # (24 вместо 20 полей — расхождение 10 vs 6, @TL 2026-11-05 09:18).
    class_name = os.path.basename(a.model)[:-3] if a.model.endswith('.cs') else None
    props = model_props(open(a.model, encoding='utf-8', errors='replace').read(), class_name)
    script = REMOTE.format(db=a.db, table=a.table)
    cmd = ['ssh', '-i', os.path.expanduser(a.key), '-o', 'BatchMode=yes',
           '-o', 'StrictHostKeyChecking=no', a.remote, 'bash -s']
    try:
        r = subprocess.run(cmd, input=script.encode('utf-8'), stdout=subprocess.PIPE,
                           stderr=subprocess.STDOUT, timeout=180)
        out = (r.stdout or b'').decode('utf-8', 'replace')
    except Exception as exc:
        print('::warning::check_readstore_live: контур недоступен (%s)' % str(exc)[:80])
        return 2

    cols = {}
    for line in out.splitlines():
        line = line.strip()
        if ':' not in line:
            continue
        if any(k in line.upper() for k in ('ERROR', 'FATAL', 'PSQL:', 'DOES NOT EXIST')):
            continue
        name, _, typ = line.partition(':')
        if not name or ' ' in name or not typ:
            continue
        cols[name.lower()] = typ.strip()
    if not cols:
        print('  сырой ответ: %s' % out.strip()[:200])
        print('::error::check_readstore_live: таблица %s.%s не найдена или пустая схема' % (a.db, a.table))
        return 1

    missing, type_bad = compare_live(props, cols)
    print('  модель: %d полей | живая таблица %s.%s: %d колонок'
          % (len(props), a.db, a.table, len(cols)))
    if missing:
        print('::error::check_readstore_live: в %s.%s нет колонок для полей %s'
              % (a.db, a.table, ', '.join(missing)))
    for name, want, got in type_bad:
        print('::error::check_readstore_live: %s.%s объявлена как %s, ожидали %s'
              % (a.db, name, got, want))
    if missing or type_bad:
        return 1
    print('  вердикт: схема полна, типы совпадают')
    return 0


if __name__ == '__main__':
    sys.exit(main())
