#!/usr/bin/env python
"""check_apim_events_liveness.py — детектор «зомби-маршрутов» Gravitee (DO-157, владелец/BE 2026-11-25 21:53).

Класс (инцидент 2026-11-25 21:15-21:22, root-cause @DO): запись API удалили **напрямую в Mongo**
(коллекция `apis`), но в `events_latest` осталось её `START_API`/`PUBLISH_API`-событие. Gateway
синхронизируется по `events_latest` и продолжает маршрутизировать по мёртвой реплике — все ручки
сервиса отдают 500 («Target not valid»), хотя код полностью исправен. Прод выглядел сломанным
40 минут, пока зомби не вычистили.

Правило: **удалять API только через management API** (он создаёт STOP-событие). Этот гейт ловит
нарушение постфактум: событие есть, живого документа — нет.

Критерий: в `events_latest` не должно быть START_API/PUBLISH_API-событий, чей `_id` отсутствует
в `apis` (STOP_API/UNPUBLISH_API у мёртвых — легальны: это и есть «удалено правильно»).
START_API у живого документа — норма.

Запуск (на VPS с контейнером apim-mongo):
    python3 tools/check_apim_events_liveness.py [--container xdiary-apim-mongo]
    python3 tools/check_apim_events_liveness.py --selftest

Коды: 0 — зомби нет; 1 — есть START/PUBLISH-зомби; 2 — не измерено (нет mongosh/mongo).
"""
import argparse
import re
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

CT = "xdiary-apim-mongo"


def mongosh(container, script):
    """Выполнить JS в mongosh контейнера; вернуть (rc, stdout)."""
    for exe in ("mongosh", "mongo"):
        r = subprocess.run(["podman", "exec", container, exe, "gravitee", "--quiet", "--eval", script],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        if r.returncode == 0 and not r.stderr.strip().startswith("Error"):
            return 0, r.stdout
    return 2, (r.stdout + r.stderr)[:200]


JS_LIVENESS = """
var alive = {};
db.apis.find({}, {_id: 1}).forEach(function(d){ alive[d._id] = true; });
var zombies = [];
db.events_latest.find({}).forEach(function(e){
  var type = e.type || "";
  if (type === "START_API" || type === "PUBLISH_API") {
    if (!alive[e._id]) zombies.push({id: e._id, type: type});
  }
});
print(JSON.stringify({total_apis: Object.keys(alive).length,
                      total_events: db.events_latest.countDocuments({}),
                      zombies: zombies}));
"""


def scan(container):
    rc, out = mongosh(container, JS_LIVENESS)
    if rc != 0:
        return None, "mongosh/mongo недоступны в контейнере %s" % container
    m = re.search(r"\{.*\}", out, re.S)
    if not m:
        return None, "неожиданный вывод mongosh: %s" % out[:120]
    import json
    return json.loads(m.group(0)), None


def receipt(d):
    return ("квитанция: apis=%d events_latest=%d | зомби START/PUBLISH без живого документа: %d"
            % (d["total_apis"], d["total_events"], len(d["zombies"])))


def selftest():
    ok = total = 0

    def case(name, cond):
        nonlocal ok, total
        total += 1
        if cond:
            ok += 1
        else:
            print("  [FAIL] %s" % name)

    # логика на словарях: живые ids vs события
    alive = {"a1": True, "a2": True}
    events = [
        {"_id": "a1", "type": "START_API"},      # живой — норма
        {"_id": "a2", "type": "STOP_API"},       # живой, стоп — норма
        {"_id": "dead1", "type": "START_API"},   # ЗОМБИ
        {"_id": "dead2", "type": "PUBLISH_API"}, # ЗОМБИ
        {"_id": "dead3", "type": "STOP_API"},    # мёртвый, стоп — легально (удалён правильно)
        {"_id": "dead4", "type": "UNPUBLISH_API"},  # легально
    ]
    zombies = [e for e in events
               if e["type"] in ("START_API", "PUBLISH_API") and e["_id"] not in alive]
    case("зомби ловятся: dead1+dead2",
         [z["_id"] for z in zombies] == ["dead1", "dead2"])
    case("STOP у мёртвого — НЕ зомби (удаление через management)",
         "dead3" not in [z["_id"] for z in zombies])
    case("живой START — не зомби", "a1" not in [z["_id"] for z in zombies])
    case("квитанция с числом", "зомби" in receipt({"total_apis": 2, "total_events": 6, "zombies": zombies}))

    print("check_apim_events_liveness --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--container", default=CT)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    d, err = scan(a.container)
    if err:
        print("::error::check_apim_events_liveness: не измерено — %s (rc=2)" % err)
        return 2
    for z in d["zombies"]:
        print("::error::check_apim_events_liveness: ЗОМБИ %s (%s) — запись удалена в Mongo напрямую, "
              "а gateway маршрутизирует по мёртвой реплике; удалите через management API и "
              "почистите events_latest" % (z["id"], z["type"]))
    print(receipt(d))
    return 1 if d["zombies"] else 0


if __name__ == "__main__":
    sys.exit(main())
