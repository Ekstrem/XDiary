#!/usr/bin/env python
"""DO-069: уведомления владельцу — по ПОЛНОМУ штампу, с дедупом и фильтром адресата.

Почему: до 2026-11-14 сопоставление шло по `HH:MM` без даты и файла. За сутки **29**
уведомлений «в чате X есть сообщение от роли (последнее HH:MM)», и каждое при проверке
давало **0 совпадений**: сообщение любого другого дня с теми же минутами выдавалось за
новое (карточка `DO-069`).

Что делает:
  1. читает `5-tech-backlog/chat/*.md` и берёт строки канона
     `- [YYYY-MM-DD HH:MM] → @<адресат>: текст`;
  2. сравнивает **дату и время целиком** (не `HH:MM`);
  3. дедуп: курсор хранит `файл → последний штамп, о котором уведомили`, повторный прогон
     на тех же данных даёт **ноль** новых;
  4. если адресат не `--to` (по умолчанию `@EX`, владелец) — не уведомляет вовсе.

Использование:
    python tools/notify_owner.py                 # напечатать новые уведомления (dry-run)
    python tools/notify_owner.py --commit        # сдвинуть курсор (после отправки)
    python tools/notify_owner.py --to @TL        # адресат фильтра
    python tools/notify_owner.py --selftest
"""
import argparse
import datetime
import glob
import json
import os
import re
import shutil
import sys
import tempfile

STAMP = re.compile(r"^- \[(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2})\] → (@[A-Za-z]+):")
CURSOR_DEFAULT = os.path.join(".git", "notify-owner-cursor.json")


def parse(path):
    """[(file, stamp_str, dt, addressee, line_no, text)]"""
    out = []
    with open(path, encoding="utf-8-sig", errors="replace") as handle:
        for n, line in enumerate(handle, 1):
            m = STAMP.match(line.strip())
            if not m:
                continue
            y, mo, d, h, mi, who = m.groups()
            try:
                dt = datetime.datetime(int(y), int(mo), int(d), int(h), int(mi))
            except ValueError:
                continue
            out.append((os.path.basename(path), "%s-%s-%s %s:%s" % (y, mo, d, h, mi), dt,
                        who.lower(), n, line.strip()))
    return out


def load_cursor(path):
    if not os.path.exists(path):
        return {}
    try:
        return json.load(open(path, encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def newer_than(stamp_str, cursor_stamp):
    """Строгое сравнение полных штампов; пустой курсор — пропускаем всё, что есть."""
    if not cursor_stamp:
        return True
    return datetime.datetime.strptime(stamp_str, "%Y-%m-%d %H:%M") > \
        datetime.datetime.strptime(cursor_stamp, "%Y-%m-%d %H:%M")


def scan(chat_dir, to="@ex", cursor=None):
    cursor = cursor or {}
    found = []
    for path in sorted(glob.glob(os.path.join(chat_dir, "*.md"))):
        name = os.path.basename(path)
        if name.startswith("_") or name.lower() == "readme.md":
            continue
        for fname, stamp, dt, who, line_no, text in parse(path):
            if who != to.lower():
                continue
            if not newer_than(stamp, cursor.get(fname)):
                continue
            found.append({"file": fname, "stamp": stamp, "line": line_no,
                          "text": text[:160]})
    found.sort(key=lambda x: (x["stamp"], x["file"], x["line"]))
    return found


def commit_cursor(found, cursor_path):
    cursor = load_cursor(cursor_path)
    for item in found:
        prev = cursor.get(item["file"])
        if not prev or newer_than(item["stamp"], prev):
            cursor[item["file"]] = item["stamp"]
    os.makedirs(os.path.dirname(cursor_path) or ".", exist_ok=True)
    json.dump(cursor, open(cursor_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return cursor


def write_chat(directory, name, lines):
    os.makedirs(directory, exist_ok=True)
    open(os.path.join(directory, name), "w", encoding="utf-8", newline="\n").write(
        "\n".join(lines) + "\n")


def selftest():
    ok = 0
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-notify")
    shutil.rmtree(root, ignore_errors=True)

    def case(n, files, cursor, to="@ex", expect_texts=None):
        d = os.path.join(root, "case%d" % n)
        shutil.rmtree(d, ignore_errors=True)
        for name, lines in files.items():
            write_chat(d, name, lines)
        got = scan(d, to, cursor)
        return got

    # 1) негативный контроль из карточки: сообщение 2026-11-03 19:39 не уведомляет сегодня
    got = case(1, {"tl.md": ["- [2026-11-03 19:39] → @EX: старое сообщение"]},
               {"tl.md": "2026-11-14 00:00"})
    if not got:
        ok += 1
    else:
        print("  [FAIL] старое сообщение дало уведомление: %s" % got)

    # 2) сегодняшнее сообщение адресату — ровно одно
    got = case(2, {"tl.md": ["- [2026-11-03 19:39] → @EX: старое",
                             "- [2026-11-14 18:12] → @EX: новое"]},
               {"tl.md": "2026-11-14 00:00"})
    if len(got) == 1 and got[0]["stamp"] == "2026-11-14 18:12":
        ok += 1
    else:
        print("  [FAIL] ждали одно новое, получили: %s" % got)

    # 3) дедуп: после сдвига курсора повторный прогон — ноль
    d3 = os.path.join(root, "case2")
    cursor_path = os.path.join(root, ".git", "cursor.json")
    commit_cursor(got, cursor_path)
    again = scan(d3, "@ex", load_cursor(cursor_path))
    if not again:
        ok += 1
    else:
        print("  [FAIL] повторный прогон дал %d уведомлений" % len(again))

    # 4) адресат не владелец — не уведомляем
    got = case(4, {"te.md": ["- [2026-11-14 20:00] → @TL: не владельцу"]}, {})
    if not got:
        ok += 1
    else:
        print("  [FAIL] уведомление о сообщении не владельцу: %s" % got)

    shutil.rmtree(root, ignore_errors=True)
    print("notify_owner --selftest: %d/4" % ok)
    return 0 if ok == 4 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--chat", default=os.path.join("5-tech-backlog", "chat"))
    ap.add_argument("--to", default="@EX")
    ap.add_argument("--cursor", default=CURSOR_DEFAULT)
    ap.add_argument("--commit", action="store_true", help="сдвинуть курсор после отправки")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    cursor = load_cursor(a.cursor)
    found = scan(a.chat, a.to, cursor)
    for item in found:
        print("::notice title=[%s] %s::%s… → %s:%d" % (a.to, item["stamp"],
                                                        item["text"][:80], item["file"], item["line"]))
    print("notify_owner: новых уведомлений %d (адресат %s, дедуп по полному штампу)"
          % (len(found), a.to))
    if a.commit and found:
        commit_cursor(found, a.cursor)
        print("курсор обновлён: %s" % a.cursor)
    return 0


if __name__ == "__main__":
    sys.exit(main())
