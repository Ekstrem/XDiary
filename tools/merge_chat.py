#!/usr/bin/env python3
"""Собрать диалог ролей: chat/*.md (outbox) -> chat/_dialogue.md.

Outbox-файлы — источник правды (каждая роль пишет только в свой файл).
Этот скрипт лишь читает их и складывает все сообщения в один файл,
отсортированный по времени. Запускается GitHub Action'ом.
"""
import os
import re
import sys

CHAT_DIR = os.path.join("5-tech-backlog", "chat")
OUT = os.path.join(CHAT_DIR, "_dialogue.md")

ROLE_NAMES = {
    "be": "Back-end",
    "do": "DevOps",
    "fe": "Front-end",
    "te": "Tester",
    "ar": "Architect",
    "tl": "TechLead",
    "po": "PO",
}
SKIP = {"README.md", "_dialogue.md"}

# - [2026-10-30 15:20] → @FE: текст
MSG = re.compile(r"^-\s*\[([^\]]+)\]\s*→\s*@([^\s:]+)\s*:\s*(.+)$")


def main():
    if not os.path.isdir(CHAT_DIR):
        print("нет папки %s" % CHAT_DIR, file=sys.stderr)
        return
    messages = []
    for fn in sorted(os.listdir(CHAT_DIR)):
        if not fn.endswith(".md") or fn in SKIP:
            continue
        role = fn[:-3].lower()
        src = ROLE_NAMES.get(role, role.upper())
        with open(os.path.join(CHAT_DIR, fn), encoding="utf-8") as f:
            for line in f:
                m = MSG.match(line.strip())
                if m:
                    ts, to, text = m.group(1).strip(), m.group(2).strip(), m.group(3).strip()
                    messages.append((ts, src, to, text))
    messages.sort(key=lambda x: x[0])

    lines = [
        "# Диалог ролей (авто-сводка)",
        "",
        "> Генерируется из `chat/*.md` (outbox). **Не редактировать вручную** — "
        "правки перезапишутся. Пиши в файл своей роли.",
        "",
        "| Время | От | Кому | Сообщение |",
        "|---|---|---|---|",
    ]
    for ts, src, to, text in messages:
        lines.append("| %s | %s | @%s | %s |" % (ts, src, to, text.replace("|", "\\|")))
    if not messages:
        lines.append("| — | — | — | Сообщений пока нет |")

    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("merged %d messages -> %s" % (len(messages), OUT))


if __name__ == "__main__":
    main()
