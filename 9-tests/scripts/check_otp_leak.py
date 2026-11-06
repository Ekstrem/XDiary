"""check_otp_leak.py — OTP не должен утекать в ответе `POST /api/v1/clients`.

Реальный дефект (`BE-090`, 2026-11-06, P0 «доступ к чужим дневникам»): `POST /clients` с **уже существующим**
номером возвращал `200`, тот же `id` и **снова `otpCode`** (плюс `otpAttempts: 0`). Сервер выдаёт свежий код
для любого известного номера, а `POST /clients/{id}/verify` с этим кодом даёт **JWT владельца** — то есть
чужой дневник. Поэтому проверка «в теле ответа нет OTP-полей» — это не косметика, а контроль доступа.

Гейт проверяет **тело ответа**, а не только код: `200` с `otpCode` — красный, `200` без OTP-полей — зелёный.

Коды: `0` — OTP-полей в ответе нет; `1` — утечка; `2` — не измерено (нет `--base`, стенд недоступен,
ответ не JSON). «Не измерено» никогда не считается зелёным.
"""

import argparse
import json
import re
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PROBE_PATH = "/api/v1/clients"
# Поля, которых в ответе быть не должно. `code` — не проверяем слепо: в домене есть и другие `code`
# (коды ошибок), поэтому ищем только OTP-специфичные имена.
LEAK_FIELDS = ("otpCode", "otpcode", "otp_code", "otpAttempts", "otpExpiresAt", "otpExpires")
# Ловим и JSON (`"otpCode":`), и не-JSON (`otpCode=`, `otpCode :`) — иначе «не разобрали» выглядело бы как «чисто».
LEAK_RX = re.compile(r'\b(otpCode|otp_code|otpAttempts|otpExpiresAt|otpExpires)\b\s*[:=]', re.I)


def scan_body(text):
    """Возвращает список найденных OTP-полей (по ключам JSON, рекурсивно)."""
    found = []

    def walk(node, path=""):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in LEAK_FIELDS or k.lower() in [f.lower() for f in LEAK_FIELDS]:
                    found.append(f"{path}{k}")
                walk(v, f"{path}{k}.")
        elif isinstance(node, list):
            for i, v in enumerate(node):
                walk(v, f"{path}[{i}].")

    try:
        walk(json.loads(text))
        return found
    except (ValueError, TypeError):
        # не JSON — ищем по тексту, чтобы «не разобрали» не выглядело как «чисто»
        return [m.group(1) for m in LEAK_RX.finditer(text or "")]


def evaluate(status, body, transient_gone=(), known_leak=()):
    """(issues, stats). `status` — код ответа, `body` — текст ответа."""
    issues = []
    leaked = scan_body(body or "")
    if leaked:
        issues.append(f"`POST {PROBE_PATH}` вернул OTP-поле(я) {sorted(set(leaked))} — "
                      f"по этому коду можно получить JWT владельца (BE-090), это доступ к чужим данным")
    stats = {"status": status, "leaked": sorted(set(leaked)),
             "len": len((body or "").strip())}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: POST {PROBE_PATH} → {stats['status']} | тело {stats['len']} симв. | "
            f"OTP-полей: {len(stats['leaked'])}{(' ' + str(stats['leaked'])) if stats['leaked'] else ''} | "
            f"нарушено: {len(issues)}")


def probe(base, phone, timeout=20):
    req = urllib.request.Request(
        base.rstrip("/") + PROBE_PATH,
        data=json.dumps({"phone": phone}).encode(),
        method="POST",
        headers={"Content-Type": "application/json", "User-Agent": "xdiary-otp-probe"},
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read(200000).decode("utf-8", "replace"), None
    except urllib.error.HTTPError as e:  # 4xx тоже информативен: тело всё равно смотрим
        return e.code, (e.read() or b"").decode("utf-8", "replace"), None
    except (urllib.error.URLError, OSError) as e:
        return None, "", f"стенд недоступен: {e}"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API (прод или dev)")
    ap.add_argument("--phone", default="+70000000000", help="номер для пробы")
    ap.add_argument("--body", help="офлайн-проверка: подставить тело ответа")
    ap.add_argument("--status", type=int, help="офлайн-проверка: подставить код ответа")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.body is not None:
        status, body, err = a.status, a.body, None
    else:
        if not a.base:
            print("::error::check_otp_leak: нет --base — не измерено (rc=2)")
            return 2
        status, body, err = probe(a.base, a.phone)
        if err:
            print(f"::error::check_otp_leak: {err} — не измерено (rc=2)")
            return 2

    issues, stats = evaluate(status, body)
    for m in issues:
        print(f"::error::check_otp_leak: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, status, body, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(status, body)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    case("дефект BE-090: otpCode в ответе", 200,
         '{"id":"c1","otpCode":"123456","otpAttempts":0}', 1,
         ["otpCode", "доступ к чужим данным"])

    case("норма: в ответе нет OTP-полей", 200,
         '{"id":"c1","version":1,"phone":"+70000000000","verified":false}', 0, ["OTP-полей: 0"])

    case("утечка вложена в объект", 200, '{"client":{"id":"c1","otpExpiresAt":"2026-11-06T00:00:00Z"}}', 1,
         ["client.otpExpiresAt"])

    case("400 с OTP в теле — всё равно утечка", 400, '{"errors":{"otpCode":["required"]}}', 1, ["otpCode"])

    case("не-JSON тело с otpCode — не маскируется", 200, "otpCode=123456", 1, ["otpCode"])

    case("пустое тело (401 без тела) — нарушений нет", 401, "", 0, ["нарушено: 0"])

    total += 1
    line = receipt_line({"status": 200, "leaked": [], "len": 10}, [])
    if "OTP-полей: 0" in line and "нарушено: 0" in line and "POST /api/v1/clients" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_otp_leak --selftest: {ok}/{total} "
          f"(OTP-поле в теле ответа = rc=1: код даёт JWT владельца, BE-090)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
