#!/usr/bin/env python
"""check_read_model_freshness.py — «200 и непусто»: сверка read-эндпоинта с числом строк в БД.

Зачем (уточнение @TL 2026-11-05 08:22): read-модели (`journal_read`, `partners_read`) пишутся
**только проекцией событий**, бэкфилла нет (`BE-071`). Поэтому `GET /partners` → `200` сам по
себе доказывает лишь то, что запрос не упал: список может быть **пустым** при непустой базе.
Критерий приёмки — `200` **и** число элементов равно `SELECT count(*)` по таблице-источнику.

Что делает:
  1. считает строки в БД контура (`--db-name`, таблица `--db-table`);
  2. вызывает read-эндпоинт изнутри сети контура (`--endpoint`);
  3. сравнивает: `элементов == строк` → ок; пусто при непустой БД → дефект (проекция/бэкфилл);
     500/ошибка → дефект с текстом причины (например `42P01 relation … does not exist`).

    # journal (эндпоинт anonymous-inside, токен не нужен)
    python tools/check_read_model_freshness.py --service journal \
        --endpoint http://xdiary-journal:8080/internal/journal/records \
        --db-name journal --db-table records

    # partners (нужен client-JWT; без него гейт честно вернёт rc=2 «не измерено»)
    python tools/check_read_model_freshness.py --service partners \
        --endpoint http://xdiary-partners:8080/api/v1/partners \
        --db-name partners --db-table partners --token "$TOKEN"

    python tools/check_read_model_freshness.py --selftest

rc: 0 — число совпало и непусто (или пусто при пустой БД); 1 — расхождение/ошибка; 2 — не измерено.
"""
import argparse
import json
import os
import subprocess
import sys

SSH_KEY = '~/.ssh/id_ed25519_xdiary'
CURL_IMAGE = 'docker.io/curlimages/curl:8.10.1'


def run_remote(remote, key, script, timeout=300):
    cmd = ['ssh', '-i', os.path.expanduser(key), '-o', 'BatchMode=yes',
           '-o', 'StrictHostKeyChecking=no', remote, 'bash -s']
    r = subprocess.run(cmd, input=script.replace('\r\n', '\n').encode('utf-8'),
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
    return (r.stdout or b'').decode('utf-8', 'replace')


REMOTE = r'''set -u
echo "DB"
podman exec xdiary-pg psql -U postgres -d {db} -Atc 'select count(*) from {table};' 2>&1 | tail -1
echo "HTTP"
{curl_cmd}
echo ""
echo "BODY"
{body_cmd}
'''


def classify(db_count, http_code, body, token_required):
    """(ok, verdict, detail) — чистая логика, её и проверяет selftest."""
    if db_count is None:
        return False, 'НЕ ИЗМЕРЕНО', 'не удалось посчитать строки в БД'
    if http_code in (None, '000'):
        return False, 'НЕ ИЗМЕРЕНО', 'эндпоинт не ответил'
    if http_code == '401' and token_required:
        return False, 'НЕ ИЗМЕРЕНО', '401: нужен client-JWT (передай --token) — иначе это не проверка'
    if http_code != '200':
        return False, 'НАРУШЕНИЕ', 'код %s (ожидали 200); тело: %s' % (http_code, (body or '')[:180])
    try:
        data = json.loads(body or '[]')
    except ValueError:
        return False, 'НАРУШЕНИЕ', 'тело не JSON: %s' % (body or '')[:120]
    items = data if isinstance(data, list) else (
        data.get('items') or data.get('content') or data.get('data') or [])
    if not isinstance(items, list):
        return False, 'НАРУШЕНИЕ', 'в ответе нет списка'
    if db_count == 0 and len(items) == 0:
        return True, 'ОК', 'пусто и в БД пусто — сравнение корректно'
    if len(items) == 0 and db_count > 0:
        return False, 'НАРУШЕНИЕ', 'эндпоинт пуст, а в БД %d строк (read-модель не заполнена: ' \
                                   'проекция без бэкфилла, BE-071)' % db_count
    if len(items) != db_count:
        return False, 'НАРУШЕНИЕ', 'элементов %d, строк в БД %d' % (len(items), db_count)
    return True, 'ОК', 'элементов %d == строк %d' % (len(items), db_count)


def selftest():
    ok = total = 0

    def case(args, want_ok, want_verdict, label):
        nonlocal ok, total
        total += 1
        good, verdict, _ = classify(*args)
        if good == want_ok and verdict == want_verdict:
            ok += 1
        else:
            print('  [FAIL] %s: ok=%s verdict=%s (ждали ok=%s %s)' %
                  (label, good, verdict, want_ok, want_verdict))

    case((5, '200', '[{}, {}, {}, {}, {}]', False), True, 'ОК', 'совпало 5 == 5')
    case((5, '200', '[]', False), False, 'НАРУШЕНИЕ', 'пусто при 5 строках — дефект BE-071')
    case((0, '200', '[]', False), True, 'ОК', 'пусто и база пуста — корректно')
    case((5, '200', '[{}]', False), False, 'НАРУШЕНИЕ', '1 против 5 — расхождение')
    case((None, '200', '[]', False), False, 'НЕ ИЗМЕРЕНО', 'БД не посчиталась')
    case((5, '500', 'relation "journal_read" does not exist', False), False, 'НАРУШЕНИЕ',
         '500 с текстом причины')
    case((5, '401', '', True), False, 'НЕ ИЗМЕРЕНО', '401 без токена — не проверка')
    case((3, '200', '{"items": [1, 2, 3]}', False), True, 'ОК', 'конверт items')

    print('check_read_model_freshness --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='200 и непусто: сверка read-эндпоинта с БД')
    ap.add_argument('--service', required=False, default='journal')
    ap.add_argument('--endpoint', required=False,
                    default='http://xdiary-journal:8080/internal/journal/records')
    ap.add_argument('--db-name', default='journal')
    ap.add_argument('--db-table', default='records')
    ap.add_argument('--token', default='')
    ap.add_argument('--remote', default='deploy@31.192.111.168')
    ap.add_argument('--key', default=SSH_KEY)
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    hdr = '-H "Authorization: Bearer %s"' % a.token if a.token else ''
    script = REMOTE.format(
        db=a.db_name, table=a.db_table,
        curl_cmd=('podman run --rm --network xdiary %s -s -o /dev/null -w "%%{http_code}" '
                  '-m 10 %s %s' % (CURL_IMAGE, hdr, a.endpoint)),
        body_cmd=('podman run --rm --network xdiary %s -s -m 10 %s %s' %
                  (CURL_IMAGE, hdr, a.endpoint)),
    )
    try:
        out = run_remote(a.remote, a.key, script)
    except Exception as exc:
        print('::warning::check_read_model_freshness: контур недоступен (%s)' % str(exc)[:80])
        return 2

    db_count = http_code = None
    body_lines, section = [], None
    for line in out.splitlines():
        if line.strip() in ('DB', 'HTTP', 'BODY'):
            section = line.strip()
            continue
        if section == 'DB':
            try:
                db_count = int(line.strip())
            except ValueError:
                db_count = None
            section = None
        elif section == 'HTTP':
            s = line.strip()
            http_code = s if s.isdigit() else None
            section = None
        elif section == 'BODY':
            body_lines.append(line)

    body = '\n'.join(body_lines).strip()
    ok, verdict, detail = classify(db_count, http_code, body, bool(a.token) or a.service == 'partners')
    print('  сервис: %s | БД %s.%s строк: %s | HTTP: %s' %
          (a.service, a.db_name, a.db_table, db_count, http_code))
    print('  вердикт: %s — %s' % (verdict, detail))
    if not ok:
        print('::error::check_read_model_freshness: %s — %s' % (verdict, detail))
    return 0 if ok else (2 if verdict == 'НЕ ИЗМЕРЕНО' else 1)


if __name__ == '__main__':
    sys.exit(main())
