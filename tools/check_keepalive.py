#!/usr/bin/env python
"""Гейт «keepalive не спамит» (находка @DO 2026-11-04: 4 пинга одной роли за 5 минут).

Правило, объявленное владельцем механизма: пинг роли пишется, если она молчит **≥15 мин**.
Проверка ловит нарушение по факту: группируем строки `keepalive` по адресату и по дате,
считаем интервалы; интервал меньше порога → пинг-спам.

Зачем гейт: «тишина 30 мин» в пинге читается как «роль не работает», хотя роль в это время
работала. Это не шум, а ложный сигнал координатору, и он же портит метрику простоя.

    python tools/check_keepalive.py                  # предупреждения (rc=0)
    python tools/check_keepalive.py --strict         # rc=1 при спаме
    python tools/check_keepalive.py --selftest
"""
import argparse
import os
import re
import shutil
import subprocess
import sys

CHAT = os.path.join("5-tech-backlog", "chat")
STAMP = re.compile(r'^- \[(\d{4}-\d{2}-\d{2}) (\d{2}):(\d{2})\] → @([A-Za-z]+):')
THRESHOLD_MIN = 15


def pings(chat_dir, addressee=None):
    """[(дата, минуты_суток, роль)] из всех чатов."""
    out = []
    for name in sorted(os.listdir(chat_dir)):
        if not name.endswith('.md'):
            continue
        for line in open(os.path.join(chat_dir, name), encoding='utf-8', errors='replace'):
            if 'keepalive' not in line:
                continue
            m = STAMP.match(line)
            if not m:
                continue
            role = m.group(4)
            if addressee and role != addressee:
                continue
            out.append((m.group(1), int(m.group(2)) * 60 + int(m.group(3)), role))
    return out


def violations(items, threshold=THRESHOLD_MIN):
    """[(роль, метка_a, метка_b, интервал)] — пинги одной роли чаще порога."""
    items = sorted(items, key=lambda x: (x[0], x[1]))
    bad = []
    by_role = {}
    for day, mins, role in items:
        by_role.setdefault((role, day), []).append(mins)
    for (role, day), mins in by_role.items():
        mins.sort()
        for a, b in zip(mins, mins[1:]):
            if b - a < threshold:
                bad.append((role, a, b, b - a))
    return bad


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-keepalive')
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    try:
        chat = os.path.join(base, '5-tech-backlog', 'chat')
        os.makedirs(chat)
        # кейс 1: спам — три пинга за 5 минут
        open(os.path.join(chat, 'tl.md'), 'w', encoding='utf-8').write(
            '- [2026-11-04 08:29] → @DO: keepalive — тишина 25 мин\n'
            '- [2026-11-04 08:30] → @DO: keepalive — тишина 26 мин\n'
            '- [2026-11-04 08:33] → @DO: keepalive — тишина 30 мин\n')
        got = violations(pings(chat))
        if len(got) == 2 and all(v[3] < THRESHOLD_MIN for v in got):
            ok += 1
        else:
            print('  [FAIL] спам не пойман: %s' % got)
        # кейс 2: норма — интервал 20 минут
        open(os.path.join(chat, 'tl.md'), 'w', encoding='utf-8').write(
            '- [2026-11-04 08:00] → @DO: keepalive — тишина 15 мин\n'
            '- [2026-11-04 08:20] → @DO: keepalive — тишина 16 мин\n')
        if violations(pings(chat)) == []:
            ok += 1
        else:
            print('  [FAIL] норма помечена как спам')
    finally:
        shutil.rmtree(base, ignore_errors=True)
    print('check_keepalive --selftest: %d/2' % ok)
    return 0 if ok == 2 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--strict', action='store_true')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    chat = os.path.join(root, CHAT)
    if not os.path.isdir(chat):
        print('::warning::check_keepalive: нет %s' % CHAT)
        return 0
    items = pings(chat)
    bad = violations(items)
    for role, x, y, gap in bad[:20]:
        msg = ('keepalive → @%s: два пинга с интервалом %d мин (порог %d) — %02d:%02d и %02d:%02d'
               % (role, gap, THRESHOLD_MIN, x // 60, x % 60, y // 60, y % 60))
        print(('::error::' if a.strict else '::warning::') + msg)
    print('check_keepalive: пингов %d, нарушений порога %d (порог %d мин)'
          % (len(items), len(bad), THRESHOLD_MIN))
    if a.strict and bad:
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
