#!/usr/bin/env python3
"""Таблица статусов задач для README.

Источник истины — поле `**Статус:**` в `5-tech-backlog/<роль>/*.md`.
Допустимые значения: Открыта, В работе, На проверке, Закрыта, Отклонена
(«Выполнена»/«Готово»/«Done» считаются синонимами «Закрыта» — но так писать не надо).

Использование:
    python tools/stats.py            # напечатать таблицу
    python tools/stats.py --write    # обновить блок в README.md между маркерами
    python tools/stats.py --check    # код 1, если README отстал от задач (для CI)
"""
import argparse
import os
import re
import sys

TASKS = "5-tech-backlog"
README = "README.md"
START = "<!-- stats:start -->"
END = "<!-- stats:end -->"

ROLE_ORDER = [
    ("back-end", "back-end"),
    ("devops", "devops"),
    ("front-end", "front-end"),
    ("tester", "tester"),
    ("arch", "arch"),
]
COLUMNS = ["Открыта", "В работе", "На проверке", "Закрыта", "Отклонена"]

STATUS_RE = re.compile(r"Статус:\*\*\s*(.+)")


def normalize(raw):
    s = raw.strip().lower()
    if "отклон" in s:
        return "Отклонена"
    if "закрыт" in s or "выполн" in s or "готов" in s:
        return "Закрыта"
    if "на проверке" in s:
        return "На проверке"
    if "в работе" in s:
        return "В работе"
    return "Открыта"


def task_status(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = STATUS_RE.search(line)
            if m:
                return normalize(m.group(1))
    return "Открыта"


def collect():
    rows = []
    totals = {c: 0 for c in COLUMNS}
    for folder, title in ROLE_ORDER:
        base = os.path.join(TASKS, folder)
        if not os.path.isdir(base):
            continue
        counts = {c: 0 for c in COLUMNS}
        total = 0
        for fn in sorted(os.listdir(base)):
            if not fn.endswith(".md") or fn == "README.md":
                continue
            total += 1
            counts[task_status(os.path.join(base, fn))] += 1
        rows.append((title, total, counts))
        for c in COLUMNS:
            totals[c] += counts[c]
    return rows, totals


def render(rows, totals):
    head = "| Роль | Всего | " + " | ".join(COLUMNS) + " |"
    sep = "|---" * (len(COLUMNS) + 2) + "|"
    out = [head, sep]
    for title, total, counts in rows:
        out.append("| %s | %d | %s |" % (title, total, " | ".join(str(counts[c]) for c in COLUMNS)))
    grand = sum(total for _, total, _ in rows)
    out.append("| **Итого** | **%d** | %s |"
               % (grand, " | ".join("**%d**" % totals[c] for c in COLUMNS)))
    return out


def block(lines):
    return "\n".join([START, *lines, END])


def current_block():
    with open(README, encoding="utf-8", newline="") as f:
        text = f.read().replace("\r\n", "\n")
    if START not in text or END not in text:
        sys.exit("README.md: нет маркеров %s ... %s" % (START, END))
    body = text.split(START, 1)[1].split(END, 1)[0].strip("\n")
    return text, body


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="обновить блок в README.md")
    ap.add_argument("--check", action="store_true", help="проверить, что README актуален")
    a = ap.parse_args()

    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    rows, totals = collect()
    table = render(rows, totals)
    text, body = current_block()

    if a.check:
        if body != "\n".join(table):
            print("README.md устарел: статусы задач изменились.")
            print("Починить: python tools/stats.py --write")
            print("")
            print("Сейчас в задачах:")
            print("\n".join(table))
            return 1
        print("stats: README актуален")
        return 0

    if a.write:
        new = text.split(START, 1)[0] + block(table) + text.split(END, 1)[1]
        with open(README, "w", encoding="utf-8", newline="\n") as f:
            f.write(new)
        print("stats: README.md обновлён")
        print("\n".join(table))
        return 0

    print("\n".join(table))
    return 0


if __name__ == "__main__":
    sys.exit(main())
