#!/usr/bin/env python3
"""Чат-инбокс TechLead: находит ВОПРОСЫ к @TL, на которые нет ответа в tl.md.

Запуск: python tools/chat_inbox.py
Код 1, если есть неотвеченные вопросы (гейт для мониторинга).

Формат сообщения: `- [YYYY-MM-DD HH:mm] → @КОМУ: текст` (см. AGENTS.md §3).
"""
import os
import re
import sys

CHAT = os.path.join("5-tech-backlog", "chat")
ROLES = ["be", "do", "fe", "te", "ar", "po"]

MSG = re.compile(r"^-\s*\[([^\]]+)\]\s*→\s*(.+?)\s*:\s*(.+)$")

# Признаки вопроса/просьбы к TL (чтобы не шуметь на подтверждениях/инструкциях).
QUESTION_MARKERS = (
    "?", "прошу", "скажи", "выбери", "выберите", "уточни", "подтверди",
    "можно ли", "какой вариант", "какой из", "как быть",
)


def parse(fn):
    out = []
    path = os.path.join(CHAT, fn)
    if not os.path.exists(path):
        return out
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = MSG.match(line.strip())
            if m:
                out.append({"t": m.group(1).strip(), "to": m.group(2).strip(), "text": m.group(3).strip()})
    return out


def is_question(text):
    t = text.lower()
    return any(k in t for k in QUESTION_MARKERS)


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    tl = parse("tl.md")
    unanswered = []
    for role in ROLES:
        tag = "@" + role.upper()
        for m in parse(role + ".md"):
            if not re.search(r"@TL\b", m["to"], re.I):
                continue
            if not is_question(m["text"]):
                continue
            # ответ TL этой роли (или @ALL) позже по времени?
            replied = any(
                (re.search(re.escape(tag) + r"\b", r["to"], re.I) or re.search(r"@ALL\b", r["to"], re.I))
                and r["t"] > m["t"]
                for r in tl
            )
            if not replied:
                unanswered.append((role.upper(), m["t"], m["text"]))

    if not unanswered:
        print("chat-inbox: неотвеченных вопросов к @TL нет")
        return 0

    print("chat-inbox: НЕ ОТВЕЧЕНО вопросов: %d" % len(unanswered))
    for role, t, text in sorted(unanswered, key=lambda x: x[1]):
        print("  [%s] %s -> @TL: %s" % (t, role, text[:200]))
    return 1


if __name__ == "__main__":
    sys.exit(main())
