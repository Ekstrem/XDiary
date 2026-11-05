#!/usr/bin/env python3
"""check_adr_status.py — значения статусов ADR по закрытому словарю (DO-093, норма @LM).

Зачем: `tools/check_adr.py` проверяет структуру ADR, но **не проверяет значение статуса**.
Из-за этого в документе мог появиться статус вне словаря (случай `ADR-013`: `Заменено`),
и гейт этого не замечал — «проверено 0» при 33 пропущенных по грандфазерингу.

Словарь (закрытый, семь значений) и требования:
  `Черновик`             — в реестр `ADL/README.md` не попадает
  `Предложено`           — без требований
  `Принято`              — дата
  `Пересмотрен (дата)`   — дата + ссылка на ADR (включает бывший `Заменено`)
  `Отменён (дата)`       — дата + причина
  `Отклонено`            — причина
  `Отложено`             — причина + что разблокирует

Правила:
  1. ADR, созданные ПОСЛЕ канона (граница грандфазеринга), обязаны иметь статус из словаря;
  2. `Пересмотрен`/`Отменён` без даты, `Пересмотрен` без ссылки на ADR, `Отклонено`/`Отложено`
     без причины — нарушение;
  3. `Черновик` в реестре `ADL/README.md` — нарушение;
  4. старые ADR — `[grandfathered]`, `rc` не красят;
  5. `0 ADR` в проверке — `rc=2` «НЕ ИЗМЕРЕНО» (не «в норме»).

    python tools/check_adr_status.py
    python tools/check_adr_status.py --root . --canon 2026-11-08
    python tools/check_adr_status.py --selftest
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys

STATUS_LINE = re.compile(r'^\s*[-*]?\s*\*\*Статус:\*\*\s*(.+?)\s*$')
ADR_FILE = re.compile(r'^adr-(\d{3})\.md$')
DATE = re.compile(r'\d{4}-\d{2}-\d{2}')
ADR_REF = re.compile(r'ADR-?\s?(\d{3})', re.I)
# @AR 2026-11-05 20:40: граница = 2026-11-05 (с этой даты ADR проверяются,
# 33 более старых остаются историей и не нормализуются)
CANON_DEFAULT = '2026-11-05'

VOCAB = ('Черновик', 'Предложено', 'Принято', 'Пересмотрен', 'Отменён', 'Отклонено', 'Отложено')


def git(*a, cwd='.'):
    r = subprocess.run(['git', *a], cwd=cwd, capture_output=True, text=True,
                       encoding='utf-8', errors='replace')
    return r.stdout


def creation_date(path, root):
    """Дата создания файла по git (--diff-filter=A); fallback — mtime."""
    rel = os.path.relpath(path, root).replace(os.sep, '/')
    out = git('log', '--diff-filter=A', '--format=%cs', '--', rel, cwd=root).strip().splitlines()
    if out and DATE.search(out[-1]):
        return out[-1].strip()
    import datetime
    return datetime.date.fromtimestamp(os.path.getmtime(path)).isoformat()


def read_status(path):
    for line in open(path, encoding='utf-8', errors='replace').read().splitlines():
        m = STATUS_LINE.match(line)
        if m:
            return m.group(1).strip()
    return None


def classify(value):
    """(базовое значение словаря | None)."""
    if not value:
        return None
    cleaned = value.replace('*', '').replace('`', '').strip()
    for word in VOCAB:
        if cleaned.startswith(word) or cleaned.startswith(word.replace('ё', 'е')):
            return word
    return None


def adr_files(root):
    return sorted(glob.glob(os.path.join(root, '04-architecture', 'ADL', 'adr-*.md')))


def registry_path(root):
    return os.path.join(root, '04-architecture', 'ADL', 'README.md')


def scan(root, canon=CANON_DEFAULT):
    """(violations[(file, why)], checked, skipped)."""
    files = adr_files(root)
    if not files:
        return [], 0, 0

    violations, checked, skipped = [], 0, 0
    for path in files:
        name = os.path.basename(path)
        if not ADR_FILE.match(name):
            continue
        created = creation_date(path, root)
        status = read_status(path)
        rel = os.path.relpath(path, root).replace(os.sep, '/')

        if created < canon:
            skipped += 1
            continue
        checked += 1

        base = classify(status)
        if base is None:
            violations.append((rel, "статус вне словаря: %r" % (status or '— отсутствует')))
            continue
        low = (status or '').lower()
        if base in ('Принято', 'Пересмотрен', 'Отменён') and not DATE.search(status or ''):
            violations.append((rel, "%s без даты: %r" % (base, status)))
        if base == 'Пересмотрен' and not ADR_REF.search(status or ''):
            violations.append((rel, "Пересмотрен без ссылки на ADR: %r" % status))
        if base == 'Отменён' and len((status or '').split('—')) < 2:
            violations.append((rel, "Отменён без причины: %r" % status))
        if base in ('Отклонено', 'Отложено') and ('—' not in (status or '') and
                                                 ':' not in (status or '')):
            violations.append((rel, "%s без причины: %r" % (base, status)))
        if base == 'Отложено' and 'разблок' not in low:
            violations.append((rel, "Отложено без «что разблокирует»: %r" % status))

    reg = registry_path(root)
    if os.path.exists(reg):
        reg_text = open(reg, encoding='utf-8', errors='replace').read()
        for path in files:
            name = os.path.basename(path)
            m = ADR_FILE.match(name)
            if not m:
                continue
            if classify(read_status(path)) == 'Черновик' and name in reg_text:
                rel_reg = os.path.relpath(reg, root).replace(os.sep, '/')
                violations.append((rel_reg, "в реестре есть Черновик: %s" % name))
    return violations, checked, skipped


def selftest():
    ok = total = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-adr-status')
    shutil.rmtree(base, ignore_errors=True)

    def case(name, files, want_rc, canon=CANON_DEFAULT, late=None):
        nonlocal ok, total
        total += 1
        repo = os.path.join(base, name)
        os.makedirs(repo, exist_ok=True)
        for c in (('init', '-q'), ('config', 'user.email', 't@t'), ('config', 'user.name', 't')):
            subprocess.run(['git', *c], cwd=repo, capture_output=True)

        def write_all(items):
            for rel, content in items.items():
                full = os.path.join(repo, rel.replace('/', os.sep))
                os.makedirs(os.path.dirname(full), exist_ok=True)
                open(full, 'w', encoding='utf-8', newline='\n').write(content)

        write_all(files)
        subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
        env = dict(os.environ, GIT_COMMITTER_DATE='2026-11-10T10:00:00',
                   GIT_AUTHOR_DATE='2026-11-10T10:00:00')
        subprocess.run(['git', 'commit', '-qm', 'init'], cwd=repo, capture_output=True, env=env)
        if late:
            write_all(late)
            subprocess.run(['git', 'add', '-A'], cwd=repo, capture_output=True)
            env2 = dict(os.environ, GIT_COMMITTER_DATE='2026-11-12T10:00:00',
                        GIT_AUTHOR_DATE='2026-11-12T10:00:00')
            subprocess.run(['git', 'commit', '-qm', 'late'], cwd=repo, capture_output=True, env=env2)
        v, checked, _ = scan(repo, canon)
        rc = 2 if checked == 0 else (1 if v else 0)
        if rc == want_rc:
            ok += 1
        else:
            print('  [FAIL] %s: rc=%d, ждали %d | %s' % (name, rc, want_rc, v[:2]))

    A = '04-architecture/ADL/'
    GOOD = '- **Статус:** Принято (2026-11-10)\n'
    # (а) статус вне словаря -> rc=1
    case('a_outside', {A + 'adr-901.md': '- **Статус:** Заменено (2026-11-10)\n'}, 1)
    # (б) Пересмотрен без даты -> rc=1
    case('b_no_date', {A + 'adr-902.md': '- **Статус:** Пересмотрен — заменён ADR-023\n'}, 1)
    # (в) Пересмотрен без ссылки -> rc=1
    case('c_no_ref', {A + 'adr-903.md': '- **Статус:** Пересмотрен (2026-11-10)\n'}, 1)
    # (г) корректный словарь -> rc=0
    case('d_ok', {A + 'adr-904.md': GOOD}, 0)
    # (г2) корректный Пересмотрен с датой и ссылкой -> rc=0
    case('d_ok_revised', {A + 'adr-905.md':
                          '- **Статус:** Пересмотрен (2026-11-09) — заменён ADR-023\n'}, 0)
    # (д) Черновик в реестре -> rc=1
    case('e_draft_in_registry',
         {A + 'adr-906.md': '- **Статус:** Черновик\n',
          A + 'README.md': '| adr-906.md | черновик |\n'}, 1)
    # (д2) Черновик вне реестра -> rc=0
    case('e_draft_ok', {A + 'adr-907.md': '- **Статус:** Черновик\n',
                        A + 'README.md': '| — | — |\n'}, 0)
    # (е) 0 ADR -> rc=2
    case('f_empty', {A + 'README.md': '| — | — |\n'}, 2)
    # (ж) старый ADR вне словаря (коммит 11-10) + новый корректный (коммит 11-12):
    # старый grandfathered -> rc=0
    case('g_grandfathered', {A + 'adr-001.md': '- **Статус:** Заменено\n'}, 0,
         canon='2026-11-11', late={A + 'adr-908.md': GOOD})
    # (з) все ADR старше канона -> проверять нечего -> rc=2 (правило 5)
    case('h_all_old', {A + 'adr-001.md': '- **Статус:** Заменено\n'}, 2, canon='2099-01-01')

    shutil.rmtree(base, ignore_errors=True)
    print('check_adr_status --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', '--repo-root', dest='root', default='.')
    ap.add_argument('--canon', default=CANON_DEFAULT,
                    help='граница грандфазеринга (ADR созданные раньше не красят rc)')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    if not os.path.isdir(os.path.join(root, '04-architecture', 'ADL')):
        print('check_adr_status: НЕ ИЗМЕРЕНО — нет каталога 04-architecture/ADL в %s' % root)
        print('::error::check_adr_status: НЕ ИЗМЕРЕНО — каталог ADL не найден')
        return 2
    v, checked, skipped = scan(root, a.canon)
    for rel, why in v:
        print('::error::(adr-status) %s: %s' % (rel, why))
    print('check_adr_status: проверено ADR %d, старых пропущено %d, нарушений %d (канон %s)'
          % (checked, skipped, len(v), a.canon))
    if checked == 0:
        print('::error::check_adr_status: НЕ ИЗМЕРЕНО — 0 ADR после канона, проверять нечего')
        return 2
    return 1 if v else 0


if __name__ == '__main__':
    sys.exit(main())
