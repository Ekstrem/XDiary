#!/usr/bin/env python
"""check_service_auth.py — (ii-b): прямой вызов в сервис без токена должен давать 401.

Почему отдельный гейт, а не `--base` из `check_apim_auth.py`:
  * `--base https://…` — это **публичный вход (YARP)**: он измеряет, что прокси закрывает,
    но после переключения трафика на APIM прокси в пути НЕ будет (endpoints APIM смотрят
    прямо в `xdiary-<bc>:8080`). Поэтому «76/76 через YARP» не защищает новый путь.
  * Здесь мерим **сервис напрямую**, изнутри контура (снаружи `xdiary-*` не резолвится —
    получили бы `000` и «не проверено»).

Классификация (по @AR 2026-11-04 17:05, гейт = (ii-b)):
  * `401`/`403` — защищено ✔
  * `2xx` — **ОТКРЫТО** (нарушитель)
  * иное (`400`, `404`, `415`, `000`) — **не проверено/не защита**: до бизнес-логики доходит
    или сервис недоступен; в отчёте отдельной колонкой, чтобы не выдать за PASS.

    python tools/check_service_auth.py --selftest
    python tools/check_service_auth.py --remote deploy@31.192.111.168 --key ~/.ssh/id_ed25519_xdiary
"""
import argparse
import json
import os
import re
import subprocess
import sys
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer

INDEX = "04-architecture/api-index.yaml"
DEFAULT_SVC = {
    'Analytics': 'xdiary-analytics', 'ApiGateway': 'xdiary-api-gateway',
    'AppleHealthGateway': 'xdiary-apple-health-gateway', 'Catalog': 'xdiary-catalog',
    'Client': 'xdiary-client', 'DailyMetrics': 'xdiary-daily-metrics',
    'GarminGateway': 'xdiary-garmin-gateway', 'Journal': 'xdiary-journal',
    'Partners': 'xdiary-partners', 'Portability': 'xdiary-portability',
    'Privacy': 'xdiary-privacy', 'SomaticSeries': 'xdiary-somatic-series',
}


def load_ops(root):
    import yaml
    data = yaml.safe_load(open(os.path.join(root, INDEX), encoding='utf-8'))
    return [op for op in data['operations'] if op.get('auth') != 'anonymous']


def classify(code):
    """Классификация по решению @AR (2026-11-04 17:55, занесено в ADR-026).

    Почему `400` — это FAIL, а не «без проверки»: в ASP.NET `[Authorize]` выполняется
    ДО model binding, значит `400` означает, что запрос **дошёл до бизнес-логики без токена**.
    Открытая ручка с невалидным телом ответит `400` — это и есть открытость.

    `UNKNOWN` остаётся только за `000` (нет ответа: сервис не поднят/не резолвится) —
    иначе гейт «зеленеет на недоступном стенде», а такой гейт хуже отсутствующего.
    """
    if code in ('401', '403'):
        return 'protected'
    if code in ('000', '', None):
        return 'unknown'
    return 'open'


def build_script(ops, svc, port='8080'):
    lines = []
    for op in ops:
        path = re.sub(r'\{[^}]+\}', '00000000-0000-0000-0000-000000000000', op['path'])
        m = op['method']
        data = " -H 'Content-Type: application/json' -d '{}'" if m == 'POST' else ''
        lines.append(
            "echo \"__%s %s\"; curl -s -o /dev/null -w '%%{http_code}\\n' -m 6%s -X %s "
            "'http://%s:%s%s'" % (m, op['operationId'], data, m, svc, port, path))
    return '; '.join(lines)


def run_remote(remote, key, inner_cmd, timeout=900):
    cmd = ['ssh', '-i', os.path.expanduser(key), '-o', 'BatchMode=yes',
           '-o', 'StrictHostKeyChecking=no', remote, inner_cmd]
    r = subprocess.run(cmd, capture_output=True, timeout=timeout)
    return (r.stdout or b'').decode('utf-8', 'replace')


def parse_service_map(raw, base=DEFAULT_SVC):
    """`bc=host:port,...` → {bc: "host:port"}. Нужно, когда адресация отличается от
    дефолтной (`xdiary-<bc>:8080`): алиасы сети, другие порты, стенд с другим compose.
    Без такой возможности спор «скрипт не тот адрес взял» заменяет факт (@AR 17:20)."""
    mapping = dict(base)
    if not raw:
        return mapping
    for part in raw.split(','):
        part = part.strip()
        if not part:
            continue
        if '=' not in part:
            raise SystemExit('--service-map: ожидал bc=host:port, получил %r' % part)
        bc, addr = part.split('=', 1)
        mapping[bc.strip()] = addr.strip()
    return mapping


def measure(remote, key, only=None, service_map=None):
    """Замер изнутри контура: один `podman run` на BC, разбор кодов."""
    out_index = run_remote(remote, key, 'cat /tmp/api-index.yaml 2>/dev/null || cat '
                                        '/opt/xdiary/04-architecture/api-index.yaml 2>/dev/null')
    if not out_index.strip():
        return None, 'индекс не найден на хосте'
    import yaml
    data = yaml.safe_load(out_index)
    ops_all = [o for o in data['operations'] if o.get('auth') != 'anonymous']
    by_bc = {}
    for op in ops_all:
        if only and op['bc'] not in only:
            continue
        by_bc.setdefault(op['bc'], []).append(op)

    results = {}
    for bc, ops in sorted(by_bc.items()):
        svc = (service_map or DEFAULT_SVC).get(bc)
        if not svc:
            continue
        host, _, port = svc.partition(':')
        inner = ("podman run --rm --network xdiary docker.io/curlimages/curl:8.10.1 "
                 "sh -c %s" % json.dumps(build_script(ops, host, port or '8080')))
        out = run_remote(remote, key, inner)
        codes, cur = {}, None
        per_op = []
        for line in out.splitlines():
            line = line.strip()
            if line.startswith('__'):
                cur = line[2:]
            elif line.isdigit() and cur:
                codes[line] = codes.get(line, 0) + 1
                per_op.append((cur, line))
                cur = None
        results[bc] = {'ops': len(ops), 'codes': codes, 'per_op': per_op}
    return results, ''


def exit_code(tot):
    """Код возврата по правилу @AR/@TL: PASS только при «нарушителей 0 И неизвестно 0».

    0 — измерено и всё защищено; 1 — есть нарушители (открытые ручки); 2 — есть неизвестные
    (сервис не ответил) ⇒ «не измерено», и это НЕ pass. Такое разделение не даёт гейту
    зеленеть на недоступном стенде — ровно класс «зелёный без шага».
    """
    if tot.get('open'):
        return 1
    if tot.get('unknown'):
        return 2
    return 0


def report(results):
    print('  %-20s %9s %9s %6s %7s' % ('BC', 'операций', '401/403', 'open', 'unknown'))
    tot = {'protected': 0, 'open': 0, 'unknown': 0, 'ops': 0}
    open_list, unknown_list = [], []
    for bc, r in sorted(results.items()):
        cls = {'protected': 0, 'open': 0, 'unknown': 0}
        for name, code in r['per_op']:
            c = classify(code)
            cls[c] += 1
            if c == 'open':
                open_list.append('%s %s -> %s' % (name, bc, code))
            elif c == 'unknown':
                unknown_list.append('%s %s -> %s' % (name, bc, code))
        for k in cls:
            tot[k] += cls[k]
        tot['ops'] += r['ops']
        print('  %-20s %9d %9d %6d %7d' % (bc, r['ops'], cls['protected'], cls['open'], cls['unknown']))
    print('\n  ИТОГО: операций %d | 401/403 (защищено) %d | OPEN (нарушители) %d | UNKNOWN %d'
          % (tot['ops'], tot['protected'], tot['open'], tot['unknown']))
    print('  (классификация @AR 17:55: 400/404/415 = OPEN; UNKNOWN только за 000)')
    for d in open_list[:20]:
        print('   OPEN:', d)
    return tot, open_list


def selftest():
    """Двойной контроль классификации: 401/403 = защита, 2xx = открыто, прочее = не проверено."""
    cases = [('401', 'protected'), ('403', 'protected'), ('200', 'open'),
             ('201', 'open'), ('400', 'open'), ('404', 'open'),
             ('415', 'open'), ('000', 'unknown')]
    ok = 0
    for code, want in cases:
        got = classify(code)
        if got != want:
            print('  [FAIL] %s -> %s, ждали %s' % (code, got, want))
        else:
            ok += 1

    # живой сервер: проверяем, что код реально читается с провода, а не из константы
    class H(BaseHTTPRequestHandler):
        def do_GET(self):
            code = {'/ok': 401, '/open': 200, '/bad': 400}.get(self.path, 404)
            self.send_response(code)
            self.end_headers()

        def log_message(self, *a):
            pass

    if classify('400') == 'open' and exit_code({'open': 1, 'unknown': 0}) == 1:
        ok += 1
    else:
        print('  [FAIL] 400 должен давать нарушителя и rc=1')
    if exit_code({'open': 0, 'unknown': 5}) == 2:
        ok += 1
    else:
        print('  [FAIL] только неизвестные должны давать rc=2, а не PASS')
    if exit_code({'open': 0, 'unknown': 0}) == 0:
        ok += 1
    else:
        print('  [FAIL] чисто должно давать rc=0')

    srv = HTTPServer(('127.0.0.1', 0), H)
    t = threading.Thread(target=srv.serve_forever, daemon=True)
    t.start()
    port = srv.server_address[1]
    import urllib.request
    import urllib.error
    live_ok = 0
    for path, want in (('/ok', '401'), ('/open', '200'), ('/bad', '400')):
        try:
            with urllib.request.urlopen('http://127.0.0.1:%d%s' % (port, path), timeout=5) as r:
                got = str(r.status)
        except urllib.error.HTTPError as exc:
            got = str(exc.code)
        if got == want:
            live_ok += 1
        else:
            print('  [FAIL] live %s -> %s, ждали %s' % (path, got, want))
    srv.shutdown()
    total = len(cases) + 6
    ok += live_ok
    print('check_service_auth --selftest: %d/%d (классификатор + живой сервер)' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='(ii-b) прямой вызов в сервис без токена')
    ap.add_argument('--remote', default='deploy@31.192.111.168')
    ap.add_argument('--key', default='~/.ssh/id_ed25519_xdiary')
    ap.add_argument('--only', default=None, help='BC через запятую (по умолчанию все)')
    ap.add_argument('--service-map', default=None,
                    help='переопределить адресацию: "Catalog=xdiary-catalog:8080,Client=client:8080"')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--json', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    only = {x.strip() for x in a.only.split(',')} if a.only else None
    results, err = measure(a.remote, a.key, only,
                           parse_service_map(getattr(a, 'service_map', None)))
    if results is None:
        print('::error::(ii-b) %s' % err)
        return 2
    tot, open_list = report(results)
    if a.json:
        print(json.dumps({'totals': tot, 'open': open_list}, ensure_ascii=False))
    return exit_code(tot)


if __name__ == '__main__':
    sys.exit(main())
