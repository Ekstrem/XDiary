#!/usr/bin/env python3
"""
check_canon.py — канон = единственный источник правды (гейт).

Маркер `<!-- canon: <id> -->` помечает блок-канон. Он обязан встречаться в
репозитории РОВНО ОДИН раз: второе вхождение — копия нормы, которая разойдётся
на первой же правке (класс «второй источник правды»). В остальных местах
допустима только ссылка «канон — <файл>», а не сам текст нормы.

Источник: AR 2026-11-05 00:40 (норма квитанции (ii-b) в ADR-026 была скопирована; текст перенесён в `ADR-026 (Принято; канон измерений переносится в ADR-031, который Предложено)`ана
в 9-tests/README.md и ITERATION-PLAN §19.1; просьбы «убери копию» выполнялись
частично, потому что правило держалось на памяти, а не на механике).

Запуск:
    python tools/check_canon.py                 # по умолчанию все *.md/*.py
    python tools/check_canon.py --config loop.config.json
    python tools/check_canon.py --selftest      # 2/2
"""

import argparse
import fnmatch
import subprocess
import glob
import json
import os
import re
import sys

MARK = re.compile(r'<!--\s*canon:\s*([\w][\w.-]*)\s*-->')
DEFAULT_PATTERNS = ['**/*.md']


def _git_tracked(root, patterns):
    """Файлы из `git ls-files` по шаблонам; None — если это не git-корень.

    Почему не `glob` по дереву (2026-11-06, находка @AR): в рабочем каталоге лежит
    вложенный worktree `origin/main/`, и обход дерева считал копии ADR оттуда — гейт
    печатал «canon:receipt-v1 встречается 3 раза» там, где в репозитории маркер ОДИН
    (`git ls-files` не содержит ни одного пути `origin/main/`). Тот же класс уже
    исправлен в `check_rule_docs` и `check_docs_size`.
    """
    try:
        out = subprocess.run(["git", "ls-files"], cwd=root, capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
    except OSError:
        return None
    if out.returncode != 0 or not out.stdout.strip():
        return None
    files = []
    for rel in out.stdout.splitlines():
        rel = rel.strip()
        if rel and any(fnmatch.fnmatch(rel, pat.lstrip("./")) for pat in patterns):
            files.append(rel)
    return sorted(set(files))


def _walk_files(root, patterns):
    """Обход дерева (fallback). DO-115: НЕ заходит во вложенные репозитории.

    Раньше `glob(..., recursive=True)` попадал в вложенный worktree `origin/main/`
    (и любой каталог с `.git`) и считал один маркер несколько раз — отсюда ложное
    «canon:receipt-v1 встречается 3 раза». Каталог с `.git` — граница репозитория,
    внутрь не идём.
    """
    files = []
    for pat in patterns:
        for path in glob.glob(os.path.join(root, pat), recursive=True):
            if os.path.isdir(path):
                continue
            rel = os.path.relpath(path, root).replace("\\", "/")
            parts = rel.split("/")[:-1]
            nested = False
            probe = root
            for part in parts:
                probe = os.path.join(probe, part)
                if os.path.exists(os.path.join(probe, ".git")):
                    nested = True
                    break
            if nested:
                continue
            files.append(rel)
    return sorted(set(files))


def scan(root, patterns, allow_walk=False):
    """id канона -> список путей, где маркер встретился.

    DO-115: если `root` не git-корень, честнее сказать «не измерено» (rc=2),
    чем считать по обходу дерева: обход видит вложенные копии и врёт числом.
    `allow_walk=True` — только для `--selftest` (там проверяется логика подсчёта).
    """
    counts = {}
    rels = _git_tracked(root, patterns)
    source = "git ls-files" if rels is not None else "НЕ ИЗМЕРЕНО: root не git-корень"
    if rels is None and allow_walk:
        rels = _walk_files(root, patterns)
        source = "обход дерева (selftest-only)"
    if rels is None:
        print("  источник файлов: %s" % source)
        return None
    for rel in rels:
        if "/chat/" in "/" + rel:
            continue
        path = os.path.join(root, rel.replace("/", os.sep))
        try:
            text = open(path, encoding="utf-8-sig").read()
        except (OSError, UnicodeDecodeError):
            continue
        for m in MARK.finditer(text):
            counts.setdefault(m.group(1), []).append("./" + rel)
    print("  источник файлов: %s (%d шт.)" % (source, len(rels)))
    return counts


def run(root, patterns, allow_walk=False, annotate=True):
    counts = scan(root, patterns, allow_walk=allow_walk)
    if counts is None:
        # DO-115: аннотация `::error::` — только для настоящего прогона. В `--selftest`
        # этот же путь проверяется намеренно, и `::error::` в логе делал шаг CI красным
        # (ci-guards #4382: «check_canon: НЕ ИЗМЕРЕНО» при rc=0 у самопроверки).
        msg = ('check_canon: НЕ ИЗМЕРЕНО — root не git-корень (обход дерева не используется: '
               'он считает вложенные копии; см. DO-115)')
        print(msg if (allow_walk or not annotate) else '::error::' + msg)
        return 2
    errors = 0
    if not counts:
        print('нет маркеров canon: — нечего проверять')
        return 0
    for cid in sorted(counts):
        paths = counts[cid]
        if len(paths) > 1:
            print(f'ERROR: canon:{cid} встречается {len(paths)} раз (нужен ровно 1): ' + ', '.join(paths))
            errors += 1
        else:
            print(f'ok: canon:{cid} — {paths[0]}')
    return 1 if errors else 0


def selftest():
    tmp = os.path.join(os.getcwd(), '.tmp-check_canon-selftest')
    os.makedirs(tmp, exist_ok=True)
    try:
        # 1) дубль маркера -> ошибка
        d = os.path.join(tmp, 'd1')
        os.makedirs(d)
        open(os.path.join(d, 'a.md'), 'w', encoding='utf-8').write('<!-- canon: x -->\n')
        open(os.path.join(d, 'b.md'), 'w', encoding='utf-8').write('<!-- canon: x -->\n')
        r1 = run(d, ['*.md'], allow_walk=True)
        # 2) один маркер -> ok
        d2 = os.path.join(tmp, 'd2')
        os.makedirs(d2)
        open(os.path.join(d2, 'a.md'), 'w', encoding='utf-8').write('<!-- canon: y -->\n')
        r2 = run(d2, ['*.md'], allow_walk=True)
        # 3) DO-115: не git-корень -> rc=2 «не измерено» (а не обход с враньём)
        r3 = run(d, ['*.md'], annotate=False)
        # 4) DO-115: вложенный каталог с .git не учитывается при обходе
        d4 = os.path.join(tmp, 'd4')
        nested = os.path.join(d4, 'origin', 'main')
        os.makedirs(nested)
        os.makedirs(os.path.join(nested, '.git'))
        open(os.path.join(d4, 'a.md'), 'w', encoding='utf-8').write('<!-- canon: z -->\n')
        open(os.path.join(nested, 'b.md'), 'w', encoding='utf-8').write('<!-- canon: z -->\n')
        counts4 = scan(d4, ['**/*.md'], allow_walk=True)
        r4 = 1 if (counts4 and len(counts4.get('z', [])) == 1) else 0
        print(f'selftest: {r1}/{r2}/{r3}/{r4} (ждём 1/0/2/1)')
        return 0 if (r1 == 1 and r2 == 0 and r3 == 2 and r4 == 1) else 1
    finally:
        import shutil
        shutil.rmtree(tmp, ignore_errors=True)


def main(argv=None):
    ap = argparse.ArgumentParser(description='гейт: маркер canon: встречается ровно один раз')
    ap.add_argument('--config', help='loop.config.json (секция canon.patterns)')
    ap.add_argument('--root', default='.')
    ap.add_argument('--selftest', action='store_true')
    args = ap.parse_args(argv)

    if args.selftest:
        sys.exit(selftest())

    patterns = DEFAULT_PATTERNS
    if args.config and os.path.isfile(args.config):
        cfg = json.load(open(args.config, encoding='utf-8'))
        patterns = cfg.get('canon', {}).get('patterns', DEFAULT_PATTERNS)

    sys.exit(run(args.root, patterns))


if __name__ == '__main__':
    main()
