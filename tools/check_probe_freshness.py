#!/usr/bin/env python3
"""check_probe_freshness.py — сторож для монитора входа (DO-080).

Зачем: `public-probe.yml` ходит каждые 15 минут (``*/15``) и проверяет вход из интернета.
Но если workflow отключён, расписание снято или раннер перестал запускаться, **молчание
пробы выглядит как успех**. Этот сторож живёт в ДРУГОМ workflow и отвечает на вопрос
«когда проба последний раз реально отчиталась успехом».

Правило: последний успешный прогон старше `--max-age-minutes` (по умолчанию **45** = 3 x 15 мин)
⇒ состояние **НЕИЗВЕСТНО** (`rc=1`), а не «ОК».

Коды: `0` — OK (проба свежая); `1` — НЕИЗВЕСТНО (проба молчит / нет ни одного success);
`2` — не измерено (нет токена, API недоступен) — «не смог измерить» не выдаётся за «всё хорошо».

Печатает строку `last_ok=<ts> age=<min> state=OK|НЕИЗВЕСТНО` — состояние видно без чтения логов.
"""
import argparse
import datetime as dt
import json
import os
import sys
import urllib.error
import urllib.request

DEFAULT_MAX_AGE_MIN = 45   # 3 x интервал пробы (15 мин)
API = "https://api.github.com"


def state_for(last_ok, now, max_age_min):
    """(age_min, state) по времени последнего успеха. `None` -> проба не отчитывалась."""
    if last_ok is None:
        return None, "НЕИЗВЕСТНО"
    age = (now - last_ok).total_seconds() / 60.0
    return age, ("OK" if age <= max_age_min else "НЕИЗВЕСТНО")


def pick_last_ok(runs):
    """Время последнего ЗАВЕРШИВШЕГОСЯ УСПЕШНОГО прогона.

    «Прогон без шагов — не прогон»: берём только `status=completed` И `conclusion=success`.
    Отменённый/в ожидании/пустой прогон `last_ok` не обновляет.
    """
    best = None
    for r in runs or []:
        if r.get("status") != "completed" or r.get("conclusion") != "success":
            continue
        stamp = r.get("updated_at") or r.get("created_at")
        if not stamp:
            continue
        try:
            ts = dt.datetime.fromisoformat(stamp.replace("Z", "+00:00"))
        except ValueError:
            continue
        if best is None or ts > best:
            best = ts
    return best


def fetch_runs(repo, workflow, token, limit=20):
    url = "%s/repos/%s/actions/workflows/%s/runs?per_page=%d" % (API, repo, workflow, limit)
    req = urllib.request.Request(url, headers={
        "Authorization": "Bearer " + token,
        "Accept": "application/vnd.github+json",
        "User-Agent": "dsh-do-watchdog",
    })
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8")).get("workflow_runs", [])


def selftest():
    now = dt.datetime(2026, 11, 12, 12, 0, tzinfo=dt.timezone.utc)
    cases = []
    # свежий успех -> OK
    age, st = state_for(now - dt.timedelta(minutes=10), now, 45)
    cases.append(("свежий success (10 мин) -> OK", st == "OK" and age == 10.0))
    # старый успех -> НЕИЗВЕСТНО
    age, st = state_for(now - dt.timedelta(minutes=189), now, 45)
    cases.append(("старый success (189 мин) -> НЕИЗВЕСТНО", st == "НЕИЗВЕСТНО"))
    # граница ровно 45 -> OK
    age, st = state_for(now - dt.timedelta(minutes=45), now, 45)
    cases.append(("ровно 45 мин -> OK (граница включительно)", st == "OK"))
    # нет ни одного success -> НЕИЗВЕСТНО
    cases.append(("нет success -> НЕИЗВЕСТНО", state_for(None, now, 45)[1] == "НЕИЗВЕСТНО"))
    # отменённый/пустой прогон не обновляет last_ok
    runs = [
        {"status": "completed", "conclusion": "success", "updated_at": "2026-11-12T10:00:00Z"},
        {"status": "completed", "conclusion": "cancelled", "updated_at": "2026-11-12T11:55:00Z"},
        {"status": "in_progress", "conclusion": None, "updated_at": "2026-11-12T11:59:00Z"},
    ]
    picked = pick_last_ok(runs)
    cases.append(("cancelled/in_progress не обновляют last_ok",
                  picked == dt.datetime(2026, 11, 12, 10, 0, tzinfo=dt.timezone.utc)))
    # прогон старше 45 мин при наличии свежего cancelled -> НЕИЗВЕСТНО
    age, st = state_for(picked, dt.datetime(2026, 11, 12, 12, 0, tzinfo=dt.timezone.utc), 45)
    cases.append(("success 10:00 + cancelled 11:55 -> НЕИЗВЕСТНО", st == "НЕИЗВЕСТНО"))

    ok = 0
    for name, passed in cases:
        print("  [%s] %s" % ("OK " if passed else "FAIL", name))
        ok += 1 if passed else 0
    print("check_probe_freshness --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="сторож свежести public-probe (DO-080)")
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY", "Ekstrem/XDiary"))
    ap.add_argument("--workflow", default="public-probe.yml")
    ap.add_argument("--max-age-minutes", type=int, default=DEFAULT_MAX_AGE_MIN)
    ap.add_argument("--token", default=os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN"))
    ap.add_argument("--now", help="ISO-время 'сейчас' (для проверок; по умолчанию — реальное)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args(argv)

    if args.selftest:
        return selftest()

    now = dt.datetime.now(dt.timezone.utc)
    if args.now:
        now = dt.datetime.fromisoformat(args.now.replace("Z", "+00:00"))

    if not args.token:
        print("::error::check_probe_freshness: НЕ ИЗМЕРЕНО — нет токена (GITHUB_TOKEN)")
        return 2
    try:
        runs = fetch_runs(args.repo, args.workflow, args.token)
    except (urllib.error.URLError, urllib.error.HTTPError, ValueError, OSError) as ex:
        print("::error::check_probe_freshness: НЕ ИЗМЕРЕНО — API недоступен (%s)" % type(ex).__name__)
        return 2

    last_ok = pick_last_ok(runs)
    age, state = state_for(last_ok, now, args.max_age_minutes)
    age_s = "?" if age is None else "%.0f" % age
    print("last_ok=%s age=%s state=%s (порог %d мин, прогонов в выборке %d)" % (
        last_ok.isoformat().replace("+00:00", "Z") if last_ok else "никогда",
        age_s, state, args.max_age_minutes, len(runs)))
    if state != "OK":
        print("::error::check_probe_freshness: НЕИЗВЕСТНО — проба входа не отчитывалась успехом "
              "дольше %d мин (last_ok=%s). Молчание монитора не равно «всё хорошо»" % (
                  args.max_age_minutes, "никогда" if last_ok is None else age_s + " мин назад"))
        return 1
    print("::notice::check_probe_freshness: OK — проба отчитывалась %.0f мин назад" % age)
    return 0


if __name__ == "__main__":
    sys.exit(main())
