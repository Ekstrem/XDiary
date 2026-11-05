#!/usr/bin/env python
"""Gate: status <-> chat in BOTH directions (owner 2026-11-15; @TL 04:20; DO-091).

Direction 1: a commit changes `**Status:**` in a card -> the same commit must add a chat line
             `- [YYYY-MM-DD HH:mm] -> @TL: <ID> <A> -> <B>`.
Direction 2 (DO-091): a chat line DECLARES a status transition -> the same commit must set the
             card status to exactly <B>. Otherwise the chat claims a status the card does not have.

Declaration predicate (4 conditions, measured by @FE on 400 commits):
  1. canonical form `<ID> <A> -> <B>`;
  2. BOTH statuses from the closed vocabulary of 8;
  3. restamp is ignored: same text (without timestamp) already present in the parent commit;
  4. `<ID>` exists as a card `5-tech-backlog/*/<ID>.md`.

    python tools/check_status_chat.py --commit HEAD
    python tools/check_status_chat.py --range origin/main..HEAD
    python tools/check_status_chat.py --selftest
"""
import argparse
import glob
import os
import re
import subprocess
import sys

STATUS = re.compile(r'^\s*[-*]\s*\*\*\u0421\u0442\u0430\u0442\u0443\u0441:\*\*\s*(.+?)\s*$')
TASK = re.compile(r'^5-tech-backlog/([a-z-]+)/([A-Z]{2}-\d{3})\.md$')
CHAT = re.compile(r'^5-tech-backlog/chat/([a-z]+)\.md$')
CHAT_LINE = re.compile(r'^- \[\d{4}-\d{2}-\d{2} \d{2}:\d{2}\] \u2192 @([A-Za-z\u0410-\u044f]+):\s*(.+)$')
DECL = re.compile(r'^`?([A-Z]{2}-\d{3})`?\s+(.+?)\s*\u2192\s*(.+?)\s*$')
TICKS = re.compile(r'[`\u2018\u2019\u00ab\u00bb]')

STATUSES = {'\u0418\u0434\u0435\u044f', '\u041d\u0430 \u043f\u0440\u043e\u0440\u0430\u0431\u043e\u0442\u043a\u0435',
            'ToDo', '\u0412 \u0440\u0430\u0431\u043e\u0442\u0435', '\u0412 \u043f\u0430\u0443\u0437\u0435',
            '\u041d\u0430 \u043f\u0440\u043e\u0432\u0435\u0440\u043a\u0435', '\u0417\u0430\u043a\u0440\u044b\u0442\u0430',
            '\u041e\u0442\u043a\u043b\u043e\u043d\u0435\u043d\u0430'}


def git(*a):
    r = subprocess.run(['git', *a], capture_output=True, text=True, encoding='utf-8',
                       errors='replace')
    return r.stdout


def changed(rev_range):
    """[(path, added, removed)] from diff."""
    out, path, add, rem = [], None, [], []
    for line in git('diff', '--unified=0', rev_range).splitlines():
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


def status_changes(rev_range):
    """{ID: (was, became)} over changed cards."""
    res = {}
    for path, add, rem in changed(rev_range):
        m = TASK.match(path)
        if not m:
            continue
        new = [x.group(1) for x in (STATUS.match(l) for l in add) if x]
        old = [x.group(1) for x in (STATUS.match(l) for l in rem) if x]
        if new and old and new[0] != old[0]:
            res[m.group(2)] = (old[0], new[0])
    return res


def chat_lines(rev_range):
    """[(file, line, addressee, body)] for chat lines added in range."""
    res = []
    for path, add, _rem in changed(rev_range):
        if not CHAT.match(path):
            continue
        for line in add:
            m = CHAT_LINE.match(line)
            if m:
                res.append((path, line, m.group(1), m.group(2).strip()))
    return res


def parent_bodies(path, rev):
    if not rev:
        return set()
    out = git('show', '%s:%s' % (rev, path))
    res = set()
    for line in out.splitlines():
        m = CHAT_LINE.match(line)
        if m:
            res.add(m.group(2).strip())
    return res


def card_exists(task_id, root):
    return bool(glob.glob(os.path.join(root, '5-tech-backlog', '*', task_id + '.md')))


def declarations(rev_range, root, tip):
    res, cache = [], {}
    for path, add, _rem in changed(rev_range):
        if not CHAT.match(path):
            continue
        if path not in cache:
            cache[path] = parent_bodies(path, (tip + '^') if tip else None)
        for line in add:
            m = CHAT_LINE.match(line)
            if not m:
                continue
            body = m.group(2).strip()
            if body in cache[path]:                         # restamp
                continue
            d = DECL.match(TICKS.sub('', body).strip())
            if not d:
                continue
            tid, a, b = d.group(1), d.group(2).strip(), d.group(3).strip()
            a, b = TICKS.sub('', a).strip(), TICKS.sub('', b).strip()
            if a not in STATUSES or b not in STATUSES:      # closed vocabulary
                continue
            if not card_exists(tid, root):                  # card exists
                continue
            res.append((path, line, tid, a, b))
    return res


def check(rev_range, root='.', tip=None):
    v = []
    st = status_changes(rev_range)
    lines = chat_lines(rev_range)
    for task_id, (old, new) in sorted(st.items()):
        if not [x for x in lines if task_id in x[1] and x[2] == 'TL']:
            v.append(('no-chat-line', task_id, old, new))
    for _path, _line, tid, a, b in declarations(rev_range, root, tip):
        if st.get(tid, (None, None))[1] != b:
            v.append(('chat-promises', tid, a, b))
    return v, st, lines


def selftest():
    import shutil
    ok = total = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-statuschat')
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    CARD = '- **\u0421\u0442\u0430\u0442\u0443\u0441:** ToDo\n'
    CARD_WORK = '- **\u0421\u0442\u0430\u0442\u0443\u0441:** \u0412 \u0440\u0430\u0431\u043e\u0442\u0435\n'
    CHAT = '5-tech-backlog/chat/do.md'
    CARD_PATH = '5-tech-backlog/do/DO-001.md'

    def case(name, initial, second, expect):
        nonlocal ok, total
        total += 1
        repo = os.path.join(base, name)
        shutil.rmtree(repo, ignore_errors=True)
        os.makedirs(repo, exist_ok=True)
        for c in (('init', '-q'), ('config', 'user.email', 't@t'), ('config', 'user.name', 't')):
            subprocess.run(['git', *c], cwd=repo, capture_output=True)

        def w(files):
            for rel, content in files.items():
                full = os.path.join(repo, rel.replace('/', os.sep))
                os.makedirs(os.path.dirname(full), exist_ok=True)
                open(full, 'w', encoding='utf-8', newline='\n').write(content)

        w(initial)
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'init'], cwd=repo, capture_output=True)
        w(second)
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        subprocess.run(['git', 'commit', '-qm', 'second'], cwd=repo, capture_output=True)
        r = subprocess.run([sys.executable, __file__, '--range', 'HEAD~1..HEAD', '--root', repo],
                           capture_output=True, text=True, encoding='utf-8', errors='replace')
        if r.returncode == expect:
            ok += 1
        else:
            print('  [FAIL] %s: rc=%d, want %d | %s'
                  % (name, r.returncode, expect, (r.stdout or '').strip()[-140:]))

    def line(body):
        return '- [2026-11-15 04:00] \u2192 @TL: ' + body + '\n'

    def line(body):
        return '- [2026-11-15 04:00] \u2192 @TL: ' + body + '\n'

    # порядок по @FE 2026-11-05 14:26: СНАЧАЛА дефект (иначе зелено на пустоте), затем ложные.
    # (1) настоящий дефект: объявление без смены статуса -> rc=1
    case('d2_defect', {CARD_PATH: CARD, CHAT: ''},
         {CHAT: line('`DO-001` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430')}, 1)
    # (2) норма: та же строка + смена статуса в том же коммите -> rc=0
    case('d2_norm', {CARD_PATH: CARD, CHAT: ''},
         {CARD_PATH: '- **\u0421\u0442\u0430\u0442\u0443\u0441:** \u0417\u0430\u043a\u0440\u044b\u0442\u0430\n',
          CHAT: line('`DO-001` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430')}, 0)
    # (3) перештамповка: тот же текст уже был у родителя с другой меткой -> rc=0
    case('d2_restamp',
         {CARD_PATH: CARD, CHAT: '- [2026-11-14 10:00] \u2192 @TL: `DO-001` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430\n'},
         {CHAT: '- [2026-11-15 04:12] \u2192 @TL: `DO-001` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430\n'}, 0)
    # (4) цитата примера: карточки FE-999 нет -> rc=0
    case('d2_unknown_id', {CARD_PATH: CARD, CHAT: ''},
         {CHAT: line('`FE-999` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430')}, 0)
    # (5) слово статуса без перехода A -> B -> rc=0
    case('d2_no_canon', {CARD_PATH: CARD, CHAT: ''},
         {CHAT: line('`DO-001` \u2192 `\u041d\u0430 \u043f\u0440\u043e\u0432\u0435\u0440\u043a\u0435`')}, 0)
    # (6) статус вне закрытого словаря -> rc=0
    case('d2_outside_vocab', {CARD_PATH: CARD, CHAT: ''},
         {CHAT: line('`DO-001` ToDo \u2192 \u0412\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u043e')}, 0)
    # (7) сменил, но не на объявленный статус -> rc=1
    case('d2_wrong_target', {CARD_PATH: CARD, CHAT: ''},
         {CARD_PATH: CARD_WORK,
          CHAT: line('`DO-001` ToDo \u2192 \u0417\u0430\u043a\u0440\u044b\u0442\u0430')}, 1)
    # направление 1
    case('d1_defect', {CARD_PATH: CARD, CHAT: ''}, {CARD_PATH: CARD_WORK}, 1)
    case('d1_norm', {CARD_PATH: CARD, CHAT: ''},
         {CARD_PATH: CARD_WORK, CHAT: line('DO-001 ToDo \u2192 \u0412 \u0440\u0430\u0431\u043e\u0442\u0435')}, 0)

    shutil.rmtree(base, ignore_errors=True)
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
        if kind == 'no-chat-line':
            print('::error::(status) %s: %s -> %s without a chat line (owner rule 2026-11-15)'
                  % (task_id, x, y))
        else:
            print('::error::(chat-promises) %s: chat declares %s -> %s, but the card status '
                  'did not become %s in this commit (DO-091)' % (task_id, x, y, y))
    print('check_status_chat: status changes %d, chat lines %d, violations %d (range %s)'
          % (len(st), len(lines), len(v), rng))
    return 1 if v else 0


if __name__ == '__main__':
    sys.exit(main())
