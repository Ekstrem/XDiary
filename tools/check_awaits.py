#!/usr/bin/env python3
"""check_awaits.py — «go» считается выданным только после подтверждения адресатом.

Правило (@TL 2026-11-14 03:10): решение/«go» действует, только если после метки выдачи
адресат либо **ответил** («принято»/«принял»/«делаю» в своём outbox), либо **сделал
артефакт** (коммит с префиксом роли, упоминающий ID/тему). Иначе решение «висит»:
две роли за сутки простояли цикл на неподтверждённом «go».

Источник: `5-tech-backlog/tl/AWAIT.md` — таблица. Ожидаемые колонки (по имени, порядок
не важен): «кому» (роль), «что» (тема/ID), «метка» (YYYY-MM-DD HH:mm).

    python tools/check_awaits.py              # предупреждения в вывод
    python tools/check_awaits.py --strict     # предупреждения = rc=1
    python tools/check_awaits.py --selftest
"""
import argparse
import os
import re
import subprocess
import sys

AWAIT = os.path.join("5-tech-backlog", "tl", "AWAIT.md")
ACCEPT = re.compile(r"(принято|принял|принимаю|делаю|взял|беру)", re.I)
STAMP = re.compile(r"(\d{4}-\d{2}-\d{2} \d{2}:\d{2})")
ROLE = re.compile(r"@?([a-z]{2})\b")
ROLE_PREFIX = {"ar": "ar:", "be": "be:", "do": "do:", "fe": "fe:", "te": "te:", "tl": "tl:"}


def parse_awaits(path):
    """[(role, what, stamp, line_no)] из markdown-таблицы."""
    rows = []
    if not os.path.exists(path):
        return rows
    header = None
    for n, line in enumerate(open(path, encoding="utf-8").read().splitlines(), 1):
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if header is None:
            header = [c.lower() for c in cells]
            continue
        if all(set(c) <= set("-: ") for c in cells if c):
            continue  # разделитель
        idx_role = idx_what = idx_stamp = None
        for i, h in enumerate(header):
            if idx_role is None and ("кому" in h or "адресат" in h or "роль" in h):
                idx_role = i
            elif idx_what is None and ("что" in h or "тема" in h or "задач" in h or "go" in h):
                idx_what = i
            elif idx_stamp is None and ("метк" in h or "когда" in h or "выдач" in h):
                idx_stamp = i
        if None in (idx_role, idx_what, idx_stamp) or len(cells) <= max(idx_role, idx_what, idx_stamp):
            continue
        m = STAMP.search(cells[idx_stamp])
        mr = ROLE.search(cells[idx_role].lower())
        if not m or not mr:
            continue
        rows.append((mr.group(1), cells[idx_what], m.group(1), n))
    return rows


def chat_accepts(root, role, after):
    """Есть ли в chat/<role>.md сообщение после `after` со словом принятия."""
    path = os.path.join(root, "5-tech-backlog", "chat", role + ".md")
    if not os.path.exists(path):
        return False
    for line in open(path, encoding="utf-8").read().splitlines():
        m = STAMP.search(line)
        if not m or m.group(1) <= after:
            continue
        if ACCEPT.search(line):
            return True
    return False


def commit_artifacts(root, role, topic):
    """Коммиты после… (упрощённо: любые) с префиксом роли и упоминанием темы."""
    prefix = ROLE_PREFIX.get(role)
    if not prefix:
        return False
    key = re.split(r"[^A-Za-z0-9_-]+", topic.strip())[0]
    if not key:
        return False
    out = subprocess.run(["git", "log", "--format=%s", "-n", "300"], cwd=root,
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    for subject in (out.stdout or "").splitlines():
        if subject.lower().startswith(prefix) and key.lower() in subject.lower():
            return True
    return False


def check(root, use_git=True):
    """use_git=False — только outbox (для selftest: иначе хвост реальной истории
    протекает в кейсы, и проверка проверяет не то)."""
    rows = parse_awaits(os.path.join(root, AWAIT))
    hanging = []
    for role, what, stamp, _ in rows:
        ok = chat_accepts(root, role, stamp)
        if not ok and use_git:
            ok = commit_artifacts(root, role, what)
        if not ok:
            hanging.append((role, what, stamp))
    return rows, hanging


def selftest():
    import shutil
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-awaits")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "tl"), exist_ok=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "chat"), exist_ok=True)

    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | ADR-028 перенос | 2026-11-13 19:00 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:30] → @TL: принял, делаю\n")
    rows, hanging = check(base, use_git=False)
    if len(rows) == 1 and not hanging:
        ok += 1
    else:
        print("  [FAIL] подтверждение не распознано: rows=%d hanging=%d" % (len(rows), len(hanging)))

    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 18:00] → @TL: принял (ДО метки — не считается)\n")
    rows, hanging = check(base, use_git=False)
    if len(hanging) == 1:
        ok += 1
    else:
        print("  [FAIL] сообщение до метки зачлось как подтверждение")

    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | ADR-028 перенос | 2026-11-13 19:00 |\n"
        "| @DO | гейт awaited | 2026-11-13 19:30 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "do.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:40] → @TL: принято, беру\n")
    rows, hanging = check(base, use_git=False)
    if len(rows) == 2 and len(hanging) == 1 and hanging[0][0] == "te":
        ok += 1
    else:
        print("  [FAIL] смешанный случай: rows=%d hanging=%s" % (len(rows), hanging))

    shutil.rmtree(base, ignore_errors=True)
    print("check_awaits --selftest: %d/3" % ok)
    return 0 if ok == 3 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    rows, hanging = check(root)
    if not rows:
        print("check_awaits: AWAIT.md нет или таблица пуста — висящих решений 0")
        return 0
    for role, what, stamp in hanging:
        print("::warning::висит: @%s — «%s» (выдано %s, подтверждения нет)" % (role, what, stamp))
    print("check_awaits: решений в AWAIT.md %d, висит %d" % (len(rows), len(hanging)))
    if a.strict and hanging:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
