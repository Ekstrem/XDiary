#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`release_build.py` — сборка всех сервисов с ОДНОГО коммита и lock с одним `commit=` (DO-125).

Зачем: `7-deploy/images.lock` собирается как «последние ДОСТУПНЫЕ образы», поэтому набор смешанный
(элементы отстают от кода). Сторож `DO-050` в `update.sh` сравнивает дату сборки образов с последним
коммитом `6-msa` и **справедливо** отказывается выкатывать прод: «образ(ы) старше кода».

Этот инструмент делает набор консистентным: тег `single-<sha HEAD>` для каждого сервиса, и все строки
lock получают **один** `commit=<sha>`. Логика вынесена из workflow, чтобы её можно было прогнать и
проверить локально (а не отлаживать heredoc внутри YAML).

Коды: 0 — ок; 1 — нарушение/сбой сборки; 2 — не измерено (нет lock/файлов).
"""
import argparse
import json
import os
import re
import subprocess
import sys

LOCK = '7-deploy/images.lock'
SERVICES_WITHOUT_IMAGE = {'front'}   # статика PWA (Pages) — Dockerfile нет, образ не собираем


def read_lock(path=LOCK):
    """[(service, path)] из lock; сервисы с `#` пропускаются."""
    rows = []
    with open(path, encoding='utf-8') as fh:
        for line in fh:
            s = line.strip()
            if not s or s.startswith('#'):
                continue
            parts = s.split()
            if len(parts) < 3:
                continue
            rows.append((parts[0], parts[2]))
    return rows


def sha_of(rev='HEAD', cwd=None):
    cp = subprocess.run(['git', 'rev-parse', '--short=7', rev], cwd=cwd, capture_output=True,
                        text=True, encoding='utf-8', errors='replace')
    return cp.stdout.strip() if cp.returncode == 0 else ''


def build(rows, sha, registry='ghcr.io', owner='ekstrem', dry_run=False, only=None):
    """Собрать (и запушить) образы; вернуть список собранных сервисов."""
    built = []
    for svc, path in rows:
        if only and svc not in only:
            continue
        df = os.path.join(path, 'Dockerfile')
        if svc in SERVICES_WITHOUT_IMAGE and not os.path.exists(df):
            print('::notice::%s — статика PAGES, образ не собирается' % svc)
            continue
        if not os.path.exists(df):
            print('::error::%s: нет %s' % (svc, df))
            return None
        tag = '%s/%s/xdiary-%s:single-%s' % (registry, owner, svc, sha)
        print('::group::build %s -> %s' % (svc, tag))
        r = subprocess.run(['docker', 'build', '-f', df, '-t', tag, path])
        if r.returncode != 0:
            print('::error::%s: сборка упала' % svc)
            return None
        if not dry_run:
            p = subprocess.run(['docker', 'push', tag])
            if p.returncode != 0:
                print('::error::%s: пуш упал' % svc)
                return None
        print('::endgroup::')
        built.append(svc)
    return built


def write_lock(rows, sha, path=LOCK, registry='ghcr.io', owner='ekstrem'):
    """Переписать lock: один `commit=<sha>` во всех строках (digest заполняется отдельно)."""
    out = []
    with open(path, encoding='utf-8') as fh:
        for line in fh:
            s = line.strip()
            if not s:
                out.append('')
                continue
            if s.startswith('#'):
                out.append(line.rstrip('\n'))
                continue
            parts = s.split()
            svc, pathx = parts[0], parts[2]
            out.append('%-22s single-%s  %s  commit=%s' % (svc, sha, pathx, sha))
    with open(path, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('\n'.join(out) + '\n')
    return path


def unique_commits(path=LOCK):
    with open(path, encoding='utf-8') as fh:
        text = fh.read()
    return sorted(set(re.findall(r'commit=([0-9a-f]+)', text)))


def main(argv=None):
    ap = argparse.ArgumentParser(description='Сборка всех сервисов с одного коммита (DO-125)')
    ap.add_argument('--list', action='store_true', help='показать сервисы из lock')
    ap.add_argument('--sha', default=None, help='коммит релиза (по умолчанию HEAD)')
    ap.add_argument('--build', action='store_true', help='собрать и запушить образы')
    ap.add_argument('--write-lock', action='store_true', help='переписать lock одним commit=')
    ap.add_argument('--check', action='store_true', help='проверить, что commit= ровно один')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--only', default=None, help='сервисы через запятую')
    ap.add_argument('--file', default=LOCK)
    a = ap.parse_args(argv)

    rows = read_lock(a.file)
    if not rows:
        print('::error::release_build: в %s нет сервисов' % a.file)
        return 2
    if a.list:
        print('сервисов в lock: %d' % len(rows))
        for svc, path in rows:
            print('  %-22s %s' % (svc, path))
        return 0

    sha = a.sha or sha_of()
    if not sha:
        print('::error::release_build: не могу определить sha')
        return 2

    only = {x.strip() for x in (a.only or '').split(',') if x.strip()} or None
    if a.build:
        built = build(rows, sha, dry_run=a.dry_run, only=only)
        if built is None:
            return 1
        print('собрано сервисов: %d' % len(built))

    if a.write_lock:
        write_lock(rows, sha, a.file)
        print('lock переписан: commit=%s для всех' % sha)

    if a.check:
        uniq = unique_commits(a.file)
        print('уникальных commit=: %d %s' % (len(uniq), uniq))
        if len(uniq) != 1:
            print('::error::DO-125: в lock НЕ один commit= (%d)' % len(uniq))
            return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
