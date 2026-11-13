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
import io
import json
import os
import pathlib
import re
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PROBE_PATH = "/api/v1/clients"
# TE-094: путь к контракту считаем от КОРНЯ РЕПОЗИТОРИЯ, а не от cwd — иначе `--selftest`
# проходит только когда его зовут из корня (в CI так и зовут, поэтому дефект был невидим).
REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTRACT = str(REPO_ROOT / "04-architecture" / "api-index.yaml")
OPERATION = "Client.Register"
# Ключи, похожие на секрет: их в теле быть не должно НИКОГДА, независимо от контракта.
SECRET_KEYS = ("otpcode", "otp_code", "otpattempts", "code", "pin", "password", "secret", "token")
# Поля, которых в ответе быть не должно. `code` — не проверяем слепо: в домене есть и другие `code`
# (коды ошибок), поэтому ищем только OTP-специфичные имена.
# Поля, которых в ответе быть НЕ должно: по ним получают доступ — код и счётчик попыток.
LEAK_FIELDS = ("otpCode", "otpcode", "otp_code", "otpAttempts")
# Ловим и JSON (`"otpCode":`), и не-JSON (`otpCode=`, `otpCode :`) — иначе «не разобрали» выглядело бы как «чисто».
LEAK_RX = re.compile(r'\b(otpCode|otp_code|otpAttempts)\b\s*[:=]', re.I)
# Поле, которое контракт РАЗРЕШАЕТ в теле 200: `04-architecture/api-index.yaml:177` —
# «новый номер: тело id, version, phone, otpExpiresAt, verified, delivery». По метке времени JWT
# не получить, поэтому это НЕ утечка: печатаем notice и в нарушения не берём.
CONTRACT_ALLOWED = ("otpExpiresAt", "otpExpires")
ALLOWED_RX = re.compile(r'\b(otpExpiresAt|otpExpires)\b\s*[:=]', re.I)


def scan_body(text):
    """(утечки, разрешённые контрактом поля) — по ключам JSON, рекурсивно."""
    leaks, allowed = [], []

    def walk(node, path=""):
        if isinstance(node, dict):
            for k, v in node.items():
                low = k.lower()
                if low in [f.lower() for f in LEAK_FIELDS]:
                    leaks.append(f"{path}{k}")
                elif low in [f.lower() for f in CONTRACT_ALLOWED]:
                    allowed.append(f"{path}{k}")
                walk(v, f"{path}{k}.")
        elif isinstance(node, list):
            for i, v in enumerate(node):
                walk(v, f"{path}[{i}].")

    try:
        walk(json.loads(text))
        return leaks, allowed
    except (ValueError, TypeError):
        # не JSON — ищем по тексту, чтобы «не разобрали» не выглядело как «чисто»
        return ([m.group(1) for m in LEAK_RX.finditer(text or "")],
                [m.group(1) for m in ALLOWED_RX.finditer(text or "")])


def contract_allowed_fields(contract_path, operation=OPERATION, status=200):
    """Поля, которые контракт РАЗРЕШАЕТ в теле `200`, из `api-index.yaml` (единственный источник правды).

    Возвращает (set полей | None, где искать). None — контракт не найден/не разобран: тогда проверка
    «поле не из контракта» невозможна, и это надо сказать вслух, а не считать «всё разрешено».
    """
    p = pathlib.Path(contract_path)
    if not p.exists():
        return None, f"нет {contract_path}"
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    start = next((i for i, l in enumerate(lines) if f"operationId: {operation}" in l), None)
    if start is None:
        return None, f"в {contract_path} нет operationId: {operation}"
    if status != 200:
        # @TL 2026-11-13 09:10: для не-200 форма описана в `error_body` рядом с `codes:`.
        # Нет `error_body` — форма не известна, и это «не измерено», а не «поле вне контракта».
        block = lines[start:start + 60]
        eb = next((l for l in block if l.strip().startswith("error_body:")), None)
        if not eb:
            return None, f"в контракте нет error_body для кода {status} — не измерено"
        m = re.search(r"\[([^\]]*)\]", eb)
        tokens = set()
        if m:
            tokens = set(x.strip().strip("'\"").lower() for x in m.group(1).split(",") if x.strip())
        return (tokens or None), f"error_body (код {status}) из {contract_path}"

    codes = next((l for l in lines[start:start + 12] if l.strip().startswith("codes:")), None)
    if not codes:
        return None, "у операции нет строки codes:"
    body = codes.split("{", 1)[1] if "{" in codes else codes
    parts = re.split(r",\s*(?=\d{3}\s*:)", body)
    ok_part = next((x for x in parts if x.strip().startswith(("200:", "200 :"))), None)
    if not ok_part:
        return None, "нет описания кода 200"
    tokens = set(w.lower() for w in re.findall(r"[A-Za-z][A-Za-z0-9]{1,}", ok_part))
    return tokens, f"{contract_path} (код 200: {ok_part.split(':', 1)[1].strip()[:80]}…)"


def unexpected_fields(body_keys, allowed):
    """Поля тела, которых контракт не разрешает (кроме секретных — они ловятся отдельно)."""
    if not allowed:
        return []
    return sorted(k for k in body_keys
                  if k.lower() not in allowed and k.lower() not in SECRET_KEYS)


def evaluate(status, body, transient_gone=(), known_leak=(), allowed_fields=None):
    """(issues, stats). `status` — код ответа, `body` — текст ответа."""
    issues = []
    leaked, allowed = scan_body(body or "")
    if leaked:
        issues.append(f"`POST {PROBE_PATH}` вернул OTP-поле(я) {sorted(set(leaked))} — "
                      f"по этому коду можно получить JWT владельца (BE-090), это доступ к чужим данным")
    # «поле, которого нет в контракте» — второй, независимый признак (принцип @AR 22:34):
    # контракт разрешает — гейт не спорит; контракт не разрешает — это и есть утечка формы.
    unexpected = []
    try:
        doc = json.loads(body or "")
        if isinstance(doc, dict) and allowed_fields is not None:
            unexpected = unexpected_fields(list(doc.keys()), allowed_fields)
    except (ValueError, TypeError):
        pass
    for k in unexpected:
        issues.append(f"в теле есть поле `{k}`, которого НЕТ в контракте (`{OPERATION}`, код 200) — "
                      f"это утечка формы: сервис отдаёт больше, чем разрешено индексом")
    stats = {"status": status, "leaked": sorted(set(leaked)),
             "allowed": sorted(set(allowed)),
             "unexpected": unexpected,
             "len": len((body or "").strip())}
    return issues, stats


def receipt_line(stats, issues):
    allowed = stats.get("allowed") or []
    return (f"квитанция: POST {PROBE_PATH} → {stats['status']} | тело {stats['len']} симв. | "
            f"OTP-полей: {len(stats['leaked'])}{(' ' + str(stats['leaked'])) if stats['leaked'] else ''} | "
            f"разрешено контрактом: {len(allowed)}{(' ' + str(allowed)) if allowed else ''} | "
            f"вне контракта: {len(stats.get('unexpected') or [])} | "
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
    ap.add_argument("--contract", default=CONTRACT, help="путь к api-index.yaml (источник правды по форме)")
    ap.add_argument("--compare", nargs=2, metavar=("BASE_A", "BASE_B"),
                    help="сравнить два стенда и назвать асимметрию (например «dev отстаёт от прода»)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    def pick_allowed(status):
        """Форма тела по ФАКТИЧЕСКОМУ коду ответа (@TL 2026-11-13 09:10).

        200 → `codes:`; иначе → `error_body:`; нет формы под код → None и это «не измерено».
        """
        fields, where = contract_allowed_fields(a.contract, status=status)
        if fields is None:
            print(f"::warning::check_otp_leak: контракт не дал форму ({where}) — проверка «поле вне "
                  f"контракта» не выполняется для кода {status}")
        else:
            print(f"::notice::check_otp_leak: разрешённые поля взяты из контракта — {where}")
        return fields

    if a.compare:
        rows = []
        for label, base in (("A", a.compare[0]), ("B", a.compare[1])):
            st, body, err = probe(base, a.phone)
            if err:
                print(f"::error::check_otp_leak: {label} ({base}) — {err} — не измерено (rc=2)")
                return 2
            iss, stt = evaluate(st, body, allowed_fields=pick_allowed(st))
            rows.append((label, base, st, stt, iss))
            print(f"   {label} {base}: HTTP {st} | OTP-полей {len(stt['leaked'])} {stt['leaked'] or ''} | "
                  f"вне контракта {len(stt.get('unexpected') or [])} | rc={1 if iss else 0}")
        (la, _ba, _sa, ta, ia), (lb, _bb, _sb, tb, ib) = rows
        if bool(ia) != bool(ib):
            behind, ahead = (la, lb) if ia else (lb, la)
            behind_leaks = len(ta["leaked"]) if ia else len(tb["leaked"])
            print(f"квитанция сравнения: {behind} отстаёт от {ahead} по `client` "
                  f"(у {behind} OTP-полей {behind_leaks}, у {ahead} — 0)")
            return 1
        print(f"квитанция сравнения: стенды одинаковы по форме ответа "
              f"(OTP-полей {len(ta['leaked'])} / {len(tb['leaked'])})")
        return 0

    if a.body is not None:
        status, body = a.status, a.body
    else:
        if not a.base:
            print("::error::check_otp_leak: нет --base — не измерено (rc=2)")
            return 2
        status, body, err = probe(a.base, a.phone)
        if err:
            print(f"::error::check_otp_leak: {err} — не измерено (rc=2)")
            return 2

    allowed_fields = pick_allowed(status)
    if allowed_fields is None and status != 200:
        print(f"::error::check_otp_leak: в контракте нет формы для кода {status} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(status, body, allowed_fields=allowed_fields)
    for m in issues:
        print(f"::error::check_otp_leak: {m}")
    if stats.get("allowed"):
        print(f"::notice::check_otp_leak: в теле есть {stats['allowed']} — это разрешено контрактом "
              f"(api-index.yaml:177), JWT по метке времени не получить, утечкой не считаем")
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

    case("утечка вложена в объект", 200, '{"client":{"id":"c1","otpCode":"123456"}}', 1,
         ["client.otpCode"])

    case("400 с OTP в теле — всё равно утечка", 400, '{"errors":{"otpCode":["required"]}}', 1, ["otpCode"])

    case("не-JSON тело с otpCode — не маскируется", 200, "otpCode=123456", 1, ["otpCode"])

    case("пустое тело (401 без тела) — нарушений нет", 401, "", 0, ["нарушено: 0"])

    # Контракт (`api-index.yaml:177`) разрешает `otpExpiresAt` в теле 200 — это НЕ утечка.
    case("otpExpiresAt разрешён контрактом — не красный", 200,
         '{"id":"c1","version":1,"phone":"+7","otpExpiresAt":"2026-11-06T19:31:37Z","verified":false}', 0,
         ["OTP-полей: 0", "разрешено контрактом: 1"])

    case("факт прода после выкатки: тело без кода", 200,
         '{"id":"c1","version":1,"phone":"+7","otpExpiresAt":"2026-11-06T19:31:37Z","verified":false}', 0,
         ["нарушено: 0"])

    case("код вернулся вместе с otpExpiresAt — снова красный", 200,
         '{"id":"c1","otpCode":"123456","otpExpiresAt":"2026-11-06T19:31:37Z"}', 1,
         ["otpCode"])

    # Принцип @AR 22:34: «разрешённое берём из контракта», а «поле вне контракта» — утечка формы.
    # @TL 2026-11-13 09:10: форма по ФАКТИЧЕСКОМУ коду — 409 берёт error_body, иначе «не измерено».
    tmp_contract = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-otp-contract.yaml")
    io.open(tmp_contract, "w", encoding="utf-8").write(
        "operations:\n"
        "  - operationId: Client.Register\n"
        "    codes: {200: \"ok — id, version, phone, otpExpiresAt, verified, delivery\", 409: \"конфликт\"}\n"
        "    error_body: [error, clientId, delivery]\n")
    got_fields, where = contract_allowed_fields(tmp_contract, status=409)
    total += 1
    if got_fields == {"error", "clientid", "delivery"}:
        ok += 1
    else:
        print(f"  [FAIL] форма 409 из error_body: ждали error/clientId/delivery, получили {got_fields} ({where})")

    io.open(tmp_contract, "w", encoding="utf-8").write(
        "operations:\n"
        "  - operationId: Client.Register\n"
        "    codes: {200: \"ok\"}\n")
    got2, where2 = contract_allowed_fields(tmp_contract, status=409)
    total += 1
    if got2 is None and "не измерено" in (where2 or ""):
        ok += 1
    else:
        print(f"  [FAIL] нет error_body ⇒ должно быть «не измерено», получили {got2} ({where2})")
    try:
        os.remove(tmp_contract)
    except OSError:
        pass

    allowed = {"id", "version", "phone", "otpexpiresat", "verified", "delivery"}
    total += 1
    iss, st = evaluate(200, '{"id":"c1","version":1,"phone":"+7","otpExpiresAt":"t","verified":false}', allowed_fields=allowed)
    if not iss and st["unexpected"] == []:
        ok += 1
    else:
        print(f"  [FAIL] контракт разрешает — красным быть не должно: {iss}")

    total += 1
    iss, st = evaluate(200, '{"id":"c1","root":{"x":1},"commandName":"","otpExpiresAt":"t"}', allowed_fields=allowed)
    if iss and st["unexpected"] == ["commandName", "root"]:
        ok += 1
    else:
        print(f"  [FAIL] поля вне контракта: unexpected={st.get('unexpected')}, issues={iss}")

    total += 1
    checked, where = contract_allowed_fields(pathlib.Path(CONTRACT))
    if checked and "otpexpiresat" in checked and "delivery" in checked:
        ok += 1
    else:
        print(f"  [FAIL] разбор контракта: {checked} ({where})")

    total += 1
    missing, where = contract_allowed_fields(pathlib.Path("no/such/file.yaml"))
    if missing is None and "нет" in where:
        ok += 1
    else:
        print(f"  [FAIL] отсутствующий контракт должен давать None: {missing} / {where}")

    total += 1
    line = receipt_line({"status": 200, "leaked": [], "allowed": [], "unexpected": [], "len": 10}, [])
    if "OTP-полей: 0" in line and "нарушено: 0" in line and "POST /api/v1/clients" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_otp_leak --selftest: {ok}/{total} "
          f"(OTP-поле в теле ответа = rc=1: код даёт JWT владельца, BE-090)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
