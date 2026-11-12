#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_history_resolvable.py` — инвариант `HistoryStaysResolvable` на контуре (приёмка `BE-099`).

Что проверяется (одно поведение, но ключевое): после **мягкого** удаления партнёра запись журнала
продолжает показывать его **имя**, а не `id`.

Дефект `BE-099`: в `6-msa/BC-Journal/src/Journal.Storage/JournalReadRepository.cs:34` резолв имён фильтрует
`dir.Where(x => x.DeletedAt is null)` (то же `:95` для `lastPartnerName`) — удалённый партнёр выпадает из
справочника, и имя деградирует до `id`.

Коды: `0` — имя сохранилось (норма); `1` — имя подменено `id` (дефект, `BE-099`); `2` — не измерено
(стенд/авторизация/данные недоступны).

Использование:
    python 9-tests/scripts/check_history_resolvable.py                 # dev по умолчанию
    python 9-tests/scripts/check_history_resolvable.py --base https://…
    python 9-tests/scripts/check_history_resolvable.py --selftest
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request

DEFAULT_BASE = "https://xdiary-vps2.taile0b6ee.ts.net"
OTP = "000000"


def name_of(recent_payload, partner_id: str) -> str:
    """Имя партнёра из ответа `/journal/recent` (dict `lastPartner` или список записей)."""
    if isinstance(recent_payload, dict):
        lp = recent_payload.get("lastPartner") or {}
        if isinstance(lp, dict) and str(lp.get("id") or "") == partner_id:
            return str(lp.get("name") or "")
        items = recent_payload.get("items") or recent_payload.get("records") or []
    elif isinstance(recent_payload, list):
        items = recent_payload
    else:
        items = []
    for item in items:
        if not isinstance(item, dict):
            continue
        for key in ("partnerName", "partner"):
            v = item.get(key)
            if isinstance(v, str) and v:
                return v
    return ""


def name_from_item(item, partner_id: str) -> str:
    """Имя партнёра из записи: `partnersNames` (список, как отдаёт Journal), `partnerName`, `partner`, `partners[]`."""
    if not isinstance(item, dict):
        return ""
    for key in ("partnerName", "partner"):
        v = item.get(key)
        if isinstance(v, str) and v:
            return v
    names = item.get("partnersNames")
    if isinstance(names, list):
        for v in names:
            if isinstance(v, str) and v:
                return v
    for part in item.get("partners") or []:
        if isinstance(part, dict):
            pid = str(part.get("id") or "")
            nm = str(part.get("name") or "")
            if nm and (not partner_id or pid == partner_id):
                return nm
    return ""


def names_in_records(payload, partner_id: str) -> str:
    items = payload if isinstance(payload, list) else ((payload or {}).get("items") or (payload or {}).get("records") or [])
    for item in items:
        nm = name_from_item(item, partner_id)
        if nm:
            return nm
    return ""


def names_in_record(payload, partner_id: str) -> str:
    return name_from_item(payload, partner_id)


def verdict(name: str, partner_id: str, expected: str) -> int:
    """0 — имя на месте; 1 — деградировало до id; 2 — не измерено (имени нет вовсе)."""
    if not name:
        return 2
    if name == partner_id or name.startswith(partner_id[:8]):
        return 1
    return 0 if expected in name or name != partner_id else 1


def _call(base, method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data, method=method,
                                headers={"Content-Type": "application/json",
                                         **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:  # noqa: BLE001
        return None, "%s: %s" % (type(e).__name__, e)


def measure(base: str, call=None) -> int:
    call = call or (lambda method, path, body=None, token=None: call(method, path, body, token))
    phone = "+7999" + str(int(time.time() * 1000) % 10_000_000).zfill(7)
    name = "TE-E2E-Hist"
    st, raw = call("POST", "/api/v1/clients", {"phone": phone})
    if st != 200:
        print("::warning::check_history_resolvable: регистрация -> %s — не измерено (rc=2)" % st)
        return 2
    cid = (json.loads(raw) or {}).get("id")
    st, raw = call("POST", "/api/v1/clients/%s/verify" % cid, {"code": OTP})
    token = (json.loads(raw) if st == 200 else {}).get("accessToken") if st == 200 else None
    if not token:
        print("::warning::check_history_resolvable: verify -> %s, токена нет — не измерено (rc=2)" % st)
        return 2
    st, raw = call("POST", "/api/v1/partners", {"name": name, "kind": "person"}, token)
    pid = (json.loads(raw) if st == 200 else {}).get("id") if st == 200 else None
    if not pid:
        print("::warning::check_history_resolvable: создание партнёра -> %s — не измерено (rc=2)" % st)
        return 2
    st, raw = call("POST", "/api/v1/journal/records", {"date": "2026-11-12"}, token)
    rid = (json.loads(raw) if st == 200 else {}).get("id") if st == 200 else None
    if not rid:
        print("::warning::check_history_resolvable: создание записи -> %s — не измерено (rc=2)" % st)
        return 2
    # без `when` запись не попадает в список (проверено замером), поэтому ставим начало
    call("POST", "/api/v1/journal/records/%s/when" % rid,
          {"startedAt": "2026-11-12T10:00:00Z"}, token)
    call("POST", "/api/v1/journal/records/%s/partners" % rid, {"partnerId": pid}, token)
    st, raw = call("POST", "/api/v1/partners/%s/remove" % pid, {}, token)
    if st != 200:
        print("::warning::check_history_resolvable: remove -> %s — не измерено (rc=2)" % st)
        return 2
    st, raw = call("GET", "/api/v1/journal/recent", None, token)
    try:
        payload = json.loads(raw)
    except Exception:  # noqa: BLE001
        payload = None
    got = name_of(payload, pid)
    code_recent = verdict(got, pid, name)

    st2, raw2 = call("GET", "/api/v1/journal/records", None, token)
    try:
        payload2 = json.loads(raw2)
    except Exception:  # noqa: BLE001
        payload2 = None
    got_records = names_in_records(payload2, pid)
    code_records = verdict(got_records, pid, name)

    print("партнёр: %s" % pid)
    print("  /journal/recent  (резолв :95): %r -> rc=%d" % (got, code_recent))
    print("  /journal/records (резолв :34): %r -> rc=%d" % (got_records, code_records))

    st3, raw3 = call("GET", "/api/v1/journal/records/%s" % rid, None, token)
    try:
        payload3 = json.loads(raw3)
    except Exception:  # noqa: BLE001
        payload3 = None
    got_one = names_in_record(payload3, pid)
    code_one = verdict(got_one, pid, name)
    print("  /journal/records/{id} (резолв :47): %r -> rc=%d" % (got_one, code_one))

    codes = (code_recent, code_records, code_one)
    code = 1 if (1 in codes) else (0 if 0 in codes else 2)
    if code == 0:
        print("квитанция: имя сохранилось | rc=0")
    elif code == 1:
        print("::error::check_history_resolvable: имя подменено id — дефект BE-099 "
              "(JournalReadRepository.cs:34/:95), rc=1")
    else:
        print("::warning::check_history_resolvable: имени в ответе нет — не измерено (rc=2)")
    return code


def selftest() -> int:
    ok = total = 0

    def case(label, got, pid, expected, want):
        nonlocal ok, total
        total += 1
        rc = verdict(got, pid, expected)
        if rc == want:
            ok += 1
            print("  [OK]   %s -> rc=%d" % (label, rc))
        else:
            print("  [FAIL] %s -> rc=%d (ждали %d)" % (label, rc, want))

    pid = "64635485-add4-4187-9ccb-b7b77d322a3a"
    case("имя на месте", "TE-E2E-Hist", pid, "TE-E2E-Hist", 0)
    case("имя = id (дефект BE-099)", pid, pid, "TE-E2E-Hist", 1)
    case("имени нет", "", pid, "TE-E2E-Hist", 2)

    total += 1
    if name_of({"lastPartner": {"id": pid, "name": "TE-E2E-Hist"}}, pid) == "TE-E2E-Hist":
        ok += 1
        print("  [OK]   разбор lastPartner")
    else:
        print("  [FAIL] разбор lastPartner")

    total += 1
    recs = {"items": [{"id": "r1", "partners": [{"id": pid, "name": "TE-E2E-Hist"}]}]}
    if names_in_records(recs, pid) == "TE-E2E-Hist":
        ok += 1
        print("  [OK]   разбор /journal/records")
    else:
        print("  [FAIL] разбор /journal/records")
    total += 1
    one = {"id": "r1", "partners": [{"id": pid, "name": "TE-E2E-Hist"}]}
    if names_in_record(one, pid) == "TE-E2E-Hist":
        ok += 1
        print("  [OK]   разбор одной записи")
    else:
        print("  [FAIL] разбор одной записи")
    # --- поток на фейковом транспорте: без сети, но через весь measure() ---
    def fake(mode):
        state = {"pid": "pid-1", "rid": "rid-1", "removed": False}

        def call(method, path, body=None, token=None):
            if path == "/api/v1/clients":
                if mode == "create-fails":
                    return 500, "{}"
                return 200, '{"id":"cid-1"}'
            if path.endswith("/verify"):
                return 200, '{"accessToken":"tok"}'
            if path == "/api/v1/partners":
                return 200, '{"id":"pid-1"}'
            if path == "/api/v1/journal/records":
                return 200, '{"id":"rid-1"}'
            if path.endswith("/when") or path.endswith("/partners"):
                return 200, "{}"
            if path.endswith("/remove"):
                state["removed"] = True
                return 200, "{}"
            if path == "/api/v1/journal/recent":
                name = "TE-E2E-Hist"
                if mode == "defect":
                    name = "pid-1"
                if mode == "no-name":
                    name = ""
                return 200, '{"lastPartner":{"id":"pid-1","name":"%s"}}' % name
            if path == "/api/v1/journal/records":
                return 200, "[]"
            if path.startswith("/api/v1/journal/records/"):
                name = "TE-E2E-Hist"
                if mode == "defect":
                    name = "pid-1"
                if mode == "no-name":
                    name = ""
                return 200, '{"id":"rid-1","partnersNames":["%s"]}' % name
            return 404, "{}"

        return call

    for mode, want in (("ok", 0), ("defect", 1), ("no-name", 2), ("create-fails", 2)):
        total += 1
        got = measure("http://fake", fake(mode))
        if got == want:
            ok += 1
            print("  [OK]   поток %-12s -> rc=%d" % (mode, got))
        else:
            print("  [FAIL] поток %-12s -> rc=%d (ждали %d)" % (mode, got, want))

    print("check_history_resolvable --selftest: %d/%d "
          "(имя / id / нет имени / lastPartner / records / one)" % (ok, total))
    return 0 if ok == total else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="инвариант HistoryStaysResolvable на контуре")
    ap.add_argument("--base", default=DEFAULT_BASE)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    return measure(a.base.rstrip("/"))


if __name__ == "__main__":
    sys.exit(main())
