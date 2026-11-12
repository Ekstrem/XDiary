# -*- coding: utf-8 -*-
"""Проверка поведения `TE-050` на dev **прямыми HTTP-запросами** (без `dotnet test`).

Что проверяем (ровно четыре теста из `PartnerSoftDeleteTests.cs`, но «руками» по контуру):
1. удалённый партнёр исчезает из `GET /api/v1/partners`;
2. повторный `POST /partners/{id}/remove` — не 5xx (идемпотентность);
3. `GET /partners/{id}` после удаления → 404/403, не 200;
4. `HistoryStaysResolvable`: запись журнала, ссылающаяся на удалённого партнёра, всё ещё показывает **имя**
   (это дефект `BE-099`: резолв фильтровал `DeletedAt is null`).

Телефон — `+7999…` (такие чистит `cleanup-e2e-dev.sh`), OTP на dev — `000000`.
"""
import json
import time
import urllib.error
import urllib.request

BASE = "https://xdiary-vps2.taile0b6ee.ts.net"
OTP = "000000"
PHONE = "+7999" + str(int(time.time() * 1000) % 10_000_000).zfill(7)
NAME = "TE-E2E-SoftDelete"


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                headers={"Content-Type": "application/json",
                                         **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read().decode("utf-8", "replace")
            return r.status, raw
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return None, "%s: %s" % (type(e).__name__, e)


def jload(raw):
    try:
        return json.loads(raw)
    except Exception:
        return None


print("телефон:", PHONE)
st, raw = call("POST", "/api/v1/clients", {"phone": PHONE})
print("1) POST /clients ->", st, raw[:120])
doc = jload(raw) or {}
cid = doc.get("id") or doc.get("clientId")
if not cid:
    print("   НЕТ id клиента — дальше нельзя")
    raise SystemExit(2)

st, raw = call("POST", "/api/v1/clients/%s/verify" % cid, {"code": OTP})
print("2) POST /clients/{id}/verify ->", st, raw[:140])
doc = jload(raw) or {}
token = doc.get("accessToken") or doc.get("access_token")
if not token:
    print("   НЕТ accessToken — дальше нельзя")
    raise SystemExit(2)

st, raw = call("POST", "/api/v1/partners", {"name": NAME, "kind": "person"}, token)
print("3) POST /partners ->", st, raw[:140])
pdoc = jload(raw) or {}
pid = pdoc.get("id") or pdoc.get("partnerId")
if not pid:
    print("   НЕТ id партнёра — дальше нельзя")
    raise SystemExit(2)

st, raw = call("GET", "/api/v1/partners", None, token)
in_list = NAME in raw or (pid and pid in raw)
print("4) GET /partners содержит партнёра:", in_list, "| статус", st)

# запись журнала, ссылающаяся на партнёра (для проверки «имя остаётся резолвимым»)
rec_body = {"date": "2026-11-12", "eventType": "kiss", "partnerId": pid, "notes": "TE-050 e2e"}
st, raw = call("POST", "/api/v1/journal", rec_body, token)
print("5) POST /journal ->", st, raw[:140])

st, raw = call("POST", "/api/v1/partners/%s/remove" % pid, {}, token)
print("6) POST /partners/{id}/remove ->", st, raw[:100])
st, raw = call("GET", "/api/v1/partners", None, token)
print("7) после удаления партнёр в списке:", (NAME in raw or (pid and pid in raw)), "| статус", st)

st2, raw2 = call("POST", "/api/v1/partners/%s/remove" % pid, {}, token)
print("8) повторный remove ->", st2, "(ожидание: не 5xx)", raw2[:80])
st3, raw3 = call("GET", "/api/v1/partners/%s" % pid, None, token)
print("9) GET /partners/{id} после удаления ->", st3, "(ожидание: 404/403, не 200)")

st4, raw4 = call("GET", "/api/v1/journal/recent", None, token)
doc4 = jload(raw4)
names = []
if isinstance(doc4, list):
    names = [str(x.get("partnerName") or x.get("partner") or "") for x in doc4]
elif isinstance(doc4, dict):
    items = doc4.get("items") or doc4.get("records") or []
    names = [str(x.get("partnerName") or x.get("partner") or "") for x in items]
print("10) GET /journal/recent ->", st4, "| первые имена:", names[:3])
print("    имя партнёра резолвится после удаления:", any(NAME in n for n in names))
print("    НЕ резолвится (виден id):", any(pid in n for n in names))
