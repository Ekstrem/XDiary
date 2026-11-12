# -*- coding: utf-8 -*-
"""Проверка `TE-050` на dev прямыми HTTP-запросами (продолжение): запись журнала + резолв имени.

Правильный путь создания записи — `POST /api/v1/journal/records` (фронт: `api.js:153`), затем
`POST /api/v1/journal/records/{id}/partners {partnerId}` (`api.js:156`). Проверяем `BE-099`:
после удаления партнёра имя в `GET /journal/recent` должно остаться **именем**, а не `id`.
"""
import json
import time
import urllib.error
import urllib.request

BASE = "https://xdiary-vps2.taile0b6ee.ts.net"
OTP = "000000"
PHONE = "+7999" + str(int(time.time() * 1000) % 10_000_000).zfill(7)
NAME = "TE-E2E-Hist"


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                headers={"Content-Type": "application/json",
                                         **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return None, "%s: %s" % (type(e).__name__, e)


def j(raw):
    try:
        return json.loads(raw)
    except Exception:
        return None


print("телефон:", PHONE)
st, raw = call("POST", "/api/v1/clients", {"phone": PHONE})
cid = (j(raw) or {}).get("id")
print("1) POST /clients ->", st, "| id:", cid)
st, raw = call("POST", "/api/v1/clients/%s/verify" % cid, {"code": OTP})
token = (j(raw) or {}).get("accessToken")
print("2) verify ->", st, "| токен:", "есть" if token else "НЕТ")

st, raw = call("POST", "/api/v1/partners", {"name": NAME, "kind": "person"}, token)
pid = (j(raw) or {}).get("id")
print("3) POST /partners ->", st, "| id:", pid)

st, raw = call("POST", "/api/v1/journal/records", {"date": "2026-11-12"}, token)
rid = (j(raw) or {}).get("id") or (j(raw) or {}).get("aggregateId")
print("4) POST /journal/records ->", st, "| id:", rid, "|", raw[:90])

st, raw = call("POST", "/api/v1/journal/records/%s/partners" % rid, {"partnerId": pid}, token)
print("5) POST /journal/records/{id}/partners ->", st, "|", raw[:100])

st, raw = call("GET", "/api/v1/journal/recent", None, token)
print("6) /journal/recent ДО удаления ->", st, "|", raw[:160])

st, raw = call("POST", "/api/v1/partners/%s/remove" % pid, {}, token)
print("7) remove партнёра ->", st)

st, raw = call("GET", "/api/v1/journal/recent", None, token)
print("8) /journal/recent ПОСЛЕ удаления ->", st, "|", raw[:220])
doc = j(raw)
items = doc if isinstance(doc, list) else ((doc or {}).get("items") or (doc or {}).get("records") or [])
names = [str(x.get("partnerName") or x.get("partner") or "") for x in items if isinstance(x, dict)]
print("   имена партнёров в ответе:", names[:4])
print("   имя сохранилось (BE-099 ок):", any(NAME in n for n in names))
print("   вместо имени id (дефект):", any(pid and pid in n for n in names))
