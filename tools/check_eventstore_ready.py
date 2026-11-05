#!/usr/bin/env python3
"""check_eventstore_ready.py — три предпосылки §5 перед раскладкой схем (ADR-034).

@AR 2026-11-06 00:05: работы по раскладке НЕ начинаются, пока не выполнены:
  (1) событие сохраняется — схема event store есть и после POST появляется строка;
  (2) `Handle` исполняется — обход `IObserver`-адаптером (маркер `[handle] entered` в логе);
  (3) представление строится проекцией, а не ремонтом (read-модель непуста без ручных INSERT).

Инструмент ничего не меняет: он только печатает три факта и вердикт.
`rc=0` — все три выполнены (можно начинать раскладку), `rc=2` — не все (НЕ ИЗМЕРЕНО),
`rc=1` — измерено и есть противоречие (например схема есть, а строк нет).

    python tools/check_eventstore_ready.py --remote deploy@31.192.111.168
    python tools/check_eventstore_ready.py --selftest
"""
import argparse
import re
import subprocess
import sys

REMOTE = r'''set -u
if podman container exists xdiary-pg 2>/dev/null; then SUDO="";
elif sudo -n podman container exists xdiary-pg 2>/dev/null; then SUDO="sudo -n";
else SUDO=""; fi
Q() { $SUDO podman exec xdiary-pg psql -U postgres -d "$1" -Atc "$2" 2>/dev/null; }
echo "schema=$(Q partners "select case when to_regclass('\"Commands\".\"DomainEvents\"') is null then 'no' else 'yes' end;")"
echo "history=$(Q partners "select count(*) from \"__EFMigrationsHistory\" where \"MigrationId\" ilike '%EventStore%';")"
echo "events=$(Q partners "select count(*) from \"Commands\".\"DomainEvents\";")"
echo "partners_read=$(Q partners "select count(*) from partners_read;")"
echo "records=$(Q journal "select count(*) from records;")"
echo "journal_read=$(Q journal "select count(*) from journal_read;")"
echo "journal_read_updated=$(Q journal "select coalesce(max(\"UpdatedAt\")::text,'none') from journal_read;")"
echo "handle_entered=$($SUDO podman logs xdiary-partners 2>&1 | grep -c '\[handle\] entered' || true)"
'''


def parse(out):
    facts = {}
    for line in (out or '').splitlines():
        m = re.match(r'^(schema|history|events|partners_read|records|journal_read|journal_read_updated|handle_entered)=([^\s]+)$', line.strip())
        if m:
            facts[m.group(1)] = m.group(2)
    return facts


def verdict(facts):
    """(rc, [(пункт, выполнено, текст)], provenance) — без догадок: только по числам."""
    schema = facts.get('schema')
    hist = int(facts.get('history') or 0)
    events = int(facts.get('events') or 0)
    pread = int(facts.get('partners_read') or 0)
    recs = int(facts.get('records') or 0)
    jread = int(facts.get('journal_read') or 0)

    items = []
    p1 = schema == 'yes' and hist > 0
    items.append(('(1) событие сохраняется',
                  p1,
                  'schema=%s, миграций EventStore=%d, строк в DomainEvents=%d'
                  % (schema, hist, events)))
    # (2) — ПО ЛОГУ: `[handle] entered`. Строки событий для этого не годятся: их нет
    # и тогда, когда консьюмер просто не смог записать (нет схемы) — разные дефекты.
    # @TL 2026-11-05 20:17: маркер в логе — плохой критерий САМ ПО СЕБЕ: после перезапуска
    # лог пуст, а строки в БД остаются (124 события / 123 представления / маркер 0 ⇒ ложный
    # красный). Поэтому (2) = маркер>0 ЛИБО «представления догнали события».
    handle = int(facts.get('handle_entered') or 0)
    window = facts.get('window') or 'весь лог текущего контейнера (с момента старта)'
    by_log = handle > 0
    by_ratio = events > 0 and pread > 0 and pread + 1 >= events
    p2 = by_log or by_ratio
    if by_log:
        basis = 'маркер `[handle] entered`=%d, окно: %s' % (handle, window)
    elif by_ratio:
        basis = ('маркер=0 (лог пуст после перезапуска), но представления догнали '
                 'события: partners_read=%d при событиях=%d' % (pread, events))
    else:
        basis = ('маркер=0 (окно: %s) и представления НЕ догнали события: %d при %d'
                 % (window, pread, events))
    items.append(('(2) Handle исполняется', p2, basis))
    # @AR 2026-11-05 16:51: «числа непусты» ≠ «проекция работает». На проде read-модель
    # заполнена ручным ремонтом `repair-2026-11-05-readmodels.sql`. Доказательство (3) —
    # провенанс: строки read-модели обновлены ПОЗЖЕ ремонта, либо пересборка дала те же числа.
    # ОГРАНИЧЕНИЕ: `partners_read` не имеет `UpdatedAt`, поэтому по времени он не проверяется
    # вовсе — для него доказательством может быть только пересборка (способ (а) @AR).
    updated = facts.get('journal_read_updated', 'none')
    repaired_at = facts.get('repaired_at')
    # Лексика та же, что у @TE в `accept_read_path --projection-provenance`:
    # rebuild | time | repair | none — чтобы квитанции двух гейтов читались одинаково.
    override = facts.get('provenance')
    if override in ('rebuild', 'time'):
        provenance = override
    elif pread == 0 and jread == 0:
        provenance = 'none'
    elif jread > 0 and updated not in ('none', '') and repaired_at and updated > repaired_at:
        provenance = 'time'
    else:
        provenance = 'repair'
    provenance_ok = provenance in ('rebuild', 'time')
    p3 = provenance_ok
    items.append(('(3) представление из проекции', p3,
                  'partners_read=%d (без UpdatedAt — провенанс не проверяем), '
                  'journal records=%d, journal_read=%d, max UpdatedAt=%s, ремонт=%s, '
                  'провенанс=%s'
                  % (pread, recs, jread, updated, repaired_at or 'не задан', provenance)))

    if all(ok for _, ok, _ in items):
        return 0, items, provenance
    # Лексика вердиктов согласована с @TE (`accept_read_path --projection-provenance`):
    # `repair` — НАРУШЕНИЕ §5 п.3 (rc=1), «провенанс не указан/строк нет» — НЕ ИЗМЕРЕНО (rc=2).
    if provenance == 'repair':
        return 1, items, provenance
    # противоречие: схема есть, а событий нет — тоже не «не измерено»
    if schema == 'yes' and events == 0:
        return 1, items, provenance
    return 2, items, provenance


def selftest():
    ok = total = 0

    def case(label, facts, want):
        nonlocal ok, total
        total += 1
        rc, items, _ = verdict(facts)
        if rc == want:
            ok += 1
        else:
            print('  [FAIL] %s: rc=%d, ждали %d' % (label, rc, want))
        return items

    # Провенанс (3): строка read-модели должна быть обновлена ПОЗЖЕ ремонта.
    REP = '2026-11-05'
    case('всё выполнено (провенанс после ремонта)',
         {'schema': 'yes', 'history': '1', 'events': '5', 'partners_read': '13',
          'records': '4', 'journal_read': '4',
          'journal_read_updated': '2026-11-06T10:00:00+00:00', 'repaired_at': REP,
          'handle_entered': '6'}, 0)
    # @TE 16:56: `repair` — нарушение §5 п.3 (rc=1), а не «не измерено»
    case('read непуст, но ВСЁ до ремонта (ручной ремонт) -> нарушение',
         {'schema': 'yes', 'history': '1', 'events': '5', 'partners_read': '13',
          'records': '4', 'journal_read': '4',
          'journal_read_updated': '2026-11-03T17:03:18+00:00', 'repaired_at': REP,
          'handle_entered': '6'}, 1)
    case('явно передан rebuild + Handle вызывается -> принято',
         {'schema': 'yes', 'history': '1', 'events': '5', 'partners_read': '13',
          'records': '4', 'journal_read': '4', 'journal_read_updated': '2026-11-03T00:00:00+00:00',
          'repaired_at': REP, 'provenance': 'rebuild', 'handle_entered': '6'}, 0)
    case('явно передан repair -> нарушение',
         {'schema': 'yes', 'history': '1', 'events': '5', 'partners_read': '13',
          'records': '4', 'journal_read': '4', 'repaired_at': REP,
          'provenance': 'repair', 'handle_entered': '6'}, 1)
    case('схемы нет', {'schema': 'no', 'history': '0', 'events': '0', 'partners_read': '0',
                       'records': '2481', 'journal_read': '0', 'repaired_at': REP}, 2)
    case('маркер 0, но представления догнали события (ложный красный) -> (2) ЗАСЧИТАН',
         {'schema': 'yes', 'history': '1', 'events': '124', 'partners_read': '123',
          'records': '4', 'journal_read': '22', 'repaired_at': REP,
          'handle_entered': '0', 'provenance': 'rebuild'}, 0)
    case('Handle НЕ вызывается (маркер 0) -> пункт 2 не выполнен',
         {'schema': 'no', 'history': '0', 'events': '0', 'partners_read': '0',
          'records': '0', 'journal_read': '0', 'repaired_at': REP,
          'handle_entered': '0'}, 2)
    case('схема есть, событий нет',
         {'schema': 'yes', 'history': '1', 'events': '0', 'partners_read': '0',
          'records': '0', 'journal_read': '0', 'repaired_at': REP}, 1)
    case('схема есть, события есть, read пуст',
         {'schema': 'yes', 'history': '1', 'events': '3', 'partners_read': '0',
          'records': '10', 'journal_read': '0', 'repaired_at': REP}, 2)

    # текстовый кейс (норма @TL): в выводе обязаны быть числа, а не «None»
    total += 1
    _, items, _ = verdict({'schema': 'no', 'history': '0', 'events': '0',
                        'partners_read': '0', 'records': '2481', 'journal_read': '0',
                        'repaired_at': '2026-11-05'})
    text = ' | '.join(t for _, _, t in items)
    if 'миграций EventStore=0' in text and 'journal records=2481' in text and 'None' not in text:
        ok += 1
    else:
        print('  [FAIL] текст без чисел или с None: %r' % text)

    print('check_eventstore_ready --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--remote', default='deploy@31.192.111.168')
    ap.add_argument('--key', default='~/.ssh/id_ed25519_xdiary')
    ap.add_argument('--projection-provenance', default=None,
                    choices=('rebuild', 'time', 'repair', 'none'),
                    help='как доказан провенанс (та же лексика, что у @TE в accept_read_path)')
    ap.add_argument('--window', default=None,
                    help='окно замера счётчика (например «60 с после старта»); '
                         'по умолчанию — весь лог текущего контейнера')
    ap.add_argument('--repaired-at', default='2026-11-05',
                    help='дата ручного ремонта read-модели: строки с UpdatedAt '
                         'раньше неё не доказывают проекцию')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    cmd = ['ssh', '-i', a.key, '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=no',
           a.remote, REMOTE]
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8',
                           errors='replace', timeout=180)
    except Exception as ex:  # noqa: BLE001
        print('check_eventstore_ready: НЕ ИЗМЕРЕНО — %s' % ex)
        return 2
    if r.returncode != 0:
        print('check_eventstore_ready: НЕ ИЗМЕРЕНО — ssh rc=%d, %s'
              % (r.returncode, (r.stderr or '')[:160]))
        return 2

    facts = parse(r.stdout)
    facts['repaired_at'] = a.repaired_at
    if a.window:
        facts['window'] = a.window
    if a.projection_provenance:
        facts['provenance'] = a.projection_provenance
    if 'schema' not in facts:
        print('check_eventstore_ready: НЕ ИЗМЕРЕНО — вывод не разобран: %r' % (r.stdout or '')[:160])
        return 2

    rc, items, provenance = verdict(facts)
    for name, okay, text in items:
        print('  [%s] %s — %s' % ('OK  ' if okay else 'НЕТ ', name, text))
    if not items[2][1]:
        print('  примечание: непустая read-модель НЕ доказывает проекцию, если '
              'max(UpdatedAt) строк раньше ремонта (%s) — это может быть ручной ремонт '
              '(repair-2026-11-05-readmodels.sql), а не работа проекции'
              % (facts.get('repaired_at') or 'не задан'))
    print('  провенанс представления: %s%s'
          % (provenance, '' if provenance in ('rebuild', 'time')
             else '  (НЕ доказательство проекции)'))
    print('check_eventstore_ready: предпосылки §5 %s'
          % ('выполнены' if rc == 0 else ('противоречие' if rc == 1 else 'НЕ ВЫПОЛНЕНЫ')))
    return rc


if __name__ == '__main__':
    sys.exit(main())
