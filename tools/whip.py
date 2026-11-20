#!/usr/bin/env python3
"""whip.py — «Кнут» (whip): детектор простоя ролей проекта.

Каждые interval_min минут (по планировщику) смотрит на outbox'ы ролей проекта
и, если доля простаивающих ролей >= idle_ratio, пишет пинг в чат координатору
(@TL), чтобы тот разморозил работу.

«Простаивает» = роль не писала в свой outbox дольше idle_minutes (по умолчанию
равно интервалу: не писал с прошлой проверки). TL из подсчёта исключён — он
адресат, а не «работающая» роль.

Конфиг — loop.config.json, секция "whip" (все поля необязательны, есть defaults):
    project       : "XDiary"    — имя проекта (метка в сообщении)
    idle_minutes  : 15          — роль «простаивает», если не писала дольше
    idle_ratio    : 0.8         — доля простаивающих (0.8 = 80%)
    interval_min  : 15          — период (справочно; cron ставит сам)
    chat_dir      : "5-tech-backlog/chat"
    roles         : ["be","do","fe","te","ar","po","lm"]  — кого считаем
    from_chat     : "5-tech-backlog/chat/lm.md"           — куда писать пинг
    to            : "@TL"       — адресат пинга

Запуск:
    python tools/whip.py --config loop.config.json
    python tools/whip.py --config loop.config.json --dry-run
    python tools/whip.py --selftest
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys

STAMP = re.compile(r"^-\s*\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\]")

DEFAULTS = {
    "project": "XDiary",
    "idle_minutes": 30,
    "idle_ratio": 0.8,
    "interval_min": 15,
    "chat_dir": "5-tech-backlog/chat",
    "roles": ["be", "do", "fe", "te", "ar", "po", "lm", "dr"],
    "from_chat": "5-tech-backlog/chat/lm.md",
    "to": "@TL + @EX",
    # Экспоненциальная задержка (владелец 2026-11-20, подписочная модель): первый пинг
    # через idle_minutes, каждый следующий (пока команда стоит) — вдвое реже: 30 → 60 → 120 …
    # Кнут не должен сам жечь лимит подписки каскадом пингов.
    "backoff_minutes": 30,
    "streak_window_hours": 6,
}


def load_config(path):
    cfg = dict(DEFAULTS)
    cfg["tz_offset_hours"] = 3
    if path and os.path.exists(path):
        with open(path, encoding="utf-8-sig") as f:
            data = json.load(f)
        wh = data.get("whip") or {}
        for k in DEFAULTS:
            if k in wh:
                cfg[k] = wh[k]
        ct = data.get("chat_time") or {}
        cfg["tz_offset_hours"] = int(ct.get("tz_offset_hours", 3))
    return cfg


def last_stamp(path):
    """Последняя валидная метка в outbox (или None, если нет)."""
    best = None
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            m = STAMP.match(line.strip())
            if not m:
                continue
            try:
                ts = datetime.datetime.strptime(m.group(1), "%Y-%m-%d %H:%M")
            except ValueError:
                continue
            if best is None or ts > best:
                best = ts
    return best


def last_commit_ts(path):
    """Время последнего коммита файла (реальные машинные часы, не шкала чата)."""
    try:
        out = subprocess.run(["git", "log", "-1", "--format=%ct", "--", path],
                             capture_output=True, text=True).stdout.strip()
        if out.isdigit():
            return datetime.datetime.fromtimestamp(int(out))
    except Exception:
        pass
    return None

def now_project(cfg):
    return (datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)
            + datetime.timedelta(hours=cfg["tz_offset_hours"]))


def idle_roles(cfg, now):
    idle, active = [], []
    for role in cfg["roles"]:
        last = last_commit_ts(os.path.join(cfg["chat_dir"], role + ".md"))
        if last is None or (now - last) > datetime.timedelta(minutes=cfg["idle_minutes"]):
            idle.append((role, last))
        else:
            active.append((role, last))
    return idle, active


def ping_history(chat_file, now, window_hours):
    """(время последнего пинга, серия пингов за окно) по файлу чата кнута.

    Серия — число строк «команда стоит» за последние `window_hours` часов; по ней
    считается экспоненциальная задержка (30 → 60 → 120 …), чтобы кнут не жег лимит
    подписки каскадом пингов, пока команда стоит.
    """
    last = None
    streak = 0
    if not os.path.exists(chat_file):
        return None, 0
    with open(chat_file, encoding="utf-8-sig") as f:
        for line in f:
            if "команда стоит" not in line:
                continue
            m = STAMP.match(line.strip())
            if not m:
                continue
            try:
                ts = datetime.datetime.strptime(m.group(1), "%Y-%m-%d %H:%M")
            except ValueError:
                continue
            last = ts if last is None or ts > last else last
            if (now - ts) < datetime.timedelta(hours=window_hours):
                streak += 1
    return last, streak


def backoff_due(cfg, last, streak, now):
    """Пинг разрешён, если с последнего пинга прошло >= backoff * 2**серия."""
    if last is None:
        return True
    wait = datetime.timedelta(minutes=cfg["backoff_minutes"] * (2 ** streak))
    return (now - last) >= wait


def ping_message(cfg, now, idle, active, streak=0):
    total = len(idle) + len(active)
    names = " " + ", ".join("@" + r.upper() for r, _ in idle) if idle else ""
    esc = " 🔁 повторный простой (серия %d)" % streak if streak > 0 else ""
    return ("- [%s] → %s: ⚠️ команда стоит: %d/%d ролей простаивают > %d мин.%s.%s "
            "Следи за правилами и организуй команду."
            % (now.strftime("%Y-%m-%d %H:%M"), cfg["to"],
               len(idle), total, cfg["idle_minutes"], names, esc))


def selftest():
    ok = True
    cfg = load_config(None)
    ok &= cfg["project"] == "XDiary" and cfg["idle_ratio"] == 0.8
    ok &= cfg["idle_minutes"] == 30 and cfg["interval_min"] == 15
    ok &= cfg["backoff_minutes"] == 30
    # last_commit_ts на реальном git-файле (сам whip.py) — не None
    ok &= last_commit_ts("tools/whip.py") is not None
    # ping_message: 1/2 = 50% (< 80%), но текст несёт «команда стоит» + адресата + счёт
    msg = ping_message(cfg, datetime.datetime(2026, 11, 4, 10, 0), [("be", None)], [("do", None)])
    ok &= "команда стоит" in msg and "организуй команду" in msg and "1/2" in msg
    ok &= "→ @TL + @EX:" in msg
    # повторный простой: серия 1 добавляет маркер
    msg2 = ping_message(cfg, datetime.datetime(2026, 11, 4, 10, 0), [("be", None)], [("do", None)], streak=1)
    ok &= "повторный простой (серия 1)" in msg2
    # backoff: свежий пинг при серии 0 -> ещё рано; старый -> пора
    now = datetime.datetime(2026, 11, 4, 12, 0)
    ok &= not backoff_due(cfg, datetime.datetime(2026, 11, 4, 11, 50), 0, now)   # 10 мин < 30
    ok &= backoff_due(cfg, datetime.datetime(2026, 11, 4, 11, 0), 0, now)        # 60 мин > 30
    ok &= not backoff_due(cfg, datetime.datetime(2026, 11, 4, 11, 30), 1, now)   # 30 мин < 60 (серия 1)
    ok &= backoff_due(cfg, datetime.datetime(2026, 11, 3, 0, 0), 1, now)         # далеко -> пора
    print("selftest:", "OK" if ok else "FAIL")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description="Кнут (whip) — детектор простоя ролей")
    ap.add_argument("--config", default=None)
    ap.add_argument("--dry-run", action="store_true", help="не писать, только показать")
    ap.add_argument("--selftest", action="store_true", help="самопроверка")
    args = ap.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if args.selftest:
        return selftest()

    cfg = load_config(args.config)
    now = now_project(cfg)
    idle, active = idle_roles(cfg, now)
    total = len(idle) + len(active)
    ratio = len(idle) / max(1, total)

    print("%s: простаивают %d/%d (%.0f%%)" % (cfg["project"], len(idle), total, ratio * 100))
    for role, last in idle:
        print("  @%s: %s" % (role.upper(),
                             last.strftime("%Y-%m-%d %H:%M") if last else "нет сообщений"))

    if ratio < cfg["idle_ratio"]:
        print("ниже порога %.0f%% — пинг не нужен" % (cfg["idle_ratio"] * 100))
        return 0

    # Экспоненциальная задержка: пока команда стоит, пингуем всё реже (30 → 60 → 120 …),
    # чтобы кнут сам не жег лимит подписки каскадом пингов.
    last_ping, streak = ping_history(cfg["from_chat"], now, cfg["streak_window_hours"])
    if not backoff_due(cfg, last_ping, streak, now):
        print("экспоненциальная задержка — пинг отложен (серия %d, последний %s)"
              % (streak, last_ping.strftime("%Y-%m-%d %H:%M") if last_ping else "—"))
        return 0

    msg = ping_message(cfg, now, idle, active, streak)
    print("ПИНГ: " + msg)
    if args.dry_run:
        return 0

    with open(cfg["from_chat"], "a", encoding="utf-8") as f:
        f.write("\n" + msg + "\n")
    print("записано в %s" % cfg["from_chat"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
