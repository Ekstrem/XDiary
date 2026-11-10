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

  `--case ordering` — канон `edm-canon` §15.1 («как проверять»): частичное событие приходит ПЕРВЫМ
      (строки в справочнике ещё нет) → сервис обязан показать исходное имя, а не `id`/пусто.
      Гоняется 3 раза без пауз и печатает долю нарушений, а не один удачный прогон.

  `--case when-normalize` — `TE-101`, нормализация:
      `POST /journal/records` → `POST .../when` с `2026-11-07T09:00:00+03:00` и с `2026-11-07T06:00:00Z`:
      оба `200` и **одна и та же** сохранённая строка; мусор (`не дата`, ``, `"   "`, `1234567890`) → `400`.

Коды: `0` — все проверки кейса совпали; `1` — расхождение (с указанием, какое); `2` — не измерено (нет `--base`,
стенд недоступен).
"""
import argparse
import json
import re
import sys
import time
from datetime import datetime, timezone
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


def diagnose_name(got, want):
    """Диагноз `recent.lastPartner.name` (TE-102, @TL `2026-11-10 19:55`): `id` и `None` — РАЗНЫЕ дефекты.

    * `ok` — имя совпало;
    * `missing` — поля нет (строка в справочник не попала: запись не дошла до БД);
    * `id` — в справочнике лежит ключ вместо имени;
    * `other` — другое значение.
    """
    if got == want:
        return "ok", "имя совпало"
    if got is None:
        return "missing", "строки в справочнике нет — поле отсутствует (запись не дошла до БД)"
    s = str(got)
    if re.fullmatch(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", s):
        return "id", "в справочнике лежит id вместо имени"
    return "other", "другое имя: %r" % (got,)


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
        _state, _why = diagnose_name(lp.get("name"), name)
        if _state != "ok":
            issues.append("recent.lastPartner.name=%r — %s (канал Journal; ждали имя %r)"
                          % (lp.get("name"), _why, name))

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


def case_ordering(base, phone, pause, iterations=3):
    """Канон `edm-canon` §15.1, «как проверять»: частичное событие приходит **первым**.

    Строки в справочнике ещё нет → сервис обязан показать **исходное** значение (имя), а не `id`/пусто.
    Гоняем `iterations` раз без пауз между созданием и профилем (частичная дельта уходит раньше полного
    события) и печатаем долю случаев с `id` вместо имени — это доля, а не один удачный прогон.

    ВАЖНО (исправлено `2026-11-10 21:5x`): `GET /journal/recent` отдаёт `lastPartner` только когда у клиента
    **есть запись**. Первая версия кейса запись не создавала, поэтому `lastPartner` всегда был пуст и кейс
    краснел по причине «нечего показать», а не по дефекту порядка. Теперь создаётся запись, ставится `when`
    (чтобы она стала самой свежей) и привязывается партнёр — как в `profile-stage1`.
    """
    bad = 0
    details = []
    for i in range(iterations):
        cid, tok, err = login(base, phone if i == 0 else phone + str(i))
        if err:
            return 2, "не измерено: %s" % err
        if not tok:
            return 2, "не измерено: verify не выдал токен (тестовый OTP включён?)"
        name = "TE-ORDER-%d-%d" % (int(time.time()), i)
        st, body, err = call(base, "POST", "/api/v1/partners", {"name": name}, token=tok)
        pid = (parse(body) or {}).get("id")
        if err or not pid:
            return 2, "не создан партнёр на итерации %d: %s" % (i, err or st)
        # без паузы: частичная дельта (профиль) уходит раньше полного события
        call(base, "POST", "/api/v1/partners/%s/profile" % pid,
             {"gender": "male", "photo": "", "notes": ""}, token=tok)

        # `recent.lastPartner` существует только при наличии записи: без неё кейс мерил пустое состояние.
        st, body, err = call(base, "POST", "/api/v1/journal/records", token=tok)
        rid = (parse(body) or {}).get("id")
        if err or not rid:
            return 2, "не создана запись на итерации %d: %s" % (i, err or st)
        call(base, "POST", "/api/v1/journal/records/%s/when" % rid,
             {"startedAt": datetime.now(timezone.utc).isoformat()}, token=tok)
        call(base, "POST", "/api/v1/journal/records/%s/partners" % rid, {"partnerId": pid}, token=tok)

        time.sleep(pause)
        st, body, err = call(base, "GET", "/api/v1/journal/recent", token=tok)
        lp = (parse(body) or {}).get("lastPartner") or {}
        got = lp.get("name")
        _state, _why = diagnose_name(got, name)
        if _state != "ok":
            bad += 1
            details.append("итерация %d: name=%r — %s (ждали %r)" % (i + 1, got, _why, name))

    if bad:
        for d in details:
            print("  [FAIL] " + d)
        return 1, "ordering: id/пусто вместо имени в %d из %d" % (bad, iterations)
    return 0, "ordering: %d/%d — частичное событие первым не ломает имя (канон §15.1)" % (iterations, iterations)


CASES = {
    "profile-stage1": case_profile_stage1,
    "when-normalize": case_when_normalize,
    "ordering": case_ordering,
}


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
    case("все кейсы объявлены", sorted(CASES), ["ordering", "profile-stage1", "when-normalize"])
    case("диагноз: имя совпало", diagnose_name("TE-X", "TE-X")[0], "ok")
    case("диагноз: None — строки нет", diagnose_name(None, "TE-X")[0], "missing")
    case("диагноз: uuid — id вместо имени",
         diagnose_name("bcc865b5-177a-4446-a0b8-27cf8aae9541", "TE-X")[0], "id")
    case("диагноз: другое имя", diagnose_name("чужое", "TE-X")[0], "other")
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
    # TE-115 (@TL `2026-11-10 17:05`): баннер старта — норма для гейт-скриптов: лог самодостаточен,
    # «пусто» отличимо от «нарушено».
    print("::notice::accept_te_data_points: старт · case=%s · base=%s · pause=%.1fs"
          % (a.case or "—", a.base or "—", a.pause), flush=True)
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
