#!/usr/bin/env python3
"""check_adr_refs.py — упомянутый статус ADR должен совпадать с фактическим.

Зачем (2026-11-05, три возврата одной ошибки): в `7-deploy/runbooks/release-gate.md` и
`5-tech-backlog/devops/DO-079.md` стояло `ADR-031 (Предложено)`, тогда как `adr-031.md`
уже был `Отклонено (2026-11-05)`. Класс: документ **цитирует статус**, которого нет;
читатель верит документу, а гейты — файлу. Проверка механическая: сравнить слово
статуса рядом с `ADR-NNN` с фактическим статусом файла.

Ловим только явные формы:
  `ADR-031` (`Предложено`)
  ADR-031 (**`Принято`**)
  ADR-026 (`Пересмотрен 2026-11-05` — действует)
Статус берём из строки `- **Статус:** …` файла `04-architecture/ADL/adr-NNN.md`.

    python tools/check_adr_refs.py
    python tools/check_adr_refs.py --selftest
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys

STATUS_LINE = re.compile(r'^\s*[-*]?\s*\*\*Статус:\*\*\s*(.+?)\s*$')
VOCAB = ('Черновик', 'Предложено', 'Принято', 'Пересмотрен', 'Отменён', 'Отклонено', 'Отложено')

# `ADR-031` (`Предложено`) | ADR-031 (**`Принято`**) | ADR-026 (`Пересмотрен 2026-11-05` — действует)
REF = re.compile(r'ADR\s?-?(\d{3})`?\s*[(\[«]\s*\**`?(' + '|'.join(VOCAB) + r')', re.UNICODE)

# @AR 2026-11-05 16:51: его зона открыта для чтения гейтом. Включается флагом
# `--include-arch`: на 2026-11-05 в его файлах есть 2 нарушения, и включать их
# в блокирующий шаг раньше правки значило бы красить main чужой зоной.
SCAN_DIRS = ('7-deploy', '5-tech-backlog/devops', 'tools')
ARCH_DIR = ('04-architecture',)


def adr_status_map(root):
    out = {}
    for path in glob.glob(os.path.join(root, '04-architecture', 'ADL', 'adr-*.md')):
        m = re.search(r'adr-(\d{3})\.md$', os.path.basename(path))
        if not m:
            continue
        for line in open(path, encoding='utf-8', errors='replace').read().splitlines():
            sm = STATUS_LINE.match(line)
            if sm:
                value = sm.group(1).replace('*', '').replace('`', '')
                out[m.group(1)] = next((w for w in VOCAB if value.startswith(w)), None)
                break
    return out


def scan(root, statuses=None, dirs=None):
    """([(файл, строка, adr, заявлено, факт)], проверено_ссылок)."""
    statuses = statuses if statuses is not None else adr_status_map(root)
    problems, refs = [], 0
    for d in (dirs or SCAN_DIRS):
        base = os.path.join(root, d)
        if not os.path.isdir(base):
            continue
        for path in glob.glob(os.path.join(base, '**', '*.md'), recursive=True):
            rel = os.path.relpath(path, root).replace(os.sep, '/')
            for no, line in enumerate(open(path, encoding='utf-8', errors='replace'), 1):
                for m in REF.finditer(line):
                    adr, claimed = m.group(1), m.group(2)
                    fact = statuses.get(adr)
                    if fact is None:
                        continue
                    refs += 1
                    if fact != claimed:
                        problems.append((rel, no, adr, claimed, fact))
    return problems, refs


def adr_status_raw(root):
    """номер -> сырой текст статуса (нужен, чтобы отличить «заменено ADR-NNN» от «отклонено»)."""
    out = {}
    for path in glob.glob(os.path.join(root, '04-architecture', 'ADL', 'adr-*.md')):
        m = re.search(r'adr-(\d{3})\.md$', os.path.basename(path))
        if not m:
            continue
        for line in open(path, encoding='utf-8', errors='replace').read().splitlines():
            sm = STATUS_LINE.match(line)
            if sm:
                out[m.group(1)] = sm.group(1).replace('*', '').replace('`', '')
                break
    return out


def accepted_refs_rejected(root):
    """Активный ADR ссылается на снятый: «заменён» -> устаревшая ссылка (stale),
    «отклонён/отменён без замены» -> опора на снятое решение (error)."""
    statuses = adr_status_map(root)
    raw = adr_status_raw(root)
    problems, stale = [], []
    for path in glob.glob(os.path.join(root, '04-architecture', 'ADL', 'adr-*.md')):
        m = re.search(r'adr-(\d{3})\.md$', os.path.basename(path))
        if not m:
            continue
        src = m.group(1)
        if statuses.get(src) not in ('Принято', 'Пересмотрен'):
            continue
        all_lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
        for no, line in enumerate(all_lines, 1):
            # @TL 2026-11-13 01:04: пометка «отклонён/снят» может стоять на СЛЕДУЮЩЕЙ строке
            # (в adr-005.md ссылка на `ADR-029` на :21, а «отклонён» — на :22). Смотрим окно.
            window = line + " " + (all_lines[no] if no < len(all_lines) else "")
            for ref in re.finditer(r'ADR\s?-?(\d{3})', line):
                tgt = ref.group(1)
                if tgt == src:
                    continue
                if statuses.get(tgt) not in ('Отклонено', 'Отменён'):
                    continue
                rel = os.path.relpath(path, root).replace(os.sep, '/')
                replaced = 'замен' in (raw.get(tgt) or '').lower()
                # @TL (дефект-плечо @AR 00:59): ссылка — ОПОРА только если рядом нет пометки,
                # что решение снято. «ADR-029 отклонён» — упоминание, а не опора.
                marker = re.search(r'отклон[её]н|отмен[её]н|снят|не действует', window, re.I)
                if replaced or marker:
                    stale.append((rel, no, src, tgt, statuses[tgt]))
                else:
                    problems.append((rel, no, src, tgt, statuses[tgt]))
    return problems, stale


def selftest():
    ok = total = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-adr-refs')
    shutil.rmtree(base, ignore_errors=True)

    def case(label, adr_status, doc, want):
        nonlocal ok, total
        total += 1
        repo = os.path.join(base, label)
        os.makedirs(os.path.join(repo, '04-architecture', 'ADL'), exist_ok=True)
        os.makedirs(os.path.join(repo, '7-deploy', 'runbooks'), exist_ok=True)
        open(os.path.join(repo, '04-architecture', 'ADL', 'adr-901.md'),
             'w', encoding='utf-8', newline='\n').write('- **Статус:** %s\n' % adr_status)
        open(os.path.join(repo, '7-deploy', 'runbooks', 'r.md'),
             'w', encoding='utf-8', newline='\n').write(doc + '\n')
        problems, _ = scan(repo)
        if len(problems) == want:
            ok += 1
        else:
            print('  [FAIL] %s: проблем %d, ждали %d | %s'
                  % (label, len(problems), want, problems[:2]))

    case('d1_mismatch', 'Отклонено (2026-11-05)',
         'канон: `ADR-901` (`Предложено` — уточнения)', 1)
    case('d2_match', 'Предложено',
         'канон: `ADR-901` (`Предложено` — уточнения)', 0)
    case('d3_revised', 'Пересмотрен (2026-11-05) — ...',
         'канон: `ADR-901` (`Пересмотрен 2026-11-05` — действует)', 0)
    case('d4_other_value', 'Пересмотрен (2026-11-05)',
         'канон: `ADR-901` (`Принято`)', 1)

    # текстовый кейс (норма @TL): в сообщении должны быть claimed и факт
    problems, _ = scan(os.path.join(base, 'd1_mismatch'))
    total += 1
    if problems and problems[0][3] == 'Предложено' and problems[0][4] == 'Отклонено':
        ok += 1
    else:
        print('  [FAIL] в результате нет пары заявлено/факт: %s' % (problems[:1],))

    shutil.rmtree(base, ignore_errors=True)
    print('check_adr_refs --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', '--repo-root', dest='root', default='.')
    ap.add_argument('--include-arch', action='store_true',
                    help='сканировать и 04-architecture (зона @AR, открыта для чтения гейтом)')
    ap.add_argument('--selftest', action='store_true')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    if not os.path.isdir(os.path.join(root, '04-architecture', 'ADL')):
        print('check_adr_refs: НЕ ИЗМЕРЕНО — нет каталога ADL в %s' % root)
        return 2

    dirs = SCAN_DIRS + ARCH_DIR if a.include_arch else SCAN_DIRS
    problems, refs = scan(root, dirs=dirs)
    link_problems, stale_links = (accepted_refs_rejected(root)
                                   if a.include_arch else ([], []))
    print('check_adr_refs: каталоги: %s' % ', '.join(dirs))
    for rel, no, adr, claimed, fact in problems:
        print('::error::(adr-refs) %s:%d — `ADR-%s` заявлен как `%s`, фактически `%s`'
              % (rel, no, adr, claimed, fact))
    for rel, no, src, tgt, fact in link_problems:
        print('::error::(adr-refs) %s:%d — активный ADR-%s ссылается на `ADR-%s` (фактически `%s`)'
              % (rel, no, src, tgt, fact))
    for rel, no, src, tgt, fact in stale_links:
        print('::warning::(adr-refs) %s:%d — ADR-%s ссылается на заменённое (`ADR-%s`), '
              'актуальная ссылка — на решение-замену' % (rel, no, src, tgt))
    print('check_adr_refs: проверено ссылок %d, нарушений %d | связность: опора на снятое %d, '
          'устаревших ссылок на заменённое %d'
          % (refs, len(problems), len(link_problems), len(stale_links)))
    if refs == 0:
        print('::error::check_adr_refs: НЕ ИЗМЕРЕНО — ни одной ссылки со статусом не найдено')
        return 2
    return 1 if (problems or link_problems) else 0


if __name__ == '__main__':
    sys.exit(main())
