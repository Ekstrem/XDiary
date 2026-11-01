#!/usr/bin/env python3
"""CI-проверка формата чата: ни одно сообщение не должно «молча» теряться.

Читает все outbox'ы `chat/*.md`, находит строки вида `- [YYYY-MM-DD ...]`
и проверяет:
  1. строка парсится единым регексом (как в merge_chat.py);
  2. адресаты — известные роли (be/do/fe/te/ar/tl/po), поддержан мультиадресат `@A + @B`.

Выход: 0 — всё ок, 1 — есть ошибки формата (CI падает), 2 — нет папки чата.
"""
import os
import re
import sys

CHAT_DIR = os.path.join("5-tech-backlog", "chat")
SKIP = {"README.md", "_dialogue.md"}
ROLES = {"be", "do", "fe", "te", "ar", "tl", "po", "all"}

# Тот же контракт, что и merge_chat.py (мультиадресат поддерживается).
MSG = re.compile(r"^-\s*\[([^\]]+)\]\s*→\s*@(.+?)\s*:\s*(.+)$")
# Строка, похожая на чат-сообщение (начинается с даты в скобках).
CHAT_LIKE = re.compile(r"^-\s*\[\d{4}-\d{2}-\d{2}")


def main():
    if not os.path.isdir(CHAT_DIR):
        print("нет папки %s" % CHAT_DIR, file=sys.stderr)
        return 2
    errors = []
    total = 0
    for fn in sorted(os.listdir(CHAT_DIR)):
        if not fn.endswith(".md") or fn in SKIP:
            continue
        path = os.path.join(CHAT_DIR, fn)
        with open(path, encoding="utf-8") as f:
            for lineno, line in enumerate(f, 1):
                s = line.strip()
                if not CHAT_LIKE.match(s):
                    continue  # не чат-сообщение (заголовок, чекбокс и т.п.)
                total += 1
                m = MSG.match(s)
                if not m:
                    errors.append("%s:%d не парсится: %s" % (fn, lineno, s[:100]))
                    continue
                addrs = re.findall(r"@?([a-zA-Z]+)", m.group(2))
                if not addrs:
                    errors.append("%s:%d нет адресата: %s" % (fn, lineno, s[:100]))
                    continue
                for a in addrs:
                    if a.lower() not in ROLES:
                        errors.append("%s:%d неизвестная роль @%s: %s" % (fn, lineno, a, s[:100]))

    if errors:
        print("ошибок формата чата: %d" % len(errors), file=sys.stderr)
        for e in errors:
            print("  " + e, file=sys.stderr)
        return 1
    print("чат OK: %d сообщений, все парсятся, роли известны" % total)
    return 0


if __name__ == "__main__":
    sys.exit(main())
