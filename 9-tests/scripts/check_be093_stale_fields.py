"""check_be093_stale_fields.py — «AddPartner не должен затирать остальные поля записи».

Дефект (`BE-093`, найден @TL 2026-11-06 21:37): `JournalEventConsumer.cs:75-77` делал
`var read = JournalProjection.Project(domainEvent); if (read is not null) await _readStore.UpsertAsync(read);`
— то есть **полную замену строки** моделью, где `Project` заполняет только 4 поля (`Id`, `ClientId`,
`Partners`, `StartedAt`), а в `JournalReadModel` их ~20 (`DurationMin`, `Place`, `Positions`, `Tags`,
`Costumes`, `Toys`, `Fantasies`, `Contraceptives`, `Orgasms`, `Garmin`, `CreatedAt/UpdatedAt`, `Deleted`).

Следствие: любое событие, меняющее `Root` (`AddPartner`), **затирает** остальные поля значениями по умолчанию.
Это «зелёный путь, который теряет данные»: ошибок в логах нет, строки просто теряют содержимое — и это же
делало `BE-077` нерасследуемым.

Проверка, которую просил @TL (`21:37`, `→ @DO + @TE`): запись с заполненными `Place`/`Tags` → `AddPartner` →
чтение; «поля на месте» или «пусто» — факт за 2 минуты.

Коды: `0` — поля сохранились; `1` — затерты (дефект подтверждён); `2` — не измерено (нет `--base`/`--token`,
стенд недоступен, ответ не JSON).
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# Поля, которые обязаны пережить `AddPartner`. Проверяем только те, что легко заполнить в одном запросе.
WATCH_FIELDS = ("place", "tags")


def http(url, method="GET", token=None, payload=None, timeout=25):
    data = json.dumps(payload).encode() if payload is not None else None
    headers = {"User-Agent": "xdiary-be093", "Accept": "application/json"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read(300000).decode("utf-8", "replace"), None
    except urllib.error.HTTPError as e:
        return e.code, (e.read() or b"").decode("utf-8", "replace"), None
    except (urllib.error.URLError, OSError) as e:
        return None, "", f"стенд недоступен: {e}"


def _norm(v):
    """Нормализация для сравнения: список тегов — множество строк, место — строка."""
    if isinstance(v, list):
        return sorted(str(x).strip().lower() for x in v)
    if isinstance(v, str):
        return v.strip().lower()
    return v


def _get(doc, field):
    """Достаёт поле из ответа: поддерживаем и «плоский», и вложенный (`record`/`data`/`item`) вид."""
    if not isinstance(doc, dict):
        return None
    for key in (field, field.lower()):
        if key in doc:
            return doc[key]
    for wrapper in ("record", "data", "item", "journalRecord"):
        inner = doc.get(wrapper)
        if isinstance(inner, dict):
            for key in (field, field.lower()):
                if key in inner:
                    return inner[key]
    return None


def evaluate(before, after):
    """before/after — распарсенные ответы чтения до и после AddPartner. → (issues, stats)."""
    issues = []
    rows = []
    for f in WATCH_FIELDS:
        b = _get(before, f)
        a = _get(after, f)
        if b in (None, "", [], {}):
            continue  # поле не было заполнено — на этом поле ничего не докажем
        same = _norm(b) == _norm(a)
        rows.append((f, b, a, same))
        if not same:
            issues.append(f"поле `{f}` было {b!r}, после `AddPartner` стало {a!r} — "
                          f"событие затирает чужие поля (полная замена строки вместо патча, BE-093)")
    stats = {"checked": [r[0] for r in rows],
             "same": sum(1 for r in rows if r[3]),
             "wiped": sum(1 for r in rows if not r[3])}
    return issues, stats


def receipt_line(stats, issues):
    checked = ", ".join(stats["checked"]) or "—"
    return (f"квитанция: проверено полей {len(stats['checked'])} ({checked}) | сохранились {stats['same']} | "
            f"затерто {stats['wiped']} | нарушено: {len(issues)}")


def stand_flow(base, token, partner_id, place, tags):
    """Создать запись с place/tags → прочитать → AddPartner → прочитать снова. → (before, after, error)."""
    b = base.rstrip("/")
    st, body, err = http(b + "/api/v1/journal/records", "POST", token, {
        "when": "2026-11-01T12:00:00Z", "place": place, "tags": tags,
    })
    if err:
        return None, None, err
    if st not in (200, 201):
        return None, None, f"создание записи: status {st}, тело {body[:200]}"
    rec_id = None
    try:
        doc = json.loads(body)
        rec_id = doc.get("id") or (doc.get("record") or {}).get("id")
    except (ValueError, TypeError):
        pass
    if not rec_id:
        return None, None, f"создание записи: в ответе нет id ({body[:160]})"

    st, before_body, err = http(f"{b}/api/v1/journal/records/{rec_id}", "GET", token)
    if err:
        return None, None, err
    if st not in (200, 201):
        return None, None, f"чтение до: status {st}"

    st2, _, err = http(f"{b}/api/v1/journal/records/{rec_id}/partners", "POST", token, {"partnerId": partner_id})
    if err:
        return None, None, err
    if st2 not in (200, 201, 204):
        return None, None, f"AddPartner: status {st2}"

    st3, after_body, err = http(f"{b}/api/v1/journal/records/{rec_id}", "GET", token)
    if err:
        return None, None, err
    if st3 not in (200, 201):
        return None, None, f"чтение после: status {st3}"
    try:
        return json.loads(before_body), json.loads(after_body), None
    except ValueError as e:
        return None, None, f"ответ не JSON: {e}"


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API")
    ap.add_argument("--token", help="JWT (или переменная окружения XDIARY_TOKEN)")
    ap.add_argument("--partner-id", help="id существующего партнёра для AddPartner")
    ap.add_argument("--place", default="Тестовое место")
    ap.add_argument("--tags", default="tag-a,tag-b")
    ap.add_argument("--before-json", help="офлайн: JSON чтения до")
    ap.add_argument("--after-json", help="офлайн: JSON чтения после")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.before_json is not None and a.after_json is not None:
        try:
            before = json.loads(a.before_json)
            after = json.loads(a.after_json)
        except ValueError as e:
            print(f"::error::check_be093_stale_fields: не разобрал --before/--after-json: {e} — не измерено (rc=2)")
            return 2
    else:
        token = a.token or os.environ.get("XDIARY_TOKEN")
        if not a.base or not token or not a.partner_id:
            print("::error::check_be093_stale_fields: нужны --base, --token (или XDIARY_TOKEN) и --partner-id — "
                  "не измерено (rc=2)")
            return 2
        tags = [t.strip() for t in a.tags.split(",") if t.strip()]
        before, after, err = stand_flow(a.base, token, a.partner_id, a.place, tags)
        if err:
            print(f"::error::check_be093_stale_fields: {err} — не измерено (rc=2)")
            return 2

    issues, stats = evaluate(before, after)
    for m in issues:
        print(f"::error::check_be093_stale_fields: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, before, after, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(before, after)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    full = {"id": "r1", "place": "Дом", "tags": ["tag-a", "tag-b"], "durationMin": 42}
    case("поля на месте — BE-093 не подтверждён", full, dict(full), 0, ["затерто 0", "place, tags"])

    wiped = {"id": "r1", "place": None, "tags": [], "durationMin": 0}
    case("поля затерты — дефект подтверждён", full, wiped, 1,
         ["place", "tags", "BE-093", "затерто 2"])

    only_tags = {"id": "r1", "place": "Дом", "tags": []}
    case("затерты только tags", full, only_tags, 1, ["`tags`", "затерто 1"])

    nested_before = {"record": dict(full)}
    nested_after = {"record": dict(wiped)}
    case("вложенный вид ответа (record/…)", nested_before, nested_after, 1, ["place", "tags"])

    nothing = {"id": "r1", "durationMin": 5}
    case("поля не были заполнены — проверять нечего", nothing, nothing, 0, ["проверено полей 0"])

    case("регистр/порядок тегов не даёт ложного красного",
         {"place": "Дом", "tags": ["B", "a"]}, {"place": " дом ", "tags": ["a", "b"]}, 0, ["затерто 0"])

    total += 1
    line = receipt_line({"checked": ["place"], "same": 1, "wiped": 0}, [])
    if "сохранились 1" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_be093_stale_fields --selftest: {ok}/{total} "
          f"(AddPartner, затирающий place/tags = rc=1: полная замена строки вместо патча, BE-093)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
