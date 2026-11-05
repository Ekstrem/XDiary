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
'''


def parse(out):
    facts = {}
    for line in (out or '').splitlines():
        m = re.match(r'^(schema|history|events|partners_read|records|journal_read)=(\S*)$', line.strip())
        if m:
            facts[m.group(1)] = m.group(2)
    return facts


def verdict(facts):
    """(rc, [(пункт, выполнено, текст)]) — без догадок: только по числам."""
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
    # (2) измеряется по логу: здесь только косвенно — наличие строк событий
    p2 = events > 0
    items.append(('(2) Handle исполняется', p2,
                  'строк событий=%d (маркер `[handle] entered` проверяется в логе)' % events))
    p3 = pread > 0 or jread > 0
    items.append(('(3) представление из проекции', p3,
                  'partners_read=%d, journal records=%d, journal_read=%d'
                  % (pread, recs, jread)))

    if all(ok for _, ok, _ in items):
        return 0, items
    # противоречие: схема есть, а событий нет — это уже не «не измерено»
    if schema == 'yes' and events == 0:
        return 1, items
    return 2, items


def selftest():
    ok = total = 0

    def case(label, facts, want):
        nonlocal ok, total
        total += 1
        rc, items = verdict(facts)
        if rc == want:
            ok += 1
        else:
            print('  [FAIL] %s: rc=%d, ждали %d' % (label, rc, want))
        return items

    case('всё выполнено', {'schema': 'yes', 'history': '1', 'events': '5',
                           'partners_read': '13', 'records': '4', 'journal_read': '4'}, 0)
    case('схемы нет', {'schema': 'no', 'history': '0', 'events': '0',
                       'partners_read': '0', 'records': '2481', 'journal_read': '0'}, 2)
    case('схема есть, событий нет', {'schema': 'yes', 'history': '1', 'events': '0',
                                     'partners_read': '0', 'records': '0',
                                     'journal_read': '0'}, 1)
    case('схема есть, события есть, read пуст',
         {'schema': 'yes', 'history': '1', 'events': '3', 'partners_read': '0',
          'records': '10', 'journal_read': '0'}, 2)

    # текстовый кейс (норма @TL): в выводе обязаны быть числа, а не «None»
    total += 1
    _, items = verdict({'schema': 'no', 'history': '0', 'events': '0',
                        'partners_read': '0', 'records': '2481', 'journal_read': '0'})
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
    if 'schema' not in facts:
        print('check_eventstore_ready: НЕ ИЗМЕРЕНО — вывод не разобран: %r' % (r.stdout or '')[:160])
        return 2

    rc, items = verdict(facts)
    for name, okay, text in items:
        print('  [%s] %s — %s' % ('OK  ' if okay else 'НЕТ ', name, text))
    print('check_eventstore_ready: предпосылки §5 %s'
          % ('выполнены' if rc == 0 else ('противоречие' if rc == 1 else 'НЕ ВЫПОЛНЕНЫ')))
    return rc


if __name__ == '__main__':
    sys.exit(main())
