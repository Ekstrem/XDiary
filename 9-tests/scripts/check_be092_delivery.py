"""check_be092_delivery.py — приёмка `BE-092`: `200`+`delivery`, `409`+`clientId`+`delivery`, `429`.

Критерий @TL (`chat/tl.md` `2026-11-07 01:14`, `→ @TE`): «после починки `deploy-dev` — приёмка по трём строкам:
`200`+`delivery`, `409`+`clientId`+`delivery`, `429`».

Зачем отдельный инструмент: три строки — это **три разных состояния одной операции** `POST /api/v1/clients`
(новый номер / занятый номер / третий вызов подряд), и проверять их руками «по очереди» значит снова получить
«посмотрел глазами». Здесь они проверяются одной командой и печатаются списком с квитанцией.

Коды: `0` — все три строки совпали; `1` — есть расхождение (с указанием, какое); `2` — не измерено (нет `--base`,
стенд недоступен, ответ не JSON). Уровень аннотации соответствует коду (`@TL 08:26`): расхождения печатаются
`::error::`, «не измерено» — `::warning::`, чтобы транзиентная сеть не выглядела как дефект продукта и шаг CI
не приходилось обходить.
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


def needs_limit_wait(status, auto_wait=True):
    """`409` (занятый номер) в коде проверяется ПОСЛЕ лимитера (`TryAcquire` → `GetByPhoneAsync`),
    поэтому второй вызов сразу после первого даёт `429`, а не `409`. Если это произошло — ждём окно
    лимитера и повторяем пробу дубля; иначе строка `409` «не измерена» по причине порядка в коде,
    а не по дефекту контракта (`@TE 08:52`, найдено прогоном на dev)."""
    return int(status or 0) == 429 and bool(auto_wait)


def mode_implies_no_delivery(mode, flag):
    """`--mode test-otp` и `--expect-no-delivery` — одно и то же требование к телу (`@DO 08:48`)."""
    return bool(flag) or (mode or "strict") == "test-otp"


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
                              f"ожидался без него (ключи: {body_keys(doc)})")
            else:
                issues.append(f"новый номер: ожидали `200` (тестовый режим), получили `{st}` "
                              f"(ключи: {body_keys(doc)})")
    else:
        label1 = "200+delivery"
        ok1 = isinstance(st, int) and 200 <= st < 300 and has_delivery
        rows[label1] = ok1
        if not ok1:
            if isinstance(st, int) and 200 <= st < 300:
                issues.append(f"новый номер: `{st}`, но в теле **нет `delivery`** (ключи: {body_keys(doc)}) "
                              f"— старый образ или `BE-092` не выкачен")
            else:
                issues.append(f"новый номер: ожидали `200`+`delivery`, получили `{st}` "
                              f"(ключи: {body_keys(doc)})")

    # @AR `2026-11-10 15:33`: «условно по предусловию». Предусловие — **строка создана** (первый вызов дал 2xx),
    # а не «всё сошлось»: на старом образе `200` без `delivery` строка создана, поэтому повтор ИЗМЕРЯЕМ
    # (и он обязан дать `409`). Не измеряем только тогда, когда первый вызов не создал строку (`429` и пр.).
    precondition_failed = not (isinstance(st, int) and 200 <= st < 300)

    st, doc = dup_resp
    has_id = isinstance(doc, dict) and bool(doc.get("clientId"))
    has_delivery = isinstance(delivery_of(doc), dict)
    if expect_no_delivery:
        label2 = "409+clientId (без delivery)"
        ok2 = st == 409 and has_id
    else:
        label2 = "409+clientId+delivery"
        ok2 = st == 409 and has_id and has_delivery
    if precondition_failed:
        rows[label2] = None  # не измерено, а не «провал»
        issues.append("предусловие не выполнено: новый номер не создан — повтор не измерен "
                      "(первый вызов не дал 2xx)")
    else:
        rows[label2] = ok2
        if not ok2:
            need = "`409` + `clientId`" + ("" if expect_no_delivery else " + `delivery`")
            issues.append(f"занятый номер: ожидали {need}, получили `{st}`, "
                          f"`clientId` {'есть' if has_id else 'нет'}, `delivery` "
                          f"{'есть' if has_delivery else 'нет'}, ключи: {body_keys(doc)}")

    st, _doc = third_resp
    ok3 = st == 429
    if precondition_failed:
        rows["429"] = None
    else:
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

    stats = {"lines": rows, "leaks": leaks, "precondition_failed": precondition_failed}
    return issues, stats


def body_keys(doc):
    """Ключи тела без значений (@DO `2026-11-10 16:18`): именно это отличает «дефект контракта» от
    «не тот образ». Значения НЕ печатаем — в теле телефон/`delivery`-адрес (PII), репозиторий публичный."""
    if not isinstance(doc, dict):
        return "—"
    return "[" + ", ".join(sorted(doc.keys())) + "]"


def receipt(stats, issues):
    lines = stats["lines"]
    marks = " | ".join(f"{k}: {'⏸ не измерено' if v is None else ('✅' if v else '❌')}"
                       for k, v in lines.items())
    return f"квитанция BE-092: {marks} | нарушено: {len(issues)}"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API (dev)")
    ap.add_argument("--phone", help="номер для пробы (по умолчанию случ. в тестовом диапазоне)")
    ap.add_argument("--expect-no-delivery", action="store_true",
                    help="тестовый режим (TESTOTP): `delivery` в теле нет по замыслу — строка 1 проверяет "
                         "`200` без `delivery`, строка 2 — `409`+`clientId` без требования `delivery`")
    ap.add_argument("--mode", choices=["strict", "test-otp"], default="strict",
                    help="то же самое именем, которое просил @DO: `test-otp` == `--expect-no-delivery` "
                         "(для шага CI в deploy.yml, где вызывается `--mode test-otp`)")
    ap.add_argument("--limit-wait", type=float, default=70.0,
                    help="сколько ждать окно лимитера, если `409` перекрыт `429`; по умолчанию 70 "
                         "(измерено @DO 2026-11-10 15:31: пауза 62 с дала 200, 65 с — 409; запас 5 с, "
                         "а не 1 — секунда запаса не стабильность, @AR 15:55)")
    ap.add_argument("--no-wait", action="store_true",
                    help="не ждать окно лимитера: тогда строка `409` будет «не измерена», а не проверена")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if not a.base:
        print("::warning::check_be092_delivery: нет --base — не измерено (rc=2)")
        return 2

    phone = a.phone or f"+7998{int(time.time()) % 10000000:07d}"
    # DO-121 (@TL 2026-11-13 00:32): префикс +7998, а не +7999 — иначе очистка E2E
    # (cleanup-e2e-dev.sh --apply --clients, phone LIKE '+7999%') удаляет только что
    # созданного клиента между пробами, и вторая проба даёт 200 вместо 409.


    def one(label):
        st, body, err = post(a.base, {"phone": phone})
        if err:
            print(f"::warning::check_be092_delivery: {err} — не измерено (rc=2)")
            return None
        print(f"   POST /clients {{phone: {phone[:4]}…{phone[-2:]}}} → {st}   ({label})")
        return (st, parse(body))

    first = one("новый номер")
    if first is None:
        return 2
    dup = one("занятый номер")
    if dup is None:
        return 2
    if needs_limit_wait(dup[0], not a.no_wait):
        print("::notice::check_be092_delivery: повтор дал `429` раньше проверки дубля "
              "(лимитер в коде стоит до `GetByPhoneAsync`) — ждём %.0f с и повторяем пробу `409`"
              % a.limit_wait)
        time.sleep(a.limit_wait)
        dup = one("занятый номер, после окна лимитера")
        if dup is None:
            return 2
    third = one("третий вызов подряд")
    if third is None:
        return 2
    results = [first, dup, third]

    issues, stats = evaluate(results[0], results[1], results[2],
                             mode_implies_no_delivery(a.mode, a.expect_no_delivery))
    for m in issues:
        print(f"::error::check_be092_delivery: {m}")
    print(receipt(stats, issues))
    if stats.get("precondition_failed"):
        # @AR 15:33: предусловие не выполнено -> «не измерено» (2), а не «нарушено» (1).
        print("::warning::check_be092_delivery: предусловие не выполнено — повтор и лимит не измерены (rc=2)")
        return 2
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, three, want_rc, must=(), expect_no_delivery=False):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(three[0], three[1], three[2], expect_no_delivery)
        text = "\n".join(issues) + "\n" + receipt(stats, issues)
        rc = 2 if stats.get("precondition_failed") else (1 if issues else 0)
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:220]}")

    good = ((200, {"id": "c1", "delivery": {"channel": "telegram", "ok": True}}),
            (409, {"error": "уже зарегистрирован", "clientId": "c1", "delivery": {"channel": "telegram", "ok": True}}),
            (429, {"error": "лимит"}))
    case("норма: три строки совпали", good, 0, ["200+delivery: ✅", "429: ✅", "нарушено: 0"])
    # предусловие: первый вызов не создал строку -> rc=2, повтор и лимит «не измерено»
    case("предусловие: первый 429 -> не измерено",
         ((429, {"error": "лимит"}), (200, {}), (429, {})), 2,
         ["409+clientId+delivery: ⏸ не измерено", "429: ⏸ не измерено"])

    old = ((200, {"id": "c1"}), (200, {"id": "c1", "otpCode": "123456"}), (200, {"id": "c1"}))
    case("старый образ: нет delivery, повтор 200", old, 1,
         ["нет `delivery`", "ожидали `409`", "ожидали `429`", "OTP-поля"])

    partial = ((200, {"id": "c1", "delivery": {"ok": False}}),
               (409, {"clientId": "c1"}),  # нет delivery
               (429, {}))
    case("409 без delivery — расхождение", partial, 1, ["409", "`delivery` нет"])

    # Контракт изменён осознанно (@AR `2026-11-10 15:33`): `429` на первом вызове — это лимитер,
    # то есть строка не создана и повтор НЕ ИЗМЕРЕН. Поэтому `rc=2` («не измерено»), а не `1`.
    # Раньше здесь ждали `1` — и это делало прогон вечно красным на контуре с включённым лимитером.
    case("429 на первом вызове — не измерено (rc=2)",
         ((429, {}), (409, {"clientId": "c", "delivery": {}}), (429, {})), 2,
         ["новый номер", "409+clientId+delivery: ⏸ не измерено"])

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

    # Порядок «лимитер до дубля»: второй вызов сразу = 429 → нужно дождаться окна и повторить.
    total += 1
    if (needs_limit_wait(429) is True and needs_limit_wait(200) is False
            and needs_limit_wait(429, auto_wait=False) is False):
        ok += 1
    else:
        print("  [FAIL] needs_limit_wait: 429 должен требовать ожидания окна, 200 — нет, --no-wait отключает")

    # Имя режима, которое просил @DO (08:48): `--mode test-otp`.
    total += 1
    if (mode_implies_no_delivery("test-otp", False) is True
            and mode_implies_no_delivery("strict", False) is False
            and mode_implies_no_delivery("strict", True) is True):
        ok += 1
    else:
        print("  [FAIL] --mode test-otp не сводится к --expect-no-delivery")

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
