#!/usr/bin/env python
"""rollout_service.py — выкатка сервиса на прод одной командой (DO-076).

Зачем: после каждого зелёного `ci-<service>` шаги одинаковы и ручные:
  dispatch сборки → дождаться success → `podman pull` → сравнить ID образа
  → пересоздать контейнер → проверить `/health` и профильный маршрут.
Руками это делается с ошибками (2026-11-04: `recreate-service.sh` снёс контейнер
и не поднял — сервис лежал ~6 минут). Здесь те же шаги, но с проверками.

Примеры:
    python tools/rollout_service.py --service portability --plan
    python tools/rollout_service.py --service portability
    python tools/rollout_service.py --service journal --no-build   # только выкатка

Требования: `GHT.md` с токеном GitHub (вне репозитория), ssh-ключ `id_ed25519_xdiary`.
"""
import argparse
import json
import os
import subprocess
import sys
import time
import urllib.request

REPO = 'Ekstrem/XDiary'
API = 'https://api.github.com/repos/' + REPO
SSH_HOST = 'deploy@31.192.111.168'
SSH_KEY = os.path.expanduser('~/.ssh/id_ed25519_xdiary')
WORKFLOW = 'ci-%s.yml'


def token():
    for path in (r'C:\Users\About\Documents\Obsidian\DSH\GHT.md',
                 os.path.expanduser('~/GHT.md')):
        if os.path.exists(path):
            for line in open(path, encoding='utf-8'):
                if line.strip().startswith('ghp_'):
                    return line.strip()
    raise SystemExit('не найден токен GitHub (GHT.md вне репозитория)')


def gh(path, data=None, method='GET'):
    req = urllib.request.Request(
        API + path, data=json.dumps(data).encode() if data else None,
        headers={'Authorization': 'token ' + token(),
                 'Accept': 'application/vnd.github+json',
                 'User-Agent': 'dsh-do',
                 'Content-Type': 'application/json'},
        method=method)
    with urllib.request.urlopen(req, timeout=40) as resp:
        body = resp.read().decode()
        return json.loads(body) if body.strip() else {}


def ssh(script, timeout=600):
    """Выполнить bash-скрипт на проде.

    Важно: передаём **байты**, а не str. С `text=True` Python на Windows сам
    переводит `\n` в `\r\n`, и bash на той стороне падает на `do\r` /
    `set -u\r` — проверено (2026-11-04, две попытки).
    """
    cmd = ['ssh', '-i', SSH_KEY, '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=no',
           SSH_HOST, 'bash -s']
    payload = script.replace('\r\n', '\n').encode('utf-8')
    r = subprocess.run(cmd, input=payload, stdout=subprocess.PIPE,
                       stderr=subprocess.STDOUT, timeout=timeout)
    out = (r.stdout or b'').decode('utf-8', 'replace')
    return r.returncode, out


def build(service, wait=True):
    wf = WORKFLOW % service
    gh('/actions/workflows/%s/dispatches' % wf, {'ref': 'main'}, 'POST')
    print('dispatch %s: отправлен' % wf)
    run_id = None
    for _ in range(12):
        runs = gh('/actions/workflows/%s/runs?per_page=3' % wf).get('workflow_runs', [])
        cand = [r for r in runs if r.get('event') == 'workflow_dispatch']
        if cand:
            run_id = cand[0]['id']
            break
        time.sleep(10)
    if not run_id:
        return False, 'прогон не появился'
    print('run %s' % run_id)
    if not wait:
        return True, 'run %s запущен (без ожидания)' % run_id
    for _ in range(40):
        run = gh('/actions/runs/%d' % run_id)
        if run.get('status') == 'completed':
            concl = run.get('conclusion')
            if concl == 'success':
                return True, 'сборка success (run %s)' % run_id
            jobs = gh('/actions/runs/%d/jobs' % run_id).get('jobs', [])
            bad = []
            for j in jobs:
                for st in j.get('steps', []):
                    if st.get('conclusion') not in ('success', 'skipped', None):
                        bad.append('%s: %s' % (j['name'], st['name']))
            return False, 'сборка %s; упали шаги: %s' % (concl, '; '.join(bad[:3]))
        time.sleep(20)
    return False, 'сборка не завершилась за отведённое время'


def rollout(service, health_path='/health'):
    img = 'ghcr.io/ekstrem/xdiary-%s:single' % service
    script = '''set -u
cd /opt/xdiary/7-deploy || exit 2
name=xdiary-%(svc)s
before=$(podman inspect $name --format '{{.Image}}' 2>/dev/null | cut -c1-12)
echo "before=$before"
podman pull -q %(img)s >/dev/null 2>&1
after=$(podman images --no-trunc --format '{{.Id}}' %(img)s | head -1 | cut -c1-12)
echo "after_pull=$after"
if [ "$before" = "$after" ]; then
  echo "verdict=same-image"
  podman start $name >/dev/null 2>&1 || true
else
  NET=xdiary; LBL="--label io.containers.autoupdate=registry"; TAG=single
  podman rm -f $name >/dev/null 2>&1
  LINE=$(grep -m1 "podman run -d --name $name" deploy.sh)
  [ -n "$LINE" ] || { echo "verdict=no-run-line"; exit 1; }
  eval "$LINE" >/dev/null 2>&1
  echo "verdict=recreated"
fi
for i in $(seq 1 20); do
  sleep 3
  st=$(podman inspect $name --format '{{.State.Status}}' 2>/dev/null || echo none)
  [ "$st" = "running" ] && break
done
echo "status=$(podman inspect $name --format '{{.State.Status}}' 2>/dev/null)"
echo "restarts=$(podman inspect $name --format '{{.RestartCount}}' 2>/dev/null)"
echo "health_yarp=$(curl -s -o /dev/null -w '%%{http_code}' -m 8 http://127.0.0.1:8080%(hp)s)"
''' % {'svc': service, 'img': img, 'hp': health_path}
    rc, out = ssh(script, timeout=900)
    return rc, out


def main():
    ap = argparse.ArgumentParser(description='Выкатить сервис на прод (DO-076)')
    ap.add_argument('--service', required=True,
                    help='имя сервиса без префикса xdiary-, например portability')
    ap.add_argument('--no-build', action='store_true', help='не запускать сборку, только выкатка')
    ap.add_argument('--plan', action='store_true', help='только показать шаги')
    ap.add_argument('--health', default='/health', help='профильный маршрут для проверки')
    a = ap.parse_args()

    steps = ['dispatch %s' % (WORKFLOW % a.service),
             'дождаться success', 'ssh: podman pull + сравнить ID образа',
             'ssh: пересоздать контейнер (строка из deploy.sh)',
             'ssh: дождаться running, снять RestartCount и код %s' % a.health]
    if a.plan:
        print('план для %s:' % a.service)
        for i, s in enumerate(steps, 1):
            print('  %d. %s' % (i, s))
        return 0

    if not a.no_build:
        ok, msg = build(a.service)
        print('сборка:', msg)
        if not ok:
            print('ВЫКАТКА ОСТАНОВЛЕНА: образ не опубликован')
            return 1

    rc, out = rollout(a.service, a.health)
    print(out.strip()[:1200])
    return 0 if rc == 0 and 'status=running' in out else 1


if __name__ == '__main__':
    sys.exit(main())
