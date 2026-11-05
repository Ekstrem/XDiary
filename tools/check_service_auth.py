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
    """Классы по канону `ADR-026` §«Квитанция (ii-b)» (решение @AR/@TL 2026-11-04/05).

    * `401`/`403` — **protected** (единственный признак защиты);
    * `404`/`405` на операции, которая **есть в индексе**, — **contract**: индекс говорит
      «операция есть», сервис — «маршрута нет»; это дефект реестра (класс DO-050),
      а не «неизвестно» и не защита;
    * нет ответа (`000`, таймаут, обрыв) — **unknown**; только этот класс «не измерен»;
    * любой другой полученный ответ (`2xx`, `400`, `415`, `429`, `5xx`) — **violator**:
      ответ получен ⇒ запрос дошёл до сервиса и был обработан без токена.

    Правило намеренно не интерпретирует смысл кода: так число воспроизводимо, и гейт
    не может «позеленеть», сложив открытые ручки в неизвестность.
    """
    if code in ('401', '403'):
        return 'protected'
    if code in ('404', '405'):
        return 'contract'
    if code in ('000', '', None):
        return 'unknown'
    return 'violator'


def build_script(ops, svc, port='8080'):
    """Скрипт для контейнера-измерителя: на каждую операцию печатает маркер и код ответа."""
    lines = []
    for op in ops:
        path = re.sub(r'\{[^}]+\}', '00000000-0000-0000-0000-000000000000', op['path'])
        m = op['method']
        data = " -H 'Content-Type: application/json' -d '{}'" if m == 'POST' else ''
        lines.append(
            ("echo \"__%s %s\"; curl -s -o /dev/null -w '%%{http_code}\\n' -m 6%s -X %s "
             "'http://%s:%s%s'") % (m, op['operationId'], data, m, svc, port, path))
    return '; '.join(lines)


def run_remote(remote, key, inner_cmd, timeout=900):
    """Выполнить команду на контуре по ssh; возвращает stdout как текст.

    ВАЖНО: без `text=True` — на Windows он вставляет CRLF, и bash падает на `do\r`.
    """
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



def collect_images(remote, key, services):
    """Образы, которые РЕАЛЬНО запущены (не теги): `images=svc:<image[:12]>`.

    Зачем (@AR 2026-11-04 18:10): за сутки трижды спорили о числах, потому что «код есть,
    образ старый». Квитанция без выкаченного образа не прикладывается к релизу.
    """
    names = ' '.join(sorted(set(services)))
    inner = " ".join([
        'for s in %s; do' % names,
        'img=$(podman inspect "$s" --format "{{.Image}}" 2>/dev/null | cut -c1-12);',
        'echo "$s ${img:-нет-контейнера}";',
        'done',
    ])
    cmd = inner
    out = run_remote(remote, key, cmd)
    images = {}
    for line in out.splitlines():
        parts = line.split()
        if len(parts) == 2:
            images[parts[0].replace('xdiary-', '')] = parts[1]
    return images


def collect_ref(remote, key):
    """Короткий SHA клона на контуре — чтобы квитанция ссылалась на реально измеренное."""
    out = run_remote(remote, key, 'git -C /home/deploy/XDiary rev-parse --short HEAD 2>/dev/null').strip()
    return out or 'НЕ_ОПРЕДЕЛЁН'


def measure(remote, key, only=None, service_map=None):
    """Замер изнутри контура: один `podman run` на BC, разбор кодов ответов."""
    out_index = run_remote(remote, key, 'cat /tmp/api-index.yaml 2>/dev/null || cat '
                                        '/opt/xdiary/04-architecture/api-index.yaml 2>/dev/null')
    if not out_index.strip():
        return None, 'индекс не найден на хосте (нужен api-index.yaml в /tmp или /opt/xdiary)'
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
        # ключ в per_op — «МЕТОД ИмяОперации», сверяем по последнему слову; каждая операция
        # закрывается явным '000', иначе «нет ответа» выпадало бы из счёта (@TL 01:35)
        measured = {}
        for cur_name, code in per_op:
            measured[cur_name.split()[-1]] = code
        per_op = [(op['operationId'], measured.get(op['operationId'], '000')) for op in ops]
        results[bc] = {'ops': len(ops), 'codes': codes, 'per_op': per_op}
    return results, ''


def exit_code(tot):
    """0 — измерено и всё защищено; 1 — есть нарушители или контракты; 2 — есть неизвестные."""
    if tot.get('violator') or tot.get('contract'):
        return 1
    if tot.get('unknown'):
        return 2
    return 0


def report(results):
    print('  инструмент: tools/check_service_auth.py — ПЕРЕКРЁСТНЫЙ КОНТРОЛЬ; '
          'канон замера (ii-b) — 9-tests/scripts/check_apim_auth.py (ADR-026 §«Квитанция (ii-b)»)')
    print('  %-20s %9s %10s %9s %8s %9s'
          % ('BC', 'операций', 'protected', 'violator', 'contract', 'unknown'))
    tot = {'protected': 0, 'violator': 0, 'contract': 0, 'unknown': 0, 'ops': 0}
    bad_list = []
    for bc, r in sorted(results.items()):
        cls = {'protected': 0, 'violator': 0, 'contract': 0, 'unknown': 0}
        for name, code in r['per_op']:
            c = classify(code)
            cls[c] += 1
            if c in ('violator', 'contract'):
                bad_list.append('%s %s -> %s (%s)' % (name, bc, code, c))
        for k in cls:
            tot[k] += cls[k]
        tot['ops'] += r['ops']
        print('  %-20s %9d %10d %9d %8d %9d'
              % (bc, r['ops'], cls['protected'], cls['violator'], cls['contract'], cls['unknown']))
    print('\n  ИТОГО: операций %d | защищено %d | нарушителей %d | contract %d | неизвестно %d'
          % (tot['ops'], tot['protected'], tot['violator'], tot['contract'], tot['unknown']))
    for d in bad_list[:20]:
        print('   НАРУШЕНИЕ:', d)
    return tot, bad_list


CANON = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       '9-tests', 'scripts', 'check_apim_auth.py')


def canon_broken(path=None):
    """Дефект канона (норма `canon-broken`, @AR 2026-11-05 02:43).

    Канон — `9-tests/scripts/check_apim_auth.py`. Известный дефект: ветка `401/403`
    делает `continue` без `protected_count += 1`, поэтому канон печатает
    `защищено=0` при любом числе `401`. Пока это так, `PASS` таким замером
    **не закрывается**: работать можно, закрывать нельзя (как `UNKNOWN`).

    Возвращает строку-причину, если канон сломан, иначе ''.
    """
    p = path or CANON
    try:
        text = open(p, encoding='utf-8', errors='replace').read()
    except OSError:
        return ''  # канона рядом нет — не наш случай, маркер не ставим
    if 'protected_count += 1' in text:
        return ''
    return 'защищено не считается: check_apim_auth.py:285 continue без инкремента'


def tool_id(path=None):
    """`путь@sha256[:12]` самого запускавшегося скрипта.

    `REF` доказывает версию **репозитория**, но не версию **скрипта**: его могли править
    в рабочем дереве (сегодня классификация менялась трижды). Поэтому в квитанции — `tool=`.
    """
    import hashlib
    p = path or os.path.abspath(__file__)
    try:
        data = open(p, 'rb').read()
    except OSError:
        return 'tool=НЕ_ОПРЕДЕЛЁН'
    return '%s@%s' % (os.path.basename(p), hashlib.sha256(data).hexdigest()[:12])


def identity_ok(tot):
    """`защищено + нарушителей + contract + неизвестно == операций`.

    Это защита от «выпавшей группы» (@AR): если какая-то группа операций выпала из счёта
    (именно так 13 операций с `400` когда-то ушли в `unchecked`), сумма перестанет сходиться —
    и квитанция недействительна, а не «почти верна».
    """
    return (tot['protected'] + tot['violator'] + tot['contract'] + tot['unknown']) == tot['ops']


def receipt(ref, where, tot, rc, images):
    """Строка квитанции в формате `ADR-026` §«Квитанция (ii-b)» (@TL 2026-11-05 01:56).

    `REF=<sha> | (ii-b) PASS|FAIL|UNKNOWN | защищено=N/76 | нарушителей=V | contract=C |
     неизвестно=U | rc=… | where=… | images=…`

    `PASS` — только при `V=0 И C=0 И U=0`; `UNKNOWN` — когда `V=C=0`, но `U>0`
    (измерено не всё, и это не «чисто»); иначе `FAIL`.
    """
    valid = identity_ok(tot)
    broken = canon_broken()
    if not valid:
        verdict = 'INVALID'
    elif broken and rc == 0:
        # норма `canon-broken`: числа перекрёстного инструмента рабочие, но PASS
        # таким замером не закрывается — как UNKNOWN, только по другой причине
        verdict = 'CANON-BROKEN'
        rc = 2
    elif rc == 0:
        verdict = 'PASS'
    elif tot['violator'] == 0 and tot['contract'] == 0:
        verdict = 'UNKNOWN'
    else:
        verdict = 'FAIL'
    img = ','.join('%s:%s' % (k, v) for k, v in sorted(images.items())) or 'НЕ_ИЗМЕРЕНО'
    return ('REF=%s | (ii-b) %s | защищено=%d/%d | нарушителей=%d | contract=%d | неизвестно=%d | '
            'сумма=%d/%d | rc=%d | where=%s | images=%s | tool=%s%s'
            % (ref, verdict, tot['protected'], tot['ops'], tot['violator'], tot['contract'],
               tot['unknown'],
               tot['protected'] + tot['violator'] + tot['contract'] + tot['unknown'], tot['ops'],
               rc, where, img, tool_id(),
               (' | canon-broken: ' + broken) if broken else ' | canon=ok'))


def selftest():
    """Проверка гейта без сети: классы, код возврата, квитанция и живой HTTP-сервер."""
    cases = [('401', 'protected'), ('403', 'protected'),
             ('200', 'violator'), ('201', 'violator'), ('400', 'violator'),
             ('415', 'violator'), ('429', 'violator'), ('500', 'violator'),
             ('404', 'contract'), ('405', 'contract'), ('000', 'unknown')]
    ok = 0
    for code, want in cases:
        got = classify(code)
        if got == want:
            ok += 1
        else:
            print('  [FAIL] %s -> %s, ждали %s' % (code, got, want))

    rc_cases = [
        ({'violator': 0, 'contract': 0, 'unknown': 0}, 0),
        ({'violator': 1, 'contract': 0, 'unknown': 0}, 1),
        ({'violator': 0, 'contract': 1, 'unknown': 0}, 1),   # контракт ломает гейт
        ({'violator': 0, 'contract': 0, 'unknown': 5}, 2),   # «не измерено» — не PASS
    ]
    for tot, want in rc_cases:
        got = exit_code(tot)
        if got == want:
            ok += 1
        else:
            print('  [FAIL] exit_code(%s) -> %s, ждали %s' % (tot, got, want))

    # квитанция: формат @TL 01:56 и правильный вердикт
    base = {'protected': 76, 'violator': 0, 'contract': 0, 'unknown': 0, 'ops': 76}
    line = receipt('abc1234', 'unit', base, 0, {'catalog': 'bff93d9e1234'})
    if ('(ii-b) PASS' in line and 'contract=0' in line and 'images=catalog:bff93d9e123' in line
            and 'where=unit' in line):
        ok += 1
    else:
        print('  [FAIL] PASS-квитанция: %s' % line)

    unk = dict(base, protected=48, unknown=28)
    line_unk = receipt('abc1234', 'unit', unk, 2, {})
    if '(ii-b) UNKNOWN' in line_unk and 'images=НЕ_ИЗМЕРЕНО' in line_unk:
        ok += 1
    else:
        print('  [FAIL] UNKNOWN-квитанция: %s' % line_unk)

    bad = {'protected': 21, 'violator': 22, 'contract': 1, 'unknown': 32, 'ops': 76}
    if '(ii-b) FAIL' in receipt('abc', 'unit', bad, 1, {}):
        ok += 1
    else:
        print('  [FAIL] FAIL-квитанция не помечена')

    # «ответа нет» не должно выпадать из счёта: иначе неизвестные занижаются
    tot_fake, _ = report({'Fake': {'ops': 2, 'per_op': [('A.Get', '401'), ('B.Get', '000')]}})
    if (tot_fake['unknown'] == 1 and tot_fake['protected'] == 1 and tot_fake['violator'] == 0
            and tot_fake['ops'] == 2):
        ok += 1
    else:
        print('  [FAIL] операция без ответа должна попадать в unknown: %s' % (tot_fake,))

    # норма canon-broken: детектор видит отсутствие инкремента и присутствие
    import tempfile as _tf
    with _tf.NamedTemporaryFile('w', suffix='.py', delete=False, encoding='utf-8') as fh:
        fh.write('def verdict():\n    if code in (401, 403):\n        continue\n')
        broken_path = fh.name
    with _tf.NamedTemporaryFile('w', suffix='.py', delete=False, encoding='utf-8') as fh:
        fh.write('def verdict():\n    protected_count += 1\n')
        ok_path = fh.name
    if canon_broken(broken_path) and not canon_broken(ok_path):
        ok += 1
    else:
        print('  [FAIL] canon_broken не различает сломанный и исправленный канон')
    os.unlink(broken_path)
    os.unlink(ok_path)
    if 'canon=ok' in receipt('abc', 'unit', base, 0, {}) or 'canon-broken' in receipt('abc', 'unit', base, 0, {}):
        ok += 1
    else:
        print('  [FAIL] в квитанции нет статуса канона')

    # tool= — обязательное поле (формат @AR §7): путь + 12 hex
    import re as _re
    if _re.search(r'tool=[A-Za-z0-9_.-]+@[0-9a-f]{12}', receipt('abc', 'unit', base, 0, {})):
        ok += 1
    else:
        print('  [FAIL] в квитанции нет tool= в формате путь@sha256[:12]')

    # тождество: выпавшая группа делает квитанцию недействительной
    broken = {'protected': 21, 'violator': 22, 'contract': 0, 'unknown': 32, 'ops': 76}
    if not identity_ok(broken) and '(ii-b) INVALID' in receipt('abc', 'unit', broken, 1, {}):
        ok += 1
    else:
        print('  [FAIL] выпавшая группа должна давать INVALID, а не PASS/FAIL')
    if identity_ok({'protected': 40, 'violator': 8, 'contract': 0, 'unknown': 28, 'ops': 76}):
        ok += 1
    else:
        print('  [FAIL] 40+8+0+28=76 — тождество должно сходиться')

    # живой сервер: код должен реально читаться с провода, а не из константы
    class H(BaseHTTPRequestHandler):
        def do_GET(self):
            code = {'/ok': 401, '/open': 200, '/bad': 400}.get(self.path, 404)
            self.send_response(code)
            self.end_headers()

        def log_message(self, *a):
            pass

    srv = HTTPServer(('127.0.0.1', 0), H)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    port = srv.server_address[1]
    import urllib.error
    import urllib.request
    for path, want in (('/ok', '401'), ('/open', '200'), ('/bad', '400')):
        try:
            with urllib.request.urlopen('http://127.0.0.1:%d%s' % (port, path), timeout=5) as r:
                got = str(r.status)
        except urllib.error.HTTPError as exc:
            got = str(exc.code)
        if got == want:
            ok += 1
        else:
            print('  [FAIL] live %s -> %s, ждали %s' % (path, got, want))
    srv.shutdown()

    total = len(cases) + len(rc_cases) + 12
    print('check_service_auth --selftest: %d/%d (классы, rc, квитанция, живой сервер)' % (ok, total))
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
    ap.add_argument('--direct', action='store_true',
                    help='совместимость с гейтом @TE: у нас это режим по умолчанию (прямо в сервис)')
    ap.add_argument('--images', default='auto', choices=['auto', 'none'],
                    help='auto — печатать images=<svc:digest[:12]> в квитанции; none — не печатать')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--where', default='podman run --rm --network xdiary (изнутри контура)',
                    help='чем измерено: обязательно попадает в квитанцию')
    ap.add_argument('--ref', default=None, help='переопределить REF (по умолчанию — клон на контуре)')
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
    rc = exit_code(tot)
    services = list((parse_service_map(getattr(a, 'service_map', None))).get(bc, '')
                    for bc in results)
    services = ['xdiary-' + x.split(':')[0].replace('xdiary-', '') for x in services if x]
    if not identity_ok(tot):
        print('::error::(ii-b) тождество N+V+C+U != 76 — квитанция недействительна (выпавшая группа операций)')
        rc = 2
    images = collect_images(a.remote, a.key, services) if a.images != 'none' else {}
    ref = a.ref or collect_ref(a.remote, a.key)
    print()
    print('  ' + receipt(ref, a.where, tot, rc, images))
    if a.json:
        print(json.dumps({'totals': tot, 'open': open_list}, ensure_ascii=False))
    return rc


if __name__ == '__main__':
    sys.exit(main())
