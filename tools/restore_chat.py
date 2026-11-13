#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`restore_chat.py` — восстановление outbox роли после «усыхания» ботом.

Инциденты 2026-11-13: бот дважды закоммитил устаревший снимок `chat/*.md` и стёр
сообщения (первый раз −81, второй −546: `tl` 250→32, `ar` 162→34, `te` 142→33,
`do` 119→35, `fe` 19→10). Каждая роль восстанавливала вручную: взять полный снимок,
объединить с текущим, дедуп по тексту, сортировка по метке.

Этот инструмент делает ту же операцию одной командой и **ничего не теряет**:
строки-сообщения (`- [YYYY-MM-DD HH:mm]`) объединяются как мультимножество
(дедуп по нормализованному тексту), заголовок берётся из текущего файла,
порядок — по метке. Идемпотентен: повторный прогон не меняет файл.

Коды: `0` — восстановлено (или нечего); `2` — не измерено (нет файла/ревизии).
"""
import argparse
import os
import re
import shutil
import subprocess
import sys

MSG = re.compile(r'^- \[\d{4}-\d{2}-\d{2} \d{2}:\d{2}\]')
STAMP = re.compile(r'^- \[(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\]')


def git(args, cwd):
    return subprocess.run(["git"] + args, cwd=cwd, capture_output=True, text=True,
                          encoding="utf-8", errors="replace")


def show(ref, path, cwd):
    cp = git(["show", "%s:%s" % (ref, path)], cwd)
    return cp.stdout if cp.returncode == 0 else None


def split(text):
    head, msgs = [], []
    for line in (text or "").splitlines():
        if MSG.match(line):
            msgs.append(line)
        elif not msgs and line.strip():
            head.append(line)
    return head, msgs


def norm(line):
    return re.sub(r"\s+", " ", line.strip())


def stamp(line):
    m = STAMP.match(line)
    return m.group(1) if m else ""


def merge_texts(texts):
    """[текст] -> (заголовок, отсортированные уникальные сообщения)."""
    head, seen, merged = None, set(), []
    for text in texts:
        h, msgs = split(text)
        if head is None and h:
            head = h
        for line in msgs:
            key = norm(line)
            if key in seen:
                continue
            seen.add(key)
            merged.append(line)
    merged.sort(key=lambda l: (stamp(l), l))
    return head or [], merged


def restore(path, rev, root, dry_run=False):
    cur_path = os.path.join(root, path.replace('/', os.sep))
    if not os.path.exists(cur_path):
        print("::error::restore_chat: файла нет в рабочем дереве: %s" % path)
        return 2
    cur = open(cur_path, encoding="utf-8").read()
    old = show(rev, path, root)
    if old is None:
        print("::error::restore_chat: в ревизии %s нет файла %s" % (rev, path))
        return 2

    head, merged = merge_texts([old, cur])
    new = "\n".join(head).rstrip("\n") + "\n" + "\n".join(merged) + "\n"
    before, after = len(split(cur)[1]), len(merged)
    print("restore_chat: %s — сейчас %d, после объединения %d (+%d)"
          % (path, before, after, after - before))

    if after < before:
        print("::error::restore_chat: объединение дало МЕНЬШЕ, чем было (%d < %d) — не пишу"
              % (after, before))
        return 2
    if dry_run:
        print("--dry-run: файл не изменён")
        return 0
    if new == cur:
        print("файл уже полон — изменений нет")
        return 0
    open(cur_path, "w", encoding="utf-8", newline="\n").write(new)
    print("записано: %d строк (было %d)" % (len(new.splitlines()), len(cur.splitlines())))
    return 0


def selftest():
    base = os.path.join(os.getcwd(), ".tmp-restore-chat-selftest")
    shutil.rmtree(base, ignore_errors=True)
    repo = os.path.join(base, "r")
    os.makedirs(os.path.join(repo, "5-tech-backlog", "chat"))

    def w(text):
        with open(os.path.join(repo, "5-tech-backlog", "chat", "te.md"), "w",
                  encoding="utf-8", newline="\n") as f:
            f.write(text)
        git(["add", "-A"], repo)
        git(["-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "c"], repo)
        return git(["rev-parse", "HEAD"], repo).stdout.strip()

    def msg(i):
        return "- [2026-11-13 03:%02d] → @TL: сообщение %d\n" % (i, i)

    git(["init", "-q"], repo)
    head = "# CHAT outbox — TestEngineer\n\n_Архив: `archive/te.md`._\n"
    full = w(head + "".join(msg(i) for i in range(1, 8)))
    # «усыхание»: остались только два сообщения
    w(head + msg(1) + msg(2))
    path = "5-tech-backlog/chat/te.md"
    rc = restore(path, full, repo)
    got = open(os.path.join(repo, "5-tech-backlog", "chat", "te.md"),
               encoding="utf-8").read()
    cases = [
        ("rc=0", rc == 0),
        ("вернулись все 7", len(split(got)[1]) == 7),
        ("заголовок сохранён", "CHAT outbox" in got),
        ("порядок по метке", [stamp(l) for l in split(got)[1]] == sorted(stamp(l) for l in split(got)[1])),
    ]
    # идемпотентность
    rc2 = restore(path, full, repo)
    got2 = open(os.path.join(repo, "5-tech-backlog", "chat", "te.md"),
                encoding="utf-8").read()
    cases.append(("повторный прогон идемпотентен", rc2 == 0 and got2 == got))
    # дубль не добавляется
    cases.append(("дедуп: 7, не 14", len(split(got2)[1]) == 7))

    shutil.rmtree(base, ignore_errors=True)
    passed = sum(1 for _, ok in cases if ok)
    for name, ok in cases:
        print("  [%s] %s" % ("OK " if ok else "FAIL", name))
    print("restore_chat --selftest: %d/%d" % (passed, len(cases)))
    return 0 if passed == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="Восстановление chat/<роль>.md после усыхания")
    ap.add_argument("--file", help="путь вида 5-tech-backlog/chat/te.md")
    ap.add_argument("--from", dest="rev", help="ревизия с полным снимком (напр. c5764163f)")
    ap.add_argument("--root", default=".")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if not a.file or not a.rev:
        print("::error::restore_chat: нужны --file и --from")
        return 2
    return restore(a.file, a.rev, os.path.abspath(a.root), a.dry_run)


if __name__ == "__main__":
    sys.exit(main())
