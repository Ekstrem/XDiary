#!/usr/bin/env python
"""check_apim_health.py — health-гейт Gravitee APIM (DO-076).

Зачем (вопрос владельца 2026-11-05): APIM стоит **в пути трафика**
(`Funnel → Caddy:8081 → APIM:18082 → сервисы`), но **не входит ни в `deploy.sh`, ни в
`update.sh`** (0 упоминаний) и не проверялся ни одним CD-шагом. Опасность конкретная:
`/health` идёт **мимо** APIM (в APIM нет маршрута → 404), поэтому существующий smoke
`curl /health` останется зелёным, **даже если APIM мёртв** — а публичный API при этом
вернёт 404/502 на всё. Это ровно класс «гейт зелёный, потому что проверяет не тот объект».

Проверки:
  1. все 4 контейнера APIM — `running`;
  2. gateway отвечает `401` на защищённом пути (значит маршрут жив, а не «всё 404»);
  3. management `/management/v2/` → `200`;
  4. mongo `ping` → `1`;
  5. **публичный путь** через Caddy (`/api/v1/catalog`) → `401` **и** есть `x-gravitee-*`;
  6. `/health` → `200` (он идёт мимо APIM — проверяем, что исключение не сломано).

    python tools/check_apim_health.py --remote deploy@31.192.111.168 --key ~/.ssh/id_ed25519_xdiary
    python tools/check_apim_health.py --selftest

rc: 0 — всё зелено; 1 — есть падение; 2 — не удалось измерить (нет ssh/контура).
"""
import argparse
import os
import subprocess
import sys

LOGIN = 'xdiary-apim-gateway xdiary-apim-management xdiary-apim-ui xdiary-apim-mongo'
PUBLIC_HOST = 'xdiary-vps1.taile0b6ee.ts.net'


def ssh(remote, key, script, timeout=300):
    cmd = ['ssh', '-i', os.path.expanduser(key), '-o', 'BatchMode=yes',
           '-o', 'StrictHostKeyChecking=no', remote, 'bash -s']
    r = subprocess.run(cmd, input=script.replace('\r\n', '\n').encode('utf-8'),
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
    return (r.stdout or b'').decode('utf-8', 'replace')


REMOTE_SCRIPT = r'''set -u
echo "containers"
for c in LOGIN; do
  printf "%s %s\n" "$c" "$(podman inspect "$c" --format '{{.State.Status}}' 2>/dev/null || echo missing)"
done
echo "gateway"
curl -s -o /dev/null -w "%{http_code}\n" -m 8 http://127.0.0.1:18082/api/v1/catalog
echo "management"
podman exec xdiary-apim-management sh -c 'curl -s -o /dev/null -w "%{http_code}" -m 8 http://127.0.0.1:8083/management/v2/' 2>/dev/null || echo 000
echo ""
echo "mongo"
podman exec xdiary-apim-mongo mongosh --quiet --eval 'db.runCommand({ping:1}).ok' 2>/dev/null | tail -1
echo "public"
curl -sS -D /tmp/hdr.txt -o /dev/null -w "%{http_code}\n" -m 8 -H "Host: PUBLIC_HOST" http://127.0.0.1:8081/api/v1/catalog
echo "gravitee"
grep -ci 'x-gravitee' /tmp/hdr.txt 2>/dev/null || echo 0
echo "health"
curl -s -o /dev/null -w "%{http_code}\n" -m 8 -H "Host: PUBLIC_HOST" http://127.0.0.1:8081/health
'''


def classify(results):
    """results: dict имя→значение. Возвращает (ok, fails[], unknowns[])."""
    fails, unknown = [], []
    for name, value in results.items():
        if name.startswith('container:'):
            c = name.split(':', 1)[1]
            if value != 'running':
                fails.append('%s: %s (ожидали running)' % (c, value))
        elif name == 'gateway':
            if value in ('000', '', None):
                unknown.append('gateway не ответил')
            elif value != '401':
                fails.append('gateway /api/v1/catalog: %s (ожидали 401 — маршрут жив)' % value)
        elif name == 'management':
            if value in ('000', '', None):
                unknown.append('management не ответил')
            elif value != '200':
                fails.append('management /management/v2/: %s (ожидали 200)' % value)
        elif name == 'mongo':
            if value not in ('1', '1.0'):
                fails.append('mongo ping: %r (ожидали 1)' % value)
        elif name == 'public':
            if value in ('000', '', None):
                unknown.append('публичный путь не ответил')
            elif value != '401':
                fails.append('публичный /api/v1/catalog: %s (ожидали 401; 404 = APIM без маршрута, '
                             '502 = APIM мёртв)' % value)
        elif name == 'gravitee':
            try:
                n = int(value)
            except (TypeError, ValueError):
                n = 0
            if n < 1:
                fails.append('публичный путь без x-gravitee — трафик идёт мимо APIM')
        elif name == 'health':
            if value != '200':
                fails.append('/health: %s (ожидали 200 — он идёт мимо APIM)' % value)
    return (not fails and not unknown), fails, unknown


def selftest():
    ok = total = 0

    def case(name, want_ok, label):
        nonlocal ok, total
        total += 1
        good, _f, _u = classify(name)
        if good == want_ok:
            ok += 1
        else:
            print('  [FAIL] %s: ok=%s, ждали %s' % (label, good, want_ok))

    base = {'container:xdiary-apim-gateway': 'running',
            'container:xdiary-apim-management': 'running',
            'container:xdiary-apim-ui': 'running',
            'container:xdiary-apim-mongo': 'running',
            'gateway': '401', 'management': '200', 'mongo': '1',
            'public': '401', 'gravitee': '3', 'health': '200'}
    case(dict(base), True, 'всё здорово')
    case(dict(base, **{'container:xdiary-apim-gateway': 'exited'}), False, 'gateway контейнер упал')
    case(dict(base, **{'public': '404'}), False, 'публичный 404 (APIM без маршрута)')
    case(dict(base, **{'public': '502'}), False, 'публичный 502 (APIM мёртв)')
    case(dict(base, **{'gravitee': '0'}), False, 'публичный без x-gravitee (мимо APIM)')
    case(dict(base, **{'health': '404'}), False, '/health не 200')
    case(dict(base, **{'management': '000'}), False, 'management недоступен')
    case(dict(base, **{'mongo': '0'}), False, 'mongo ping 0')

    print('check_apim_health --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Health-гейт Gravitee APIM')
    ap.add_argument('--remote', default='deploy@31.192.111.168')
    ap.add_argument('--key', default='~/.ssh/id_ed25519_xdiary')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    script = REMOTE_SCRIPT.replace('LOGIN', LOGIN).replace('PUBLIC_HOST', PUBLIC_HOST)
    try:
        out = ssh(a.remote, a.key, script)
    except Exception as exc:
        print('::error::check_apim_health: контур недоступен (%s) — не измерено' % str(exc)[:80])
        return 2
    if not out.strip():
        print('::error::check_apim_health: пустой ответ контура — не измерено')
        return 2

    results, section, buf = {}, None, []
    for line in out.splitlines():
        line = line.rstrip()
        if line in ('containers', 'gateway', 'management', 'mongo', 'public', 'gravitee', 'health'):
            if section == 'containers':
                for row in buf:
                    parts = row.split()
                    if len(parts) == 2:
                        results['container:' + parts[0]] = parts[1]
            elif section:
                results[section] = (buf[0].strip() if buf else '')
            section, buf = line, []
        elif section:
            buf.append(line)
    if section == 'containers':
        for row in buf:
            parts = row.split()
            if len(parts) == 2:
                results['container:' + parts[0]] = parts[1]
    elif section:
        results[section] = (buf[0].strip() if buf else '')

    good, fails, unknown = classify(results)
    for name, value in sorted(results.items()):
        print('  %-38s %s' % (name, value))
    if fails:
        for f in fails:
            print('::error::check_apim_health: %s' % f)
    if unknown:
        for u in unknown:
            print('::warning::check_apim_health: не измерено — %s' % u)
    print('APIM: %s' % ('OK' if good else ('НЕ ИЗМЕРЕНО' if unknown and not fails else 'FAIL')))
    return 0 if good else (2 if unknown and not fails else 1)


if __name__ == '__main__':
    sys.exit(main())
