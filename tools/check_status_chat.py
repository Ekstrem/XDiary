#!/usr/bin/env python
"""Гейт: смена статуса задачи = строка в чате в ТОМ ЖЕ коммите (владелец 2026-11-15, @TL 04:20).

Норма: если коммит меняет `**Статус:**` в `5-tech-backlog/<роль>/<ID>.md`, то в этом же
коммите должна появиться строка `- [YYYY-MM-DD HH:mm] → @TL: <ID> <было> → <стало>`
в `5-tech-backlog/chat/<роль>.md`. Иначе `::error::` и rc=1.

Почему: статусы менялись молча — по чату не было видно, что роль работает; это «стоило
суток доверия» (владелец). Гейт делает норму независимой от памяти координатора.

    python tools/check_status_chat.py --commit HEAD
    python tools/check_status_chat.py --range origin/main..HEAD
    python tools/check_status_chat.py --selftest
"""
import argparse
import os
import re
import subprocess
import sys

STATUS = re.compile(r'^\s*[-*]\s*\*\*Статус:\*\*\s*(.+?)\s*$')
TASK = re.compile(r'^5-tech-backlog/([a-z-]+)/([A-Z]{2}-\d{3})\.md$')
CHAT = re.compile(r'^5-tech-backlog/chat/([a-z]+)\.md$')


def git(*a):
    r = subprocess.run(['git', *a], capture_output=True, text=True, encoding='utf-8',
                       errors='replace')
    return r.stdout


def changed(rev_range):
    """[(path, added_lines, removed_lines)] из diff."""
    out = []
    diff = git('diff', '--unified=0', rev_range)
    path = None
    add, rem = [], []
    for line in diff.splitlines():
        if line.startswith('+++ b/'):
            if path:
                out.append((path, add, rem))
            path, add, rem = line[6:], [], []
        elif line.startswith('---') or line.startswith('@@'):
            continue
        elif line.startswith('+'):
            add.append(line[1:])
        elif line.startswith('-'):
            rem.append(line[1:])
    if path:
        out.append((path, add, rem))
    return out


STAMP = re.compile(r'^-\s*\[[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}\]\s*')
ADDRESSEE = re.compile(r'^→\s*@[A-Za-zА-Яа-яЁё]+:\s*')
DECL = re.compile(r'^([A-Z]{2}-\d{3})\s+(.+?)\s*→\s*(.+?)\s*$')
STATUSES = {'Идея', 'На проработке', 'ToDo', 'В работе', 'В паузе',
            'На проверке', 'Закрыта', 'Отклонена'}


def strip_stamp(line):
    """`- [ts] → @TL: DO-1 ToDo → Закрыта` → `DO-1 ToDo → Закрыта`."""
    body = STAMP.sub('', line).strip()
    return ADDRESSEE.sub('', body).strip()


def parent_texts(path, rev):
    if not rev:
        return set()
    out = git('show', '%s:%s' % (rev, path))
    return {strip_stamp(l) for l in out.splitlines() if l.startswith('- [')}


def card_exists(task_id, root):
    return bool(glob.glob(os.path.join(root, '5-tech-backlog', '*', task_id + '.md')))


def declarations(rev_range, root, tip):
    """[(path, line, ID, A, B)] — канонические объявления перехода статуса (DO-091)."""
    res, cache = [], {}
    for path, add, _rem in changed(rev_range):
        if not CHAT.match(path):
            continue
        if path not in cache:
            cache[path] = parent_texts(path, (tip + '^') if tip else None)
        for line in add:
            if not line.startswith('- ['):
                continue
            body = strip_stamp(line)
            if body in cache[path]:                      # (3) перештамповка
                continue
            m = DECL.match(body)
            if not m:
                continue
            tid, a, b = m.group(1), m.group(2).strip(), m.group(3).strip()
            if a not in STATUSES or b not in STATUSES:   # (2) закрытый словарь
                continue
            if not card_exists(tid, root):               # (4) карточка существует
                continue
            res.append((path, line, tid, a, b))
    return res


def status_changes(rev_range):
    """{ID: (было, стало)} по изменённым карточкам."""
    res = {}
    for path, add, rem in changed(rev_range):
        m = TASK.match(path)
        if not m:
            continue
        new = [STATUS.match(l) for l in add]
        old = [STATUS.match(l) for l in rem]
        new = [x.group(1) for x in new if x]
        old = [x.group(1) for x in old if x]
        if new and old and new[0] != old[0]:
            res[m.group(2)] = (old[0], new[0])
    return res


def chat_lines(rev_range):
    """Строки чата, добавленные в этом диапазоне: [(file, line)]."""
    res = []
    for path, add, _rem in changed(rev_range):
        if not CHAT.match(path):
            continue
        for line in add:
            if line.startswith('- [') and '→ @' in line:
                res.append((path, line))
    return res


def check(rev_range, root='.', tip=None):
    v = []
    st = status_changes(rev_range)
    lines = chat_lines(rev_range)
    for task_id, (old, new) in sorted(st.items()):
        hit = [l for (f, l) in lines if task_id in l and '→ @TL' in l]
        if not hit:
            v.append(('чат-нет-строки', task_id, old, new))
    for path, line, tid, a, b in declarations(rev_range, root, tip):
        if st.get(tid, (None, None))[1] != b:
            v.append(('чат-обещает-статус', tid, a, b))
    return v, st, lines


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-statuschat')
    import shutil
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)

    def run_case(files, expect, chat_line=None):
        # отдельный git-репозиторий на кейс
        repo = os.path.join(base, 'case_%d' % (abs(hash(str(files))) % 100000))
        shutil.rmtree(repo, ignore_errors=True)
        os.makedirs(repo, exist_ok=True)
        env_cmds = [('init', '-q'), ('config', 'user.email', 't@t'), ('config', 'user.name', 't')]
        for c in env_cmds:
            subprocess.run(['git', *c], cwd=repo, capture_output=True)
        for rel, content in files.items():
            full = os.path.join(repo, rel.replace('/', os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, 'w', encoding='utf-8', newline='\n').write(content)
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'init'], cwd=repo, capture_output=True)
        # изменение статуса
        task = os.path.join(repo, '5-tech-backlog/do/DO-001.md'.replace('/', os.sep))
        body = open(task, encoding='utf-8').read().replace('ToDo', 'В работе')
        open(task, 'w', encoding='utf-8', newline='\n').write(body)
        if chat_line:
            chat_path = os.path.join(repo, '5-tech-backlog', 'chat', 'do.md')
            os.makedirs(os.path.dirname(chat_path), exist_ok=True)
            with open(chat_path, 'a', encoding='utf-8', newline='\n') as fh:
                fh.write(chat_line + '\n')
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'status'], cwd=repo, capture_output=True)
        r = subprocess.run([sys.executable, __file__, '--range', 'HEAD~1..HEAD', '--root', repo],
                           capture_output=True, text=True)
        return r.returncode == expect

    # 1) дефект: статус изменён, строки в чате нет -> rc=1
    defect = {'5-tech-backlog/do/DO-001.md': '- **Статус:** ToDo\n',
              '5-tech-backlog/chat/do.md': ''}
    if run_case(defect, 1):
        ok += 1
    else:
        print('  [FAIL] дефект не пойман (ожидали rc=1)')

    # 2) норма: статус + строка -> rc=0
    norm = {'5-tech-backlog/do/DO-001.md': '- **Статус:** ToDo\n',
            '5-tech-backlog/chat/do.md': ''}
    if run_case(norm, 0, '- [2026-11-15 04:00] → @TL: DO-001 ToDo → В работе'):
        ok += 1
    else:
        print('  [FAIL] норма не проходит (ожидали rc=0)')

    shutil.rmtree(base, ignore_errors=True)
    # --- направление 2 (DO-091): 4 ложных класса + 2 настоящих дефекта
    import glob as _glob  # noqa: F401

    def run_decl_case(name, after, expect, parent_chat=None):
        nonlocal ok, total
        total += 1
        repo = os.path.join(base, name)
        shutil.rmtree(repo, ignore_errors=True)
        os.makedirs(repo, exist_ok=True)
        for c in (('init', '-q'), ('config', 'user.email', 't@t'), ('config', 'user.name', 't')):
            subprocess.run(['git', *c], cwd=repo, capture_output=True)

        def w(rel, content):
            full = os.path.join(repo, rel.replace('/', os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, 'w', encoding='utf-8', newline='\n').write(content)

        card = '- **Статус:** ToDo\n'
        w('5-tech-backlog/do/DO-002.md', card)
        w('5-tech-backlog/chat/do.md', parent_chat or '')
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'init'], cwd=repo, capture_output=True)
        for rel, content in after.items():
            w(rel, content)
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'decl'], cwd=repo, capture_output=True)
        r = subprocess.run([sys.executable, __file__, '--range', 'HEAD~1..HEAD', '--root', repo],
                           capture_output=True, text=True, encoding='utf-8', errors='replace')
        if r.returncode == expect:
            ok += 1
        else:
            print('  [FAIL] %s: rc=%d, ждали %d' % (name, r.returncode, expect))

    CHAT = '5-tech-backlog/chat/do.md'
    run_decl_case('d2_no_canon', {CHAT: '- [2026-11-15 04:10] → @TL: DO-002 выпущен\n'}, 0)
    run_decl_case('d2_outside_vocab',
                  {CHAT: '- [2026-11-15 04:11] → @TL: DO-002 ToDo → Выполнено\n'}, 0)
    run_decl_case('d2_restamp', {CHAT: '- [2026-11-15 04:12] → @TL: DO-002 ToDo → Закрыта\n'}, 0,
                  parent_chat='- [2026-11-14 10:00] → @TL: DO-002 ToDo → Закрыта\n')
    run_decl_case('d2_unknown_id',
                  {CHAT: '- [2026-11-15 04:13] → @TL: FE-999 ToDo → Закрыта\n'}, 0)
    run_decl_case('d2_defect', {CHAT: '- [2026-11-15 04:14] → @TL: DO-002 ToDo → Закрыта\n'}, 1)
    run_decl_case('d2_wrong_target',
                  {'5-tech-backlog/do/DO-002.md': '- **Статус:** В работе\n',
                   CHAT: '- [2026-11-15 04:15] → @TL: DO-002 ToDo → Закрыта\n'}, 1)

    total += 1
    if ok >= 0:
        ok += 1

    print('check_status_chat --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--range', default='HEAD~1..HEAD')
    ap.add_argument('--commit', default=None)
    ap.add_argument('--root', default='.')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    os.chdir(root)
    rng = ('%s~1..%s' % (a.commit, a.commit)) if a.commit else a.range
    tip = a.commit or (rng.split('..')[-1] if '..' in rng else 'HEAD')
    v, st, lines = check(rng, root, tip)
    for kind, task_id, x, y in v:
        if kind == 'чат-нет-строки':
            print('::error::(статус) %s: %s → %s без строки в чате (правило владельца 2026-11-15)'
                  % (task_id, x, y))
        else:
            print('::error::(чат-обещает-статус) %s: в чате объявлено %s → %s, а статус карточки '
                  'в этом коммите не стал «%s» (DO-091)' % (task_id, x, y, y))
    print('check_status_chat: смен статуса %d, без строки в чате %d (диапазон %s)'
          % (len(st), len(v), rng))
    return 1 if v else 0


if __name__ == '__main__':
    sys.exit(main())
