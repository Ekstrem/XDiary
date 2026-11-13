#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_direct_push.py` — прямой пуш в `main` ловится ТОПОЛОГИЕЙ, а не по имени коммита.

Зачем: кнут @LM 2026-11-13 08:33 обвинил @DO и @TL в «прямом пуше в main (do: DO-123,
tl: TL-002)», хотя оба коммита приехали через PR: в `main` они — **вторые родители**
merge-коммитов. Детектор, который ищет по `git log --grep`/`git rev-list`, находит их и
делает ложный вывод; сегодня это стоило двух отчётов и разбора.

Признак прямого пуша (единственный корректный):
  коммит в ПЕРВОЙ линии (`--first-parent`) И у него НЕ больше одного родителя (не merge)
  И он не бот-коммит чата (`chat(bot):`, PDR-047).

Всё остальное — штатный мерж PR: роль вливает в `role/<name>`, в `main` попадает merge.

Коды: `0` — прямых пушей нет; `1` — есть; `2` — не измерено.
"""
import argparse
import os
import shutil
import subprocess
import sys

BOT_PREFIX = 'chat(bot)'


def git(args, cwd):
    return subprocess.run(['git'] + args, cwd=cwd, capture_output=True, text=True,
                          encoding='utf-8', errors='replace')


def commits(ref, cwd, limit):
    cp = git(['rev-list', '--first-parent', '-%d' % limit, ref], cwd)
    return [s for s in cp.stdout.split() if s]


def info(sha, cwd):
    cp = git(['show', '-s', '--format=%h%x00%s%x00%p', sha], cwd)
    if cp.returncode != 0:
        return None
    parts = cp.stdout.strip().split('\x00')
    if len(parts) < 3:
        return None
    return {'sha': parts[0], 'subject': parts[1], 'parents': parts[2].split()}


def scan(ref, cwd, limit=40):
    """[(sha, subject)] — коммиты первой линии, которые являются прямым пушем."""
    bad, checked = [], 0
    for sha in commits(ref, cwd, limit):
        ci = info(sha, cwd)
        if ci is None:
            continue
        checked += 1
        if len(ci['parents']) > 1:          # merge — штатный путь PR
            continue
        if ci['subject'].startswith(BOT_PREFIX):   # бот чата (PDR-047)
            continue
        bad.append((ci['sha'], ci['subject']))
    return bad, checked


def main_check(args):
    bad, checked = scan(args.ref, args.root, args.limit)
    print('check_direct_push: ref=%s, проверено коммитов первой линии: %d' % (args.ref, checked))
    if not checked:
        print('::error::check_direct_push: нечего проверять (не измерено)')
        return 2
    for sha, subj in bad:
        print('::error::прямой пуш в main: %s %s' % (sha, subj[:100]))
    if bad:
        print('check_direct_push: прямых пушей %d — роль пушит в role/<name>, в main через PR' % len(bad))
        return 1
    print('check_direct_push: прямых пушей нет (все — merge PR или бот чата)')
    return 0


def selftest():
    base = os.path.join(os.getcwd(), '.tmp-direct-push-selftest')
    shutil.rmtree(base, ignore_errors=True)
    repo = os.path.join(base, 'r')
    os.makedirs(repo)

    def r(*a):
        return subprocess.run(['git'] + list(a), cwd=repo, capture_output=True, text=True,
                              encoding='utf-8', errors='replace')

    def commit(msg, path='f'):
        with open(os.path.join(repo, path), 'a', encoding='utf-8') as fh:
            fh.write(msg + '\n')
        r('add', '-A')
        r('-c', 'user.email=t@t', '-c', 'user.name=role', 'commit', '-qm', msg)
        return r('rev-parse', 'HEAD').stdout.strip()

    r('init', '-q', '-b', 'main')
    commit('init')
    # 1) прямой пуш роли в main
    direct = commit('do: прямой пуш роли')
    # 2) ветка + merge: коммит роли во ВТОРОМ родителе — не нарушение
    r('checkout', '-q', '-b', 'role/do')
    branch_commit = commit('do: коммит в ветке')
    r('checkout', '-q', 'main')
    r('-c', 'user.email=t@t', '-c', 'user.name=role', 'merge', '--no-ff', '-m',
      'Merge pull request #1 from role/do', 'role/do')
    merge_sha = r('rev-parse', 'HEAD').stdout.strip()
    # 3) бот-коммит чата первой линией — разрешён
    os.makedirs(os.path.join(repo, '5-tech-backlog', 'chat'), exist_ok=True)
    commit('chat(bot): sync [skip ci]', '5-tech-backlog/chat/do.md')

    bad, checked = scan('HEAD', repo, limit=20)
    shas = [s for s, _ in bad]
    cases = [
        ('найден прямой пуш', direct[:7] in shas),
        ('merge-коммит не считается', merge_sha[:7] not in shas),
        ('коммит из PR (второй родитель) не считается', branch_commit[:7] not in shas),
        ('бот-коммит чата не считается', not any('chat(bot)' in s for _, s in bad)),
        ('проверено 4 коммита первой линии', checked == 4),
        ('ровно один нарушитель', len(bad) == 1),
    ]
    shutil.rmtree(base, ignore_errors=True)
    passed = sum(1 for _, ok in cases if ok)
    for name, ok in cases:
        print('  [%s] %s' % ('OK ' if ok else 'FAIL', name))
    print('check_direct_push --selftest: %d/%d' % (passed, len(cases)))
    return 0 if passed == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description='Прямые пуши в main: топология, а не имена')
    ap.add_argument('--ref', default='HEAD', help='что проверять (по умолчанию HEAD)')
    ap.add_argument('--limit', type=int, default=40, help='сколько коммитов первой линии')
    ap.add_argument('--root', default='.')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    return main_check(a)


if __name__ == '__main__':
    sys.exit(main())
