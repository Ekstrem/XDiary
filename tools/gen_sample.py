"""Генерирует тестовые CSV соматики и сна для XDiary (в samples/)."""
import os
import csv
import math
import datetime

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "samples")
os.makedirs(OUT, exist_ok=True)

now = datetime.datetime.now().replace(second=0, microsecond=0)
HOURS = 3
start = now - datetime.timedelta(hours=HOURS)

rows = []
t = start
while t <= now:
    minutes_from_end = (now - t).total_seconds() / 60.0
    hr = 62.0
    stress = 22
    battery = 70
    hrv = 48
    # имитация «пика» в последние 25 минут (чтобы на графике был виден всплеск)
    if minutes_from_end < 25:
        x = 25 - minutes_from_end  # 0..25
        if x < 6:
            hr = 62 + (x / 6.0) * 58
        elif x < 16:
            hr = 120 + math.sin(x) * 3
        else:
            hr = 120 - ((x - 16) / 9.0) * 58
        stress = min(96, int(20 + (hr - 62) * 0.7))
        battery = max(5, int(70 - (x / 25.0) * 25))
        hrv = max(20, int(48 - (hr - 62) * 0.25))
    rows.append([t.strftime("%Y-%m-%dT%H:%M"), int(round(hr)), int(stress), int(battery), int(hrv)])
    t += datetime.timedelta(minutes=1)

with open(os.path.join(OUT, "sample_somatic.csv"), "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["timestamp", "hr", "stress", "body_battery", "hrv"])
    w.writerows(rows)

today = now.date()
with open(os.path.join(OUT, "sample_sleep.csv"), "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["date", "sleep_score", "duration_min", "deep_min", "light_min", "rem_min", "awake_min"])
    w.writerow([today.isoformat(), 82, 440, 95, 210, 75, 60])

print("OK ->", OUT)
print("somatic rows:", len(rows))
