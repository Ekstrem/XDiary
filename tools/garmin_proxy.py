#!/usr/bin/env python3
"""XDiary — локальный HTTP-прокси к Garmin Connect Personal API.

Зачем: Garmin не отдаёт CORS-заголовки, поэтому PWA в браузере не может
обращаться к personal.connectapi.garmin.com напрямую. Этот прокси крутится
на ПК (в той же Wi-Fi, что и телефон) и отдаёт JSON с CORS для приложения.

Запуск:
    python tools/garmin_proxy.py
Проверка:
    http://127.0.0.1:8765/health

Телефон (XDiary) в Настройках указывает адрес вида http://192.168.1.10:8765
Ключ берётся из .env, либо из заголовка Authorization: ApiKey <ключ> (если
в приложении введён свой ключ).
"""

import datetime
import json
import os
import sys
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import garmin_sync as gs

PORT = int(os.environ.get("GARMIN_PROXY_PORT", "8765"))


def _range(qs):
    today = datetime.date.today()
    dto = (qs.get("to") or [today.isoformat()])[0]
    dfrom = (qs.get("from") or [(today - datetime.timedelta(days=7)).isoformat()])[0]
    return dfrom, dto


def _dates(dfrom, dto):
    return datetime.date.fromisoformat(dfrom), datetime.date.fromisoformat(dto)


def _fetch_hr(token, start, end):
    hr, resting = {}, {}
    for d in gs.day_iter(start, end):
        obj = gs.get_json(token, "/wellness-service/wellness/dailyHeartRate?date=%s" % d.isoformat())
        if not obj:
            continue
        r = obj.get("restingHeartRate")
        if r:
            resting[d.isoformat()] = r
        for it in obj.get("heartRateValues") or []:
            if len(it) >= 2 and it[1] is not None:
                hr[gs.minute_of(it[0])] = it[1]
    return hr, resting


def _fetch_bb(token, start, end):
    bb = {}
    obj = gs.get_json(token, "/wellness-service/wellness/bodyBattery/reports/daily?startDate=%s&endDate=%s&chunks=288" % (start, end))
    for dayv in obj or []:
        for it in dayv.get("bodyBatteryValuesArray") or []:
            if len(it) >= 2 and it[1] is not None:
                bb[gs.minute_of(it[0])] = it[1]
    return bb


def _fetch_stress(token, start, end):
    stress = {}
    d0 = start
    while d0 <= end:
        d1 = min(d0 + datetime.timedelta(days=6), end)
        obj = gs.get_json(token, "/usersummary-service/stats/stress/daily/%s/%s" % (d0.isoformat(), d1.isoformat()))
        for row in obj or []:
            cd = row.get("calendarDate")
            if cd:
                stress[cd] = (row.get("values") or {}).get("overallStressLevel")
        d0 = d1 + datetime.timedelta(days=1)
    return stress


def _fetch_sleep(token, start, end):
    sleeps = []
    obj = gs.get_json(token, "/sleep-service/sleep/dailySleepSummariesByDate?startDate=%s&endDate=%s" % (start, end))
    if isinstance(obj, list) and obj:
        for s in obj:
            day = s.get("calendarDate")
            if not day:
                continue
            sc = ((s.get("sleepScores") or {}).get("overall") or {})
            sleeps.append({
                "date": day,
                "score": sc.get("value"),
                "durationMin": round((s.get("sleepTimeSeconds") or 0) / 60),
                "deepMin": round((s.get("deepSleepSeconds") or 0) / 60),
                "lightMin": round((s.get("lightSleepSeconds") or 0) / 60),
                "remMin": round((s.get("remSleepSeconds") or 0) / 60),
                "awakeMin": round((s.get("awakeSleepSeconds") or 0) / 60),
            })
    else:
        for d in gs.day_iter(start, end):
            obj = gs.get_json(token, "/sleep-service/sleep/dailySleep?date=%s" % d.isoformat())
            if not obj:
                continue
            sc = ((obj.get("sleepScores") or {}).get("overall") or {})
            sleeps.append({
                "date": d.isoformat(),
                "score": sc.get("value"),
                "durationMin": round((obj.get("sleepTimeSeconds") or 0) / 60),
                "deepMin": round((obj.get("deepSleepSeconds") or 0) / 60),
                "lightMin": round((obj.get("lightSleepSeconds") or 0) / 60),
                "remMin": round((obj.get("remSleepSeconds") or 0) / 60),
                "awakeMin": round((obj.get("awakeSleepSeconds") or 0) / 60),
            })
    return sleeps


def _fetch_hrv(token, start, end):
    hrv = {}
    obj = gs.get_json(token, "/hrv-service/hrv/daily/%s/%s" % (start, end))
    summaries = obj.get("hrvSummaries") if isinstance(obj, dict) else None
    for s in summaries or []:
        day = s.get("calendarDate")
        if day and s.get("lastNightAvg") is not None:
            hrv[day] = s.get("lastNightAvg")
    return hrv


def build_sync(token, dfrom, dto):
    start, end = _dates(dfrom, dto)
    hr, resting = _fetch_hr(token, start, end)
    bb = _fetch_bb(token, start, end)
    stress = _fetch_stress(token, start, end)
    sleeps = _fetch_sleep(token, start, end)
    hrv = _fetch_hrv(token, start, end)
    somatic = [[t, hr.get(t), bb.get(t)] for t in sorted(set(hr) | set(bb))]
    return {
        "somatic": somatic,
        "sleep": sleeps,
        "stress": stress,
        "resting_hr": resting,
        "hrv": hrv,
        "debug": {
            "hr": len(hr),
            "bb": len(bb),
            "sleep": len(sleeps),
            "stress": len([d for d in stress if stress[d] is not None]),
            "hrv": len(hrv),
            "resting": len(resting),
        },
    }


def build_activities(token, dfrom, dto, favorite):
    q = "?limit=100&startDate=%s&endDate=%s" % (dfrom, dto)
    if favorite:
        q += "&favorite=1"
    obj = gs.get_json(token, "/activitylist-service/activities/search/activities" + q)
    out = []
    for a in obj or []:
        out.append({
            "id": a.get("activityId"),
            "name": a.get("activityName"),
            "type": (a.get("activityType") or {}).get("typeKey"),
            "startLocal": a.get("startTimeLocal"),
            "durationSec": a.get("duration"),
            "isFavorite": a.get("isFavorite"),
        })
    return out


HEARTS = "❤♥🖤💔❣"


def _is_heart(name):
    return any(ch in (name or "") for ch in HEARTS)


def sessions_csv(acts, fmt):
    import csv
    import io

    buf = io.StringIO()
    w = csv.writer(buf)
    if fmt == "records":
        w.writerow(["id", "started_at", "duration_min", "partner", "place", "positions",
                    "tags", "costumes", "toys", "fantasies", "orgasms_me", "orgasms_partner", "garmin"])
        for a in acts:
            if not _is_heart(a.get("name")):
                continue
            st = (a.get("startLocal") or "").replace(" ", "T")[:16]
            w.writerow(["garmin-%s" % a.get("id"), st, round((a.get("durationSec") or 0) / 60),
                        "", "", "", "", "", "", "", 0, 0, 1])
    else:
        w.writerow(["date", "time", "duration_min", "name", "type", "favorite"])
        for a in acts:
            if not _is_heart(a.get("name")):
                continue
            parts = (a.get("startLocal") or "").split(" ")
            w.writerow([parts[0] if parts else "", parts[1] if len(parts) > 1 else "",
                        round((a.get("durationSec") or 0) / 60), a.get("name") or "",
                        a.get("type") or "", "1" if a.get("isFavorite") else "0"])
    return buf.getvalue()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        sys.stderr.write("[proxy] %s %s\n" % (getattr(self, "command", "-"), getattr(self, "path", "-")))

    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")

    def _json(self, obj, code=200):
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self._cors()
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _key(self):
        auth = self.headers.get("Authorization", "")
        if auth.startswith("ApiKey "):
            return auth[len("ApiKey "):].strip()
        return gs.load_key()

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_GET(self):
        try:
            u = urllib.parse.urlparse(self.path)
            qs = urllib.parse.parse_qs(u.query)
            if u.path == "/health":
                self._json({"ok": True})
                return
            # локальные CSV (без обращения к Garmin)
            if u.path.startswith("/export/"):
                fname = os.path.basename(u.path[len("/export/"):])
                fpath = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "garmin_export", fname)
                if not os.path.isfile(fpath):
                    self._json({"error": "not found"}, 404)
                    return
                with open(fpath, "rb") as f:
                    data = f.read()
                self.send_response(200)
                self._cors()
                self.send_header("Content-Type", "text/csv; charset=utf-8")
                self.send_header("Content-Disposition", 'attachment; filename="%s"' % fname)
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
                return
            key = self._key()
            if not key:
                self._json({"error": "no api key"}, 400)
                return
            token = gs.get_token(key)
            dfrom, dto = _range(qs)
            if u.path == "/sync":
                self._json(build_sync(token, dfrom, dto))
                return
            if u.path == "/sync/stress":
                self._json({"stress": _fetch_stress(token, *_dates(dfrom, dto))})
                return
            if u.path == "/sync/somatic":
                hr, resting = _fetch_hr(token, *_dates(dfrom, dto))
                bb = _fetch_bb(token, *_dates(dfrom, dto))
                somatic = [[t, hr.get(t), bb.get(t)] for t in sorted(set(hr) | set(bb))]
                self._json({"somatic": somatic, "resting_hr": resting, "counts": {"hr": len(hr), "bb": len(bb)}})
                return
            if u.path == "/sync/sleep":
                self._json({"sleep": _fetch_sleep(token, *_dates(dfrom, dto))})
                return
            if u.path == "/sync/hrv":
                self._json({"hrv": _fetch_hrv(token, *_dates(dfrom, dto))})
                return
            if u.path == "/activities":
                favorite = (qs.get("favorite") or ["0"])[0] in ("1", "true", "yes")
                self._json(build_activities(token, dfrom, dto, favorite))
                return
            if u.path == "/sessions.csv":
                fmt = (qs.get("format") or ["human"])[0]
                acts = build_activities(token, dfrom, dto, False)
                text = "\ufeff" + sessions_csv(acts, fmt)
                data = text.encode("utf-8")
                self.send_response(200)
                self._cors()
                self.send_header("Content-Type", "text/csv; charset=utf-8")
                self.send_header("Content-Disposition", 'attachment; filename="sessions.csv"')
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
                return
            self._json({"error": "not found"}, 404)
        except Exception as e:  # noqa: BLE001
            sys.stderr.write("[proxy] ERROR %s\n" % e)
            self._json({"error": str(e)}, 500)


def main():
    log_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "garmin_export", "proxy.log")
    try:
        os.makedirs(os.path.dirname(log_path), exist_ok=True)
        log = open(log_path, "a", encoding="utf-8", buffering=1)
        sys.stdout = log
        sys.stderr = log
    except OSError:
        pass
    print("XDiary Garmin proxy on http://0.0.0.0:%d" % PORT)
    print("  /health | /sync?from=YYYY-MM-DD&to=YYYY-MM-DD | /activities?favorite=1&from=..&to=..")
    ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()


if __name__ == "__main__":
    main()
