#!/usr/bin/env python
"""check_env_hosts.py — гейт: `Host=` в `.env` должен соответствовать контуру.

Инцидент 2026-11-04/05: в прод-`secrets/.env` десять строк `ConnectionStrings__*`
указывали на **dev-хост** `77.91.73.67` вместо прод-БД `10.89.9.10`. Прод-сервис при
пересоздании стучался в чужую БД и падал `28P01`; хуже — мог бы **писать** в dev.
Класс дефекта: «конфиг контура не проверяется», и до первого пересоздания он невидим.

Гейт проверяет **имя хоста**, а не пароль: значения паролей не читаются и не печатаются.

    python tools/check_env_hosts.py --env /opt/xdiary/7-deploy/secrets/.env --expect 10.89.9.10
    python tools/check_env_hosts.py --selftest

rc: 0 — все строки указывают на ожидаемый хост; 1 — есть чужие хосты (список в выводе);
2 — файл не найден/нет строк ConnectionStrings (проверять нечего — это не «чисто»).
"""
import argparse
import os
import re
import sys

# хосты контуров: прод и dev — разные машины, и это ровно та пара, которую перепутали
KNOWN = {'10.89.9.10': 'прод-БД (контур podman на VPS-1)', '77.91.73.67': 'dev-БД (VPS-2)'}


def parse_hosts(text):
    """Возвращает список (ключ, host) из строк `ConnectionStrings__<Name>=…Host=<h>;…`.

    Пароли не извлекаются: нам нужен только `Host=`.
    """
    rows = []
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith('ConnectionStrings__') or '=' not in line:
            continue
        key, value = line.split('=', 1)
        m = re.search(r'(?:^|;)Host=([^;]*)', value)
        rows.append((key.replace('ConnectionStrings__', ''), (m.group(1).strip() if m else '')))
    return rows


def check(rows, expect):
    bad = [(k, h) for k, h in rows if h != expect]
    return bad


def selftest():
    ok = total = 0

    def case(text, expect, want_bad):
        nonlocal ok, total
        total += 1
        rows = parse_hosts(text)
        bad = check(rows, expect)
        if len(bad) == want_bad:
            ok += 1
        else:
            print('  [FAIL] %r -> bad=%s, ждали %d' % (text[:60], bad, want_bad))

    case('ConnectionStrings__JournalDb=Host=10.89.9.10;Port=5432;Database=journal', '10.89.9.10', 0)
    case('ConnectionStrings__JournalDb=Host=77.91.73.67;Port=5432;Database=journal', '10.89.9.10', 1)
    case('ConnectionStrings__A=Host=10.89.9.10;Password=x\r\nConnectionStrings__B=Host=77.91.73.67;Password=y',
         '10.89.9.10', 1)
    case('ConnectionStrings__A=Host=10.89.9.10;Password=x\r\nConnectionStrings__B=Host=10.89.9.10;Password=y',
         '10.89.9.10', 0)
    # ключ случая: пароль в значении не должен влиять на результат и не должен печататься
    case('ConnectionStrings__A=Host=zzz;Password=secret-value', '10.89.9.10', 1)
    # CRLF из Windows не должен ломать разбор
    case('ConnectionStrings__A=Host=10.89.9.10;Password=p\r', '10.89.9.10', 0)

    if 'secret-value' not in repr(check(parse_hosts('ConnectionStrings__A=Host=zzz;Password=secret-value'),
                                       '10.89.9.10')):
        ok += 1
    else:
        print('  [FAIL] пароль попал в вывод проверки — так нельзя')
    total += 1

    print('check_env_hosts --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Host= в .env против ожидаемого контура')
    ap.add_argument('--env', default='/opt/xdiary/7-deploy/secrets/.env')
    ap.add_argument('--expect', default='10.89.9.10', help='ожидаемый хост БД контура')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    if not os.path.exists(a.env):
        print('::error::check_env_hosts: нет файла %s' % a.env)
        return 2
    rows = parse_hosts(open(a.env, encoding='utf-8', errors='replace').read())
    if not rows:
        print('::error::check_env_hosts: в %s нет строк ConnectionStrings__* — проверять нечего' % a.env)
        return 2

    bad = check(rows, a.expect)
    print('  ожидаемый хост: %s (%s)' % (a.expect, KNOWN.get(a.expect, 'не из списка известных')))
    print('  строк ConnectionStrings: %d | соответствуют: %d | чужих: %d'
          % (len(rows), len(rows) - len(bad), len(bad)))
    for key, host in bad:
        print('   ЧУЖОЙ ХОСТ: %s -> %s (%s)' % (key, host or 'НЕТ Host=', KNOWN.get(host, 'неизвестный')))
    if bad:
        print('::error::check_env_hosts: %d строк смотрят не на тот контур — это инцидент, а не мелочь' % len(bad))
        return 1
    print('check_env_hosts: все строки указывают на %s — OK' % a.expect)
    return 0


if __name__ == '__main__':
    sys.exit(main())
