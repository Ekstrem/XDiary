#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_direct_push.py` — прямой пуш в `main` ловится ТОПОЛОГИЕЙ, а не по имени коммита.

Зачем: кнут @LM 2026-11-13 08:33 обвинил @DO и @TL в «прямом пуше в main (do: DO-123,
tl: TL-002)», хотя оба коммита приехали через PR: в `main` они — **вторые родители**
merge-коммитов. Детектор, который ищет по `git log --grep`/`git rev-list`, находит их и
делает ложный вывод; сегодня это стоило двух отчётов и разбора.

Признак прямого пуша (единственный корректный):
  коммит в ПЕРВОЙ линии (`--first-parent`) И у него НЕ больше одного родителя (не merge)
  И он не коммит ЧАТА (PDR-047: чат коммитит бот).

Коммит чата определяется не по префиксу сообщения (бот пишет и `chat(bot): sync`, и
`chat: убраны маркеры конфликта…` — второй случай давал ложный кнут 2026-11-13 14:05),
а по составу: если ВСЕ изменённые файлы лежат под `5-tech-backlog/chat/`, это чат.

Всё остальное — штатный мерж PR: роль вливает в `role/<name>`, в `main` попадает merge.

Коды: `0` — прямых пушей нет; `1` — есть; `2` — не измерено.
"""
import argparse
import os
import shutil
import subprocess
import sys

BOT_PREFIX = 'chat(bot)'
CHAT_PREFIX = '5-tech-backlog/chat/'


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
    return {'sha': parts[0], 'subject': parts[1], 'parents': parts[2].split(),
            'full_sha': sha}


def touches_only_chat(sha, cwd):
    """True, если коммит меняет ТОЛЬКО chat/** (тогда это бот чата, PDR-047)."""
    cp = git(['show', '--name-only', '--format=', sha], cwd)
    files = [f for f in cp.stdout.splitlines() if f.strip()]
    if not files:
        return False
    return all(f.startswith(CHAT_PREFIX) for f in files)


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
        if ci['subject'].startswith(BOT_PREFIX):   # бот чата по префиксу (PDR-047)
            continue
        if touches_only_chat(ci['full_sha'], cwd):  # бот чата по составу файлов (PDR-047)
            continue
        bad.append((ci['full_sha'], ci['subject']))
    return bad, checked


def main_check(args):
    bad, checked = scan(args.ref, args.root, args.limit)
    print('check_direct_push: ref=%s, проверено коммитов первой линии: %d' % (args.ref, checked))
    if not checked:
        print('::error::check_direct_push: нечего проверять (не измерено)')
        return 2
    for sha, subj in bad:
        print('::error::прямой пуш в main: %s %s' % (sha[:9], subj[:100]))
    if bad:
        print('check_direct_push: прямых пушей %d — роль пушит в role/<name>, в main через PR' % len(bad))
        return 1
    print('check_direct_push: прямых пушей нет (все — merge PR или бот чата)')
    return 0


def selftest():
    base = os.path.join(os.getcwd(), '.tmp-direct-push-selftest')
    shutil.rmtree(base, ignore_errors=True)
    repo = os.path.join(base, 'r')
    os.makedirs(repo, exist_ok=True)

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
    # бот с другим префиксом — раньше это был ложный кнут
    chat_only = commit('chat: убраны маркеры конфликта', '5-tech-backlog/chat/te.md')
    # чат вместе с кодом — это уже прямой пуш роли, не бот
    with open(os.path.join(repo, '7-deploy'), 'w', encoding='utf-8') as fh:
        fh.write('x\n')
    with open(os.path.join(repo, '5-tech-backlog', 'chat', 'tl.md'), 'w', encoding='utf-8') as fh:
        fh.write('y\n')
    r('add', '-A')
    r('-c', 'user.email=t@t', '-c', 'user.name=role', 'commit', '-qm', 'do: чат и код вместе')
    chat_plus_code = r('rev-parse', 'HEAD').stdout.strip()

    bad, checked = scan('HEAD', repo, limit=20)
    shas = [s for s, _ in bad]
    cases = [
        ('найден прямой пуш', any(x.startswith(direct[:7]) for x in shas)),
        ('merge-коммит не считается', not any(x.startswith(merge_sha[:7]) for x in shas)),
        ('коммит из PR (второй родитель) не считается',
         not any(x.startswith(branch_commit[:7]) for x in shas)),
        ('бот-коммит чата не считается', not any('chat(bot)' in s for _, s in bad)),
        ('проверено 6 коммитов первой линии', checked == 6),
        # корневой коммит (`init`) тоже не merge и не бот — он честно попадает в список,
        # поэтому ожидаем ДВУХ нарушителей: init + прямой пуш роли. Первая версия ждала одного
        # и падала 5/6 — тест поймал сам себя, это и есть его работа.
        ('нарушителей три: init + прямой пуш + chat&код', len(bad) == 3),
        # 2026-11-13 14:05: бот коммитит чат и с префиксом `chat(bot):`, и с `chat:` —
        # второй давал ложный кнут. Признак чата — состав файлов, а не сообщение.
        ('коммит только chat/** не считается', chat_only not in [x[0] for x in bad]),
        ('коммит chat + код считается', chat_plus_code in [x[0] for x in bad]),
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
