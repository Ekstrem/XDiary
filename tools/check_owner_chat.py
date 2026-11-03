#!/usr/bin/env python3
"""check_owner_chat.py — проверка «строка владельца обязана оказаться в origin» (D-16).

Зачем: за сутки дважды строки владельца существовали только в рабочем дереве
(08:49/08:52/08:53 и 10:21), то есть «запись в файл ≠ запись в git»: при следующем
reset/checkout они исчезают. Плагин моста git-операции делать не должен (общее дерево,
rebase-состояния), поэтому проверка механическая и запускается раннером (`sweep`/`health`)
или агентом.

Проверки (все — по факту, без «помнить»):
  1. грязные файлы чата: `git status --porcelain 5-tech-backlog/chat/` непустой → warning;
  2. строки `ex.md`: каждая локальная строка должна присутствовать в
     `git show origin/main:5-tech-backlog/chat/ex.md`; отсутствует = дефект моста;
  3. SLA: если строка отсутствует в origin и её метка старше SLA минут — это ошибка (exit 1);
  4. §6: в несохранённых строках не должно быть 6+ цифр подряд (код) и телефонов
     `+7…`; такие строки в коммит не идут, пока не зачищены.

Запуск:
  python tools/check_owner_chat.py                 # проверка, exit 1 при ошибках
  python tools/check_owner_chat.py --sla 10        # SLA в минутах (по умолчанию 10)
  python tools/check_owner_chat.py --ref origin/main
"""
import argparse
import datetime
import os
import re
import subprocess
import sys

CHAT_DIR = "5-tech-backlog/chat"
OWNER = "ex.md"
STAMP = re.compile(r"^- \[(\d{4}-\d\d-\d\d \d\d:\d\d)\]")
CODE = re.compile(r"(?<!\d)\d{6,}(?!\d)")
PHONE = re.compile(r"\+\d{10,15}")


def run(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True,
                          encoding="utf-8", errors="replace")


def dirty_chat_files():
    out = run("status", "--porcelain", CHAT_DIR).stdout
    return [l for l in out.splitlines() if l.strip()]


def origin_text(ref, path):
    r = run("show", "%s:%s" % (ref, path))
    return r.stdout if r.returncode == 0 else ""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ref", default="origin/main")
    ap.add_argument("--sla", type=int, default=10, help="минут, после которых отсутствие в origin = дефект")
    ap.add_argument("--now", default=None, help="для тестов: 'YYYY-MM-DD HH:mm'")
    ap.add_argument("--all", action="store_true",
                    help="проверить ВСЕ файлы чата, а не только ex.md (D-16 повторился у @TL)")
    a = ap.parse_args()

    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    failures, warnings = [], []

    dirty = dirty_chat_files()
    if dirty:
        warnings.append("грязные файлы чата (%d): %s" % (len(dirty), ", ".join(d.split()[-1] for d in dirty[:6])))

    now = datetime.datetime.strptime(a.now, "%Y-%m-%d %H:%M") if a.now else datetime.datetime.now()

    if a.all:
        files = sorted(n for n in os.listdir(CHAT_DIR) if n.endswith(".md"))
    else:
        files = [OWNER]

    total_local = total_missing = 0
    for name in files:
        path = "%s/%s" % (CHAT_DIR, name)
        local = []
        try:
            with open(path, encoding="utf-8", errors="replace") as f:
                local = [l for l in f.read().splitlines() if STAMP.match(l)]
        except OSError:
            continue
        remote = {l for l in origin_text(a.ref, path).splitlines() if STAMP.match(l)}
        missing = [l for l in local if l not in remote]
        total_local += len(local)
        total_missing += len(missing)
        if missing:
            warnings.append("%s: строк локально %d, в %s нет %d" % (name, len(local), a.ref, len(missing)))
        for line in missing:
            m = STAMP.match(line)
            try:
                when = datetime.datetime.strptime(m.group(1), "%Y-%m-%d %H:%M")
            except ValueError:
                failures.append("%s: нечитаемая метка: %s" % (name, line[:60]))
                continue
            age = (now - when).total_seconds() / 60.0
            # часы чата идут впереди машинных — отрицательный возраст = «свежая», не ошибка
            if age > a.sla:
                failures.append("%s: строка отсутствует в %s и старше %d мин (%.0f): %s"
                                % (name, a.ref, a.sla, age, line[:80]))
            else:
                warnings.append("%s: строка ещё не в %s (%.0f мин, в пределах SLA): %s"
                                % (name, a.ref, age, line[:60]))

            if CODE.search(line) or PHONE.search(line):
                failures.append("%s: §6 — в несохранённой строке есть код/номер: %s"
                                % (name, line[:80]))

    print("check_owner_chat: проверено файлов %d, строк локально %d, отсутствуют в %s %d"
          % (len(files), total_local, a.ref, total_missing))
    for w in warnings:
        print("  ~ %s" % w)
    for f in failures:
        print("  ! %s" % f)

    if failures:
        print("\nИТОГ: %d ошибок (строка владельца не доехала в %s)" % (len(failures), a.ref))
        return 1
    print("\nИТОГ: строки владельца синхронизированы")
    return 0


if __name__ == "__main__":
    sys.exit(main())
