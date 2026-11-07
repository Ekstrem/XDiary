"""check_be092_delivery.py — приёмка `BE-092`: `200`+`delivery`, `409`+`clientId`+`delivery`, `429`.

Критерий @TL (`chat/tl.md` `2026-11-07 01:14`, `→ @TE`): «после починки `deploy-dev` — приёмка по трём строкам:
`200`+`delivery`, `409`+`clientId`+`delivery`, `429`».

Зачем отдельный инструмент: три строки — это **три разных состояния одной операции** `POST /api/v1/clients`
(новый номер / занятый номер / третий вызов подряд), и проверять их руками «по очереди» значит снова получить
«посмотрел глазами». Здесь они проверяются одной командой и печатаются списком с квитанцией.

Коды: `0` — все три строки совпали; `1` — есть расхождение (с указанием, какое); `2` — не измерено (нет `--base`,
стенд недоступен, ответ не JSON).
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

PROBE_PATH = "/api/v1/clients"


def post(base, payload, timeout=25):
    headers = {"Content-Type": "application/json", "User-Agent": "xdiary-be092",
               "Origin": "https://ekstrem.github.io", "Accept": "application/json"}
    req = urllib.request.Request(base.rstrip("/") + PROBE_PATH,
                                 data=json.dumps(payload).encode(), method="POST", headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read(200000).decode("utf-8", "replace"), None
    except urllib.error.HTTPError as e:
        return e.code, (e.read() or b"").decode("utf-8", "replace"), None
    except (urllib.error.URLError, OSError) as e:
        return None, "", f"стенд недоступен: {e}"


def parse(body):
    try:
        return json.loads(body or "")
    except (ValueError, TypeError):
        return None


def delivery_of(doc):
    return (doc or {}).get("delivery") if isinstance(doc, dict) else None


def evaluate(new_resp, dup_resp, third_resp, expect_no_delivery=False):
    """(new, dup, third) — (status, doc) → (issues, stats).

    `expect_no_delivery=True` — тестовый режим (`TESTOTP`): по замыслу `delivery` в теле **нет**
    (`ClientsController.cs:33-34`), поэтому строка 1 проверяет `2xx` **без** `delivery` (а если поле
    вдруг есть — это расхождение: режим не тот, что ожидали), строка 2 — `409`+`clientId`,
    `delivery` при этом необязателен, строка 3 (`429`) не меняется.
    """
    issues = []
    rows = {}

    st, doc = new_resp
    has_delivery = isinstance(delivery_of(doc), dict)
    if expect_no_delivery:
        label1 = "200 (тестовый режим, без delivery)"
        ok1 = isinstance(st, int) and 200 <= st < 300 and not has_delivery
        rows[label1] = ok1
        if not ok1:
            if isinstance(st, int) and 200 <= st < 300:
                issues.append(f"новый номер: `{st}`, но `delivery` в теле **есть** — тестовый режим "
                              f"ожидался без него (поля: {sorted(doc.keys()) if isinstance(doc, dict) else '—'})")
            else:
                issues.append(f"новый номер: ожидали `200` (тестовый режим), получили `{st}`")
    else:
        label1 = "200+delivery"
        ok1 = isinstance(st, int) and 200 <= st < 300 and has_delivery
        rows[label1] = ok1
        if not ok1:
            if isinstance(st, int) and 200 <= st < 300:
                issues.append(f"новый номер: `{st}`, но в теле **нет `delivery`** "
                              f"(поля: {sorted(doc.keys()) if isinstance(doc, dict) else '—'}) — старый образ или `BE-092` не выкачен")
            else:
                issues.append(f"новый номер: ожидали `200`+`delivery`, получили `{st}`")

    st, doc = dup_resp
    has_id = isinstance(doc, dict) and bool(doc.get("clientId"))
    has_delivery = isinstance(delivery_of(doc), dict)
    if expect_no_delivery:
        label2 = "409+clientId (без delivery)"
        ok2 = st == 409 and has_id
    else:
        label2 = "409+clientId+delivery"
        ok2 = st == 409 and has_id and has_delivery
    rows[label2] = ok2
    if not ok2:
        need = "`409` + `clientId`" + ("" if expect_no_delivery else " + `delivery`")
        issues.append(f"занятый номер: ожидали {need}, получили `{st}`, "
                      f"`clientId` {'есть' if has_id else 'нет'}, `delivery` "
                      f"{'есть' if has_delivery else 'нет'}")

    st, _doc = third_resp
    ok3 = st == 429
    rows["429"] = ok3
    if not ok3:
        issues.append(f"третий вызов подряд: ожидали `429` (лимит 1/60с на клиента), получили `{st}`")

    # Бонус-проверка контракта `BE-090`: кода в теле быть не должно ни в одном ответе.
    leaks = []
    for label, (_st, doc) in (("200", new_resp), ("409", dup_resp)):
        if isinstance(doc, dict) and any(k in doc for k in ("otpCode", "otp_code", "otpAttempts")):
            leaks.append(label)
    if leaks:
        issues.append(f"в теле есть OTP-поля в ответах {leaks} — это уже дефект приватности, не только контракта")

    stats = {"lines": rows, "leaks": leaks}
    return issues, stats


def receipt(stats, issues):
    lines = stats["lines"]
    marks = " | ".join(f"{k}: {'✅' if v else '❌'}" for k, v in lines.items())
    return f"квитанция BE-092: {marks} | нарушено: {len(issues)}"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API (dev)")
    ap.add_argument("--phone", help="номер для пробы (по умолчанию случ. в тестовом диапазоне)")
    ap.add_argument("--expect-no-delivery", action="store_true",
                    help="тестовый режим (TESTOTP): `delivery` в теле нет по замыслу — строка 1 проверяет "
                         "`200` без `delivery`, строка 2 — `409`+`clientId` без требования `delivery`")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if not a.base:
        print("::error::check_be092_delivery: нет --base — не измерено (rc=2)")
        return 2

    phone = a.phone or f"+7999{int(time.time()) % 10000000:07d}"
    results = []
    for _ in range(3):
        st, body, err = post(a.base, {"phone": phone})
        if err:
            print(f"::error::check_be092_delivery: {err} — не измерено (rc=2)")
            return 2
        results.append((st, parse(body)))
        print(f"   POST /clients {{phone: {phone[:4]}…{phone[-2:]}}} → {st}")

    issues, stats = evaluate(results[0], results[1], results[2], a.expect_no_delivery)
    for m in issues:
        print(f"::error::check_be092_delivery: {m}")
    print(receipt(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, three, want_rc, must=(), expect_no_delivery=False):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(three[0], three[1], three[2], expect_no_delivery)
        text = "\n".join(issues) + "\n" + receipt(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:220]}")

    good = ((200, {"id": "c1", "delivery": {"channel": "telegram", "ok": True}}),
            (409, {"error": "уже зарегистрирован", "clientId": "c1", "delivery": {"channel": "telegram", "ok": True}}),
            (429, {"error": "лимит"}))
    case("норма: три строки совпали", good, 0, ["200+delivery: ✅", "429: ✅", "нарушено: 0"])

    old = ((200, {"id": "c1"}), (200, {"id": "c1", "otpCode": "123456"}), (200, {"id": "c1"}))
    case("старый образ: нет delivery, повтор 200", old, 1,
         ["нет `delivery`", "ожидали `409`", "ожидали `429`", "OTP-поля"])

    partial = ((200, {"id": "c1", "delivery": {"ok": False}}),
               (409, {"clientId": "c1"}),  # нет delivery
               (429, {}))
    case("409 без delivery — расхождение", partial, 1, ["409", "`delivery` нет"])

    case("429 вместо 200 — тоже расхождение",
         ((429, {}), (409, {"clientId": "c", "delivery": {}}), (429, {})), 1, ["новый номер"])

    # Тестовый режим (@TL 08:46): `delivery` в теле нет по замыслу.
    test_mode_good = ((200, {"id": "c1", "version": 1, "phone": "+79990000000", "verified": False}),
                      (409, {"clientId": "c1"}),
                      (429, {}))
    case("тестовый режим: 200 без delivery + 409+clientId + 429", test_mode_good, 0,
         ["тестовый режим", "нарушено: 0"], expect_no_delivery=True)

    test_mode_wrong = ((200, {"id": "c1", "delivery": {"ok": True}}),
                       (409, {"clientId": "c1"}),
                       (429, {}))
    case("тестовый режим: delivery внезапно есть — расхождение", test_mode_wrong, 1,
         ["`delivery` в теле **есть**"], expect_no_delivery=True)

    case("тестовый режим: повтор всё равно 200 — расхождение", ((200, {"id": "c1"}), (200, {"id": "c1"}), (429, {})), 1,
         ["ожидали `409` + `clientId`"], expect_no_delivery=True)

    total += 1
    line = receipt({"lines": {"200+delivery": True, "409+clientId+delivery": True, "429": True}, "leaks": []}, [])
    if "200+delivery: ✅" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_be092_delivery --selftest: {ok}/{total} "
          f"(200+delivery / 409+clientId+delivery / 429 — три строки приёмки BE-092; "
          f"плюс тестовый режим `--expect-no-delivery`: норма, «delivery внезапно есть», повтор 200)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
