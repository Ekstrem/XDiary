#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_chat_shrink.py` — гейт «чат не усыхает» (инцидент @AR 2026-11-13 00:36).

Что случилось: бот коммитит `chat/*.md` из снимка, который может быть **старше** `main`;
тогда строки, добавленные ролями позже, молча исчезают, а коммит выглядит успешным.
Одним «sync» `3ba4c4b83` (00:34) пропало 67 сообщений: `ar` 41→24, `tl` 47→13, `te` 45→29,
`do` 41→26, `be` 4→2, `lm` 8→7. Ни `chat_lint` (формат цел), ни глаз в диффе этого не видит.

Метод (@AR 00:41): **монотонность по истории коммитов**, а не сравнение копий — бот пишет
усечённый снимок и в `origin`, и в рабочее дерево, поэтому «локальная копия = origin»
ничего не доказывает. Сравниваем число сообщений `- [YYYY-MM-DD HH:mm]` в родителе и в
коммите для каждого изменённого `chat/*.md`; уменьшение → `rc=1` с файлом, числами и
**перечнем пропавших меток**. Осознанный сброс чата (архив) — `--allow-shrink --reason`.

Коды: `0` — не усохло (или разрешено); `1` — усохло; `2` — не измерено (нет git/пустой диапазон).
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
from collections import Counter

MSG = re.compile(r'^- \[\d{4}-\d{2}-\d{2} \d{2}:\d{2}\]')
STAMP = re.compile(r'^- \[(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\].*')


def git(args, cwd):
    cp = subprocess.run(["git"] + args, cwd=cwd, capture_output=True, text=True,
                        encoding="utf-8", errors="replace")
    return cp.returncode, cp.stdout, cp.stderr


def count_msgs(text):
    return sum(1 for line in (text or "").splitlines() if MSG.match(line))


def stamps(text):
    """Метки сообщений файла: [`2026-11-13 00:20`, …] — для отчёта о потерях."""
    out = []
    for line in (text or "").splitlines():
        m = STAMP.match(line)
        if m:
            out.append(m.group(1) + " :: " + line[60:110].strip())
    return out


def missing_stamps(before, after):
    """Какие сообщения были в родителе и исчезли в коммите (мультимножество по тексту)."""
    b, a = Counter(stamps(before)), Counter(stamps(after))
    lost = []
    for key, n in b.items():
        lost.extend([key] * max(0, n - a.get(key, 0)))
    return sorted(lost)


def show(ref, path, cwd):
    rc, out, _ = git(["show", "%s:%s" % (ref, path)], cwd)
    return out if rc == 0 else None


def changed_chat_files(rng, cwd):
    rc, out, err = git(["diff", "--name-only", rng, "--", "5-tech-backlog/chat"], cwd)
    if rc != 0:
        raise OSError((err or "").strip()[:200])
    return [f for f in out.splitlines() if f.endswith(".md")]


def check(rev_from, rev_to, root, allow_shrink=False):
    files = changed_chat_files("%s..%s" % (rev_from, rev_to), root)
    findings, measured = [], 0
    for f in files:
        before = show(rev_from, f, root)
        after = show(rev_to, f, root)
        if after is None:
            continue
        b, a = count_msgs(before or ""), count_msgs(after or "")
        measured += 1
        if a < b:
            findings.append((f, b, a, b - a, missing_stamps(before or "", after or "")))
    if allow_shrink:
        findings = []      # осознанный сброс: фиксируем решение, но гейт не валим
    return findings, measured, files


def selftest():
    cases = []
    # temporary.Directory на Windows падает в rmtree (WinError 5) — каталог под cwd.
    base = os.path.join(os.getcwd(), ".tmp-chat-shrink-selftest")
    shutil.rmtree(base, ignore_errors=True)
    repo = os.path.join(base, "r")
    os.makedirs(os.path.join(repo, "5-tech-backlog", "chat"))
    chat = os.path.join(repo, "5-tech-backlog", "chat", "do.md")

    def w(text, msg=""):
        open(chat, "w", encoding="utf-8").write(text)
        git(["add", "-A"], repo)
        git(["-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", msg or "c"], repo)
        return git(["rev-parse", "HEAD"], repo)[1].strip()

    def msg(i):
        return "- [2026-11-13 00:%02d] → @TL: сообщение номер %d\n" % (i % 60, i)

    git(["init", "-q"], repo)
    c0 = w("".join(msg(i) for i in range(1, 6)), "five")
    six = w("".join(msg(i) for i in range(1, 7)), "six")

    got, measured, _ = check(c0, six, repo)
    cases.append(("рост 5 -> 6 не ловится", got == [] and measured == 1))

    shrunk = w("".join(msg(i) for i in range(1, 4)), "shrunk")
    got, measured, _ = check(six, shrunk, repo)
    cases.append(("усыхание 6 -> 3 поймано", len(got) == 1 and got[0][1] == 6 and got[0][2] == 3))
    cases.append(("названы пропавшие метки", bool(got) and len(got[0][4]) == 3))

    ok, _, _ = check(six, shrunk, repo, allow_shrink=True)
    cases.append(("усыхание разрешено флагом", ok == []))

    same = w("".join(msg(i) for i in range(1, 4)), "same")
    got, measured, _ = check(shrunk, same, repo)
    cases.append(("без изменений не ловится", got == []))

    touched = w("".join(msg(i) for i in range(1, 4)) + "заголовок\n", "cosmetic")
    got, _, files = check(same, touched, repo)
    cases.append(("правка заголовка без смены числа — 0 находок",
                  got == [] and len(files) == 1))

    shutil.rmtree(base, ignore_errors=True)
    passed = sum(1 for _, p in cases if p)
    for name, p in cases:
        print("  [%s] %s" % ("OK " if p else "FAIL", name))
    print("check_chat_shrink --selftest: %d/%d" % (passed, len(cases)))
    return 0 if passed == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="Гейт «чат не усыхает» (инцидент AR/DO 2026-11-13)")
    ap.add_argument("--range", default="HEAD~1..HEAD")
    ap.add_argument("--root", default=".")
    ap.add_argument("--allow-shrink", action="store_true",
                    help="разрешить осознанный сброс чата (архив)")
    ap.add_argument("--reason", default="", help="почему усыхание разрешено")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root or ".")
    if ".." not in a.range:
        print("::error::check_chat_shrink: --range должен быть A..B, получено %r" % a.range)
        return 2
    rev_from, rev_to = a.range.split("..", 1)
    try:
        findings, measured, files = check(rev_from, rev_to, root, a.allow_shrink)
    except OSError as ex:
        print("::error::check_chat_shrink: не измерено: %s" % ex)
        return 2

    if measured == 0:
        print("check_shrink: chat/*.md не менялись в %s — проверять нечего" % a.range)
        return 0

    if findings and not a.allow_shrink:
        for f, b, aft, delta, lost in findings:
            print("::error::(chat-shrink) %s: сообщений было %d, стало %d (−%d) — "
                  "бот закоммитил устаревший снимок или усыхание не объяснено"
                  % (f, b, aft, delta))
            if lost:
                shown = "; ".join(lost[:8]) + (" …" if len(lost) > 8 else "")
                print("    пропали сообщения (%d): %s" % (len(lost), shown))
        print("check_shrink: ПРОВАЛ — усохло файлов %d из %d" % (len(findings), measured))
        return 1

    if findings and a.allow_shrink:
        print("check_shrink: усыхание разрешено (%s), файлов: %d"
              % (a.reason or "без причины", len(findings)))
    print("check_shrink: ок — проверено файлов %d, усыханий %d"
          % (measured, 0 if a.allow_shrink else len(findings)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
