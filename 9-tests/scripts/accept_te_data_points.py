#!/usr/bin/env python3
"""accept_te_data_points.py — две приёмки @TE одной командой, чтобы их мог прогнать любой с доступом к контуру.

Зачем отдельный скрипт: из песочницы @TE контур недоступен (`curl` → `rc=35`, SSH-маршрут таймаутит), а обе
приёмки — это по 4–6 вызовов с паузами. Скрипт делает их сам и печатает **одну строку квитанции**, которую
достаточно вставить в чат.

Кейсы:
  `--case profile-stage1` — `TE-102`, этап 1 (контракт `request` = `required: [gender, photo, notes]`):
      1) `POST /partners {name}` → `GET`: имя есть;
      2) `POST /partners/{id}/profile {gender, photo:"", notes:""}` → `GET`: имя **не затёрто**;
      3) `POST .../profile {gender, photo:"TE-PHOTO", notes:"TE-NOTES"}` → `GET`: поля записались;
      4) `POST .../profile {gender, photo:"", notes:""}` → `GET`: `photo` **стёрт явно** (`""`).
      Плюс `GET /journal/recent` **после создания записи** — `lastPartner.name` должно быть именем, не id
      (`TE-102`, строка 5: два канала — проекция Partners и справочник Journal).

  `--case when-normalize` — `TE-101`, нормализация:
      `POST /journal/records` → `POST .../when` с `2026-11-07T09:00:00+03:00` и с `2026-11-07T06:00:00Z`:
      оба `200` и **одна и та же** сохранённая строка; мусор (`не дата`, ``, `"   "`, `1234567890`) → `400`.

Коды: `0` — все проверки кейса совпали; `1` — расхождение (с указанием, какое); `2` — не измерено (нет `--base`,
стенд недоступен).
"""
import argparse
import json
import sys
import time
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass


def call(base, method, path, body=None, token=None, timeout=25):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base.rstrip("/") + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json")
    req.add_header("Origin", "https://ekstrem.github.io")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, (r.read(200000) or b"").decode("utf-8", "replace"), None
    except urllib.error.HTTPError as e:
        return e.code, (e.read() or b"").decode("utf-8", "replace"), None
    except (urllib.error.URLError, OSError) as e:
        return None, "", "стенд недоступен: %s" % e


def parse(body):
    try:
        return json.loads(body or "")
    except (ValueError, TypeError):
        return None


def login(base, phone):
    st, body, err = call(base, "POST", "/api/v1/clients", {"phone": phone})
    if err:
        return None, None, err
    cid = (parse(body) or {}).get("id")
    st, body, err = call(base, "POST", "/api/v1/clients/%s/verify" % cid, {"code": "000000"})
    if err:
        return cid, None, err
    tok = (parse(body) or {}).get("accessToken")
    return cid, tok, None


# --- кейс profile-stage1 -------------------------------------------------------------------

def case_profile_stage1(base, phone, pause):
    cid, tok, err = login(base, phone)
    if err:
        return 2, "не измерено: %s" % err
    if not tok:
        return 2, "не измерено: verify не выдал токен (тестовый OTP включён?)"

    issues = []
    name = "TE-STAGE1-%d" % int(time.time())
    st, body, err = call(base, "POST", "/api/v1/partners", {"name": name}, token=tok)
    if err:
        return 2, "не измерено: %s" % err
    pid = (parse(body) or {}).get("id")
    if st != 200 or not pid:
        return 2, "не создан партнёр: POST /partners → %s" % st

    def read():
        st, body, err = call(base, "GET", "/api/v1/partners/%s" % pid, token=tok)
        return (parse(body) or {}) if not err else {}

    def step(label, payload, expect):
        time.sleep(pause)
        st, body, err = call(base, "POST", "/api/v1/partners/%s/profile" % pid, payload, token=tok)
        if err:
            issues.append("%s: стенд недоступен (%s)" % (label, err))
            return None
        doc = read()
        got = {k: doc.get(k) for k in ("name", "gender", "photo", "notes")}
        for k, want in expect.items():
            if got.get(k) != want:
                issues.append("%s: %s=%r, ждали %r (ответ %s)" % (label, k, got.get(k), want, st))
        return got

    step("1. profile без полей профиля", {"gender": "female", "photo": "", "notes": ""}, {"name": name})
    step("2. profile с полями", {"gender": "male", "photo": "TE-PHOTO", "notes": "TE-NOTES"},
         {"name": name, "gender": "male", "photo": "TE-PHOTO", "notes": "TE-NOTES"})
    step("3. явное стирание", {"gender": "male", "photo": "", "notes": ""}, {"name": name, "photo": ""})

    # второй канал: запись + привязка партнёра → recent.lastPartner.name
    st, body, err = call(base, "POST", "/api/v1/journal/records", token=tok)
    rid = (parse(body) or {}).get("id") if not err else None
    if rid:
        call(base, "POST", "/api/v1/journal/records/%s/partners" % rid, {"partnerId": pid}, token=tok)
        time.sleep(pause)
        st, body, err = call(base, "GET", "/api/v1/journal/recent", token=tok)
        lp = (parse(body) or {}).get("lastPartner") or {}
        if lp.get("name") != name:
            issues.append("recent.lastPartner.name=%r, ждали имя %r (канал Journal)" % (lp.get("name"), name))

    if issues:
        for i in issues:
            print("  [FAIL] " + i)
        return 1, "profile-stage1: нарушено %d" % len(issues)
    return 0, "profile-stage1: имя сохранено, поля записаны и стёрты явно, recent.lastPartner.name = имя"


# --- кейс when-normalize -------------------------------------------------------------------

def case_when_normalize(base, phone, pause):
    cid, tok, err = login(base, phone)
    if err:
        return 2, "не измерено: %s" % err
    if not tok:
        return 2, "не измерено: verify не выдал токен (тестовый OTP включён?)"

    issues = []
    st, body, err = call(base, "POST", "/api/v1/journal/records", token=tok)
    if err:
        return 2, "не измерено: %s" % err
    rid = (parse(body) or {}).get("id")
    if not rid:
        return 2, "не создана запись: POST /journal/records → %s" % st

    def set_when(raw_json):
        time.sleep(pause)
        req_body = raw_json.encode()
        req = urllib.request.Request(base.rstrip("/") + "/api/v1/journal/records/%s/when" % rid,
                                     data=req_body, method="POST")
        req.add_header("Content-Type", "application/json")
        req.add_header("Authorization", "Bearer " + tok)
        try:
            with urllib.request.urlopen(req, timeout=25) as r:
                return r.status
        except urllib.error.HTTPError as e:
            return e.code
        except (urllib.error.URLError, OSError):
            return None

    def read_when():
        st, body, err = call(base, "GET", "/api/v1/journal/records/%s" % rid, token=tok)
        return (parse(body) or {}).get("startedAt")

    for label, payload in (("мусор", '"не дата"'), ("пустая строка", '""'), ("пробелы", '"   "'),
                           ("число-строка", '"1234567890"')):
        code = set_when('{"startedAt": %s}' % payload)
        if code != 400:
            issues.append("%s: %s, ждали 400" % (label, code))

    a = set_when('{"startedAt": "2026-11-07T09:00:00+03:00"}')
    va = read_when()
    b = set_when('{"startedAt": "2026-11-07T06:00:00Z"}')
    vb = read_when()
    if a != 200 or b != 200:
        issues.append("валидные ISO: %s / %s, ждали 200/200" % (a, b))
    if va != vb:
        issues.append("нормализация: +03:00 → %r, Z → %r — строки разные" % (va, vb))

    if issues:
        for i in issues:
            print("  [FAIL] " + i)
        return 1, "when-normalize: нарушено %d" % len(issues)
    return 0, "when-normalize: мусор → 400, эквивалентные моменты → одна строка %r" % va


CASES = {"profile-stage1": case_profile_stage1, "when-normalize": case_when_normalize}


def selftest():
    ok = total = 0

    def case(name, got, want):
        nonlocal ok, total
        total += 1
        if got == want:
            ok += 1
        else:
            print("  [FAIL] %s: got %r, want %r" % (name, got, want))

    case("разбор JSON", parse('{"a": 1}'), {"a": 1})
    case("не-JSON → None", parse("не json"), None)
    case("пустое тело → None", parse(""), None)
    case("неизвестный кейс отсутствует в словаре", "nope" in CASES, False)
    case("оба кейса объявлены", sorted(CASES), ["profile-stage1", "when-normalize"])
    print("accept_te_data_points --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API контура (dev/прод)")
    ap.add_argument("--case", choices=sorted(CASES), help="какой кейс прогнать")
    ap.add_argument("--phone", help="номер для логина (по умолчанию случайный из +7999…)")
    ap.add_argument("--pause", type=float, default=5.0, help="пауза после записи перед чтением, с (по умолчанию 5)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if not a.base or not a.case:
        print("::warning::accept_te_data_points: нет --base/--case — не измерено (rc=2)")
        return 2
    phone = a.phone or "+7999%07d" % (int(time.time()) % 10000000)
    rc, line = CASES[a.case](a.base, phone, a.pause)
    print(("::error::" if rc == 1 else "::warning::" if rc == 2 else "::notice::") + line)
    print("квитанция %s: %s | контур %s | rc=%d" % (a.case, "✅" if rc == 0 else "❌", a.base, rc))
    return rc


if __name__ == "__main__":
    sys.exit(main())
