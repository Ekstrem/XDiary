#!/usr/bin/env python3
# XDiary — выгрузка Garmin Connect Personal API в CSV для импорта в XDiary.
#
# Авторизация: ключ -> OAuth2 token (на 24 ч):
#   POST https://personal.connectapi.garmin.com/personalapitoken-service/personal-api/personal-api-key/token
#   Authorization: ApiKey <ключ>
# Далее: Authorization: Bearer <token>
#
# Что выгружается:
#   somatic.csv      — пульс (поминутно) + Body Battery, формат XDiary "соматика"
#   sleep.csv        — сон по дням, формат XDiary "сон"
#   stress.csv       — стресс по дням (формат Garmin Connect Reports)
#   resting_hr.csv   — пульс покоя по дням (формат Garmin Connect Reports)
#
# Пример:
#   python tools/garmin_sync.py --from 2026-10-21 --to 2026-10-28
#   (по умолчанию — последние 7 дней)

import argparse
import csv
import datetime
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = "https://personal.connectapi.garmin.com"
TOKEN_PATH = "/personalapitoken-service/personal-api/personal-api-key/token"
DEFAULT_OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "garmin_export")
DELAY = float(os.environ.get("GARMIN_DELAY", "0.4"))


def load_key():
    key = os.environ.get("GARMIN_API_KEY", "").strip()
    if key:
        return key
    base = os.path.dirname(os.path.abspath(__file__))
    for path in (os.path.join(base, "..", ".env"), ".env"):
        if os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if line.startswith("GARMIN_API_KEY="):
                        return line.split("=", 1)[1].strip().strip('"').strip("'")
    sys.exit("Не найден GARMIN_API_KEY: задайте переменную окружения или строку в .env")


def request(method, url, headers, data=None):
    req = urllib.request.Request(url, method=method, headers=headers, data=data)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()


def get_token(key):
    status, body = request("POST", BASE + TOKEN_PATH, {"Authorization": "ApiKey " + key})
    text = body.decode("utf-8", "replace")
    if status != 200:
        raise SystemExit("Не удалось получить токен (HTTP %s): %s" % (status, text[:400]))
    try:
        obj = json.loads(text)
    except ValueError:
        obj = None
    if isinstance(obj, dict):
        for k in ("access_token", "accessToken", "token", "jwt"):
            if isinstance(obj.get(k), str) and obj[k]:
                return obj[k]
    if text.strip() and not text.lstrip().startswith("{"):
        return text.strip()
    raise SystemExit("Не удалось разобрать ответ токена: %s" % text[:400])


def get_json(token, path):
    time.sleep(DELAY)
    status, body = request("GET", BASE + path, {
        "Authorization": "Bearer " + token,
        "Accept": "application/json",
    })
    if status != 200:
        print("  ! %s -> HTTP %s" % (path, status))
        return None
    try:
        return json.loads(body.decode("utf-8", "replace"))
    except ValueError:
        print("  ! %s -> ответ не JSON" % path)
        return None


def minute_of(ms):
    dt = datetime.datetime.fromtimestamp(ms / 1000.0)
    return dt.strftime("%Y-%m-%dT%H:%M")


def day_iter(start, end):
    d = start
    while d <= end:
        yield d
        d += datetime.timedelta(days=1)


def main():
    ap = argparse.ArgumentParser(description="Выгрузка Garmin Connect Personal API в CSV для XDiary")
    today = datetime.date.today()
    ap.add_argument("--from", dest="dfrom", default=(today - datetime.timedelta(days=7)).isoformat())
    ap.add_argument("--to", dest="dto", default=today.isoformat())
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()

    start = datetime.date.fromisoformat(args.dfrom)
    end = datetime.date.fromisoformat(args.dto)
    if end < start:
        sys.exit("--to раньше --from")

    token = get_token(load_key())
    os.makedirs(args.out, exist_ok=True)

    hr = {}
    resting = {}
    print("Пульс + пульс покоя...")
    for d in day_iter(start, end):
        obj = get_json(token, "/wellness-service/wellness/dailyHeartRate?date=%s" % d.isoformat())
        if not obj:
            continue
        r = obj.get("restingHeartRate")
        if r:
            resting[d.isoformat()] = r
        for it in obj.get("heartRateValues") or []:
            if len(it) >= 2 and it[1] is not None:
                hr[minute_of(it[0])] = it[1]

    print("Body Battery...")
    bb = {}
    obj = get_json(token, "/wellness-service/wellness/bodyBattery/reports/daily?startDate=%s&endDate=%s&chunks=288" % (start, end))
    for dayv in obj or []:
        for it in dayv.get("bodyBatteryValuesArray") or []:
            if len(it) >= 2 and it[1] is not None:
                bb[minute_of(it[0])] = it[1]

    print("Стресс...")
    stress = {}
    d0 = start
    while d0 <= end:
        d1 = min(d0 + datetime.timedelta(days=6), end)
        obj = get_json(token, "/usersummary-service/stats/stress/daily/%s/%s" % (d0.isoformat(), d1.isoformat()))
        for row in obj or []:
            cd = row.get("calendarDate")
            if cd:
                stress[cd] = (row.get("values") or {}).get("overallStressLevel")
        d0 = d1 + datetime.timedelta(days=1)

    print("Сон...")
    sleeps = []
    obj = get_json(token, "/sleep-service/sleep/dailySleepSummariesByDate?startDate=%s&endDate=%s" % (start, end))
    if isinstance(obj, list) and obj:
        for s in obj:
            day = s.get("calendarDate")
            if not day:
                continue
            sc = ((s.get("sleepScores") or {}).get("overall") or {})
            sleeps.append({
                "date": day,
                "score": sc.get("value"),
                "duration_min": round((s.get("sleepTimeSeconds") or 0) / 60),
                "deep_min": round((s.get("deepSleepSeconds") or 0) / 60),
                "light_min": round((s.get("lightSleepSeconds") or 0) / 60),
                "rem_min": round((s.get("remSleepSeconds") or 0) / 60),
                "awake_min": round((s.get("awakeSleepSeconds") or 0) / 60),
            })
    else:
        for d in day_iter(start, end):
            obj = get_json(token, "/sleep-service/sleep/dailySleep?date=%s" % d.isoformat())
            if not obj:
                continue
            sc = ((obj.get("sleepScores") or {}).get("overall") or {})
            sleeps.append({
                "date": d.isoformat(),
                "score": sc.get("value"),
                "duration_min": round((obj.get("sleepTimeSeconds") or 0) / 60),
                "deep_min": round((obj.get("deepSleepSeconds") or 0) / 60),
                "light_min": round((obj.get("lightSleepSeconds") or 0) / 60),
                "rem_min": round((obj.get("remSleepSeconds") or 0) / 60),
                "awake_min": round((obj.get("awakeSleepSeconds") or 0) / 60),
            })

    def write_csv(name, header, rows):
        path = os.path.join(args.out, name)
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(header)
            w.writerows(rows)
        return path

    write_csv("somatic.csv",
              ["timestamp", "hr", "stress", "body_battery", "hrv"],
              [[t, hr.get(t, ""), "", bb.get(t, ""), ""] for t in sorted(set(hr) | set(bb))])

    write_csv("sleep.csv",
              ["date", "sleep_score", "duration_min", "deep_min", "light_min", "rem_min", "awake_min"],
              [[s["date"], s["score"], s["duration_min"], s["deep_min"], s["light_min"], s["rem_min"], s["awake_min"]] for s in sleeps])

    write_csv("stress.csv",
              ["", "Переживание стресса"],
              [[datetime.date.fromisoformat(day).strftime("%m/%d/%Y"), stress[day]]
               for day in sorted(stress) if stress[day] is not None])

    write_csv("resting_hr.csv",
              ["", "Частота пульса в состоянии покоя"],
              [[datetime.date.fromisoformat(day).strftime("%m/%d/%Y"), resting[day]] for day in sorted(resting)])

    print("\nГотово. Папка:", os.path.abspath(args.out))
    print("  somatic.csv   — %d точек (HR), %d точек (BB)" % (len(hr), len(bb)))
    print("  sleep.csv     — %d ночей" % len(sleeps))
    print("  stress.csv    — %d дней" % len([d for d in stress if stress[d] is not None]))
    print("  resting_hr.csv— %d дней" % len(resting))


if __name__ == "__main__":
    main()
