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
import pathlib
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# Поля, которые обязаны пережить `AddPartner`. Проверяем только те, что легко заполнить в одном запросе.
WATCH_FIELDS = ("place", "tags")
# Откуда берётся каждое поле: важно для аудита квитанции. У `tags` нет маршрута `/tags`,
# но `POST …/accessories` их принимает и сохраняет (проверено на dev), поэтому источник указывается явно.
SOURCE = {"place": "/place", "tags": "/accessories"}
# Второе условие @TL (`21:43`): если `Deleted` не проецируется, удалённая запись остаётся в выдаче —
# это уже приватность, а не косметика. Проверяем: после `delete` чтение по id не должно отдавать запись.


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
    if not rows:
        issues.append("нечего сравнивать: поля не заполнились (`place`/`tags` пусты до `AddPartner`) — "
                      "это НЕ «в порядке», это не измерено")
    stats = {"checked": [r[0] for r in rows],
             "same": sum(1 for r in rows if r[3]),
             "wiped": sum(1 for r in rows if not r[3])}
    return issues, stats


def evaluate_deleted(get_status, body, in_list=None, rec_id=None):
    """После `delete` чтение по id не должно отдавать запись. → (issues, stats)."""
    issues = []
    visible = False
    if isinstance(get_status, int) and 200 <= get_status < 300:
        visible = True
    if in_list is True:
        visible = True
    if visible:
        issues.append(f"после `delete` запись {rec_id or ''} всё ещё отдаётся "
                      f"(GET → {get_status}, в списке: {in_list}) — `Deleted` не проецируется, "
                      f"удалённое остаётся в выдаче (приватность, тот же класс, что BE-093)")
    stats = {"deleted_get": get_status, "in_list": in_list}
    return issues, stats


def deleted_receipt(stats, issues):
    return (f"квитанция удаления: GET после delete → {stats['deleted_get']} | в списке: {stats['in_list']} | "
            f"нарушено: {len(issues)}")


def stand_flow_deleted(base, token, partner_id):
    """Создать запись → delete → прочитать. → (get_status, in_list, error)."""
    b = base.rstrip("/")
    st, body, err = http(b + "/api/v1/journal/records", "POST", token, {"when": "2026-11-01T12:00:00Z"})
    if err:
        return None, None, err
    if st not in (200, 201):
        return None, None, f"создание записи: status {st}"
    rec_id = None
    try:
        doc = json.loads(body)
        rec_id = doc.get("id") or (doc.get("record") or {}).get("id")
    except (ValueError, TypeError):
        pass
    if not rec_id:
        return None, None, "создание записи: в ответе нет id"
    st2, _, err = http(f"{b}/api/v1/journal/records/{rec_id}/delete", "POST", token, {})
    if err:
        return None, None, err
    if st2 not in (200, 204):
        return None, None, f"delete: status {st2}"
    st3, _, err = http(f"{b}/api/v1/journal/records/{rec_id}", "GET", token)
    if err:
        return None, None, err
    in_list = None
    st4, list_body, err4 = http(f"{b}/api/v1/journal/records", "GET", token)
    if not err4 and st4 == 200:
        try:
            doc = json.loads(list_body)
            # Список приходит и массивом, и конвертом — поддерживаем оба вида (иначе AttributeError
            # выглядел как «красный гейт» с пустым выводом; поймано 2026-11-07).
            if isinstance(doc, list):
                items = doc
            else:
                items = doc.get("items") or doc.get("records") or (doc.get("data") or {}).get("items") or []
            in_list = any((i or {}).get("id") == rec_id for i in items if isinstance(i, dict))
        except (ValueError, TypeError):
            in_list = None
    return st3, in_list, None


def receipt_line(stats, issues):
    checked = ", ".join(f"{f}={SOURCE.get(f, '?')}" for f in stats["checked"]) or "—"
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

    # ВАЖНО (дефект, найденный @DO 23:07): читать «до» надо ПОСЛЕ заполнения полей, иначе сравнивать нечего
    # и гейт даёт «rc=0 при проверено полей 0» — холостой проход, а не измерение.
    # Контракт `SpecifyWhen`: тело `{ startedAt }` (см. `JournalController` + `8-front/js/api.js:159`).
    # С `{ when }` сервер отвечает `400` — это был мой дефект инструмента, а не продукта.
    stw0, _, errw0 = http(f"{b}/api/v1/journal/records/{rec_id}/when", "POST", token,
                          {"startedAt": "2026-11-08T10:00:00Z"})
    if errw0:
        return None, None, errw0
    stpl0, _, errp0 = http(f"{b}/api/v1/journal/records/{rec_id}/place", "POST", token, {"place": place})
    if errp0:
        return None, None, errp0
    # теги ставятся операцией `AddAccessories` (в индексе нет отдельного `/tags`).
    # Форма тела найдена на стенде: все четыре массива обязательны, иначе `400 validation`
    # (проверено: `{"tags":[…],"costumes":[],"toys":[],"fantasies":[]}` → 200, `tags` читаются обратно).
    stac, _, errac = http(f"{b}/api/v1/journal/records/{rec_id}/accessories", "POST", token,
                          {"tags": list(tags), "costumes": [], "toys": [], "fantasies": []})
    if errac:
        return None, None, errac

    st, before_body, err = http(f"{b}/api/v1/journal/records/{rec_id}", "GET", token)
    if err:
        return None, None, err
    if st not in (200, 201):
        # Создано (200 + id), но прочитать нельзя → это НЕ «не измерено», это дефект чтения. Прежде чем
        # вернуть ошибку, пробуем ручки `when` и `place`: именно `when` краснит `smoke-front`, и вызывающему
        # полезно видеть его статус, а не только «GET 404».
        stw, _, _ = http(f"{b}/api/v1/journal/records/{rec_id}/when", "POST", token,
                         {"when": "2026-11-08T10:00:00Z"})
        stpl, _, _ = http(f"{b}/api/v1/journal/records/{rec_id}/place", "POST", token, {"place": place})
        return None, None, (f"READ_PATH: запись создана (id {rec_id}), но `GET /journal/records/{{id}}` → {st}; "
                            f"`POST …/when` → {stw}; `POST …/place` → {stpl} — записи нет в read-модели")

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


def direct_read(base, token):
    """Создать запись и прочитать её по тому же id. → (id, status_чтения, error)."""
    b = base.rstrip("/")
    st, body, err = http(b + "/api/v1/journal/records", "POST", token, {"when": "2026-11-08T10:00:00Z"})
    if err:
        return None, None, err
    if st not in (200, 201):
        return None, None, f"создание записи: status {st}, тело {body[:160]}"
    rec_id = None
    try:
        doc = json.loads(body)
        rec_id = doc.get("id") or (doc.get("record") or {}).get("id")
    except (ValueError, TypeError):
        pass
    if not rec_id:
        return None, None, f"создание записи: в ответе нет id ({body[:160]})"
    stg, _bg, errg = http(f"{b}/api/v1/journal/records/{rec_id}", "GET", token)
    return rec_id, stg, errg


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API")
    ap.add_argument("--token", help="JWT (или переменная окружения XDIARY_TOKEN)")
    ap.add_argument("--partner-id", help="id существующего партнёра для AddPartner")
    ap.add_argument("--place", default="Тестовое место")
    ap.add_argument("--tags", default="tag-a,tag-b")
    ap.add_argument("--before-json", help="офлайн: JSON чтения до")
    ap.add_argument("--after-json", help="офлайн: JSON чтения после")
    ap.add_argument("--check-deleted", action="store_true",
                    help="дополнительно проверить, что после delete запись не отдаётся (условие @TL 21:43)")
    ap.add_argument("--deleted-get-status", type=int, help="офлайн: подставить статус GET после delete")
    ap.add_argument("--deleted-in-list", choices=("true", "false"), help="офлайн: есть ли запись в списке")
    ap.add_argument("--direct", action="store_true",
                    help="прямой критерий: создать запись и прочитать её по тому же id (ожидание 200, не 404)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.direct:
        token = a.token or os.environ.get("XDIARY_TOKEN")
        if not a.base or not token:
            print("::error::check_be093_stale_fields: для --direct нужны --base и --token — не измерено (rc=2)")
            return 2
        rid, stg, errd = direct_read(a.base, token)
        if errd:
            print(f"::error::check_be093_stale_fields: {errd} — не измерено (rc=2)")
            return 2
        if isinstance(stg, int) and 200 <= stg < 300:
            print(f"квитанция (direct): создано и прочитано по тому же id {rid[:8]}… | GET → {stg} | нарушено: 0")
            return 0
        print(f"::error::check_be093_stale_fields: запись создана (id {rid[:8]}…), но `GET /journal/records/{{id}}` → "
              f"{stg} — read-path не закрыт (тот же критерий, что краснит smoke-front)")
        print(f"квитанция (direct): GET по id из ответа → {stg} | нарушено: 1")
        return 1

    if a.check_deleted:
        if a.deleted_get_status is not None:
            in_list = {"true": True, "false": False, None: None}[
                a.deleted_in_list if a.deleted_in_list in ("true", "false") else None]
            issues, stats = evaluate_deleted(a.deleted_get_status, "", in_list)
        else:
            token = a.token or os.environ.get("XDIARY_TOKEN")
            if not a.base or not token:
                print("::error::check_be093_stale_fields: для --check-deleted нужны --base и --token — "
                      "не измерено (rc=2)")
                return 2
            st, in_list, err = stand_flow_deleted(a.base, token, a.partner_id)
            if err:
                print(f"::error::check_be093_stale_fields: {err} — не измерено (rc=2)")
                return 2
            issues, stats = evaluate_deleted(st, "", in_list)
        for m in issues:
            print(f"::error::check_be093_stale_fields: {m}")
        print(deleted_receipt(stats, issues))
        return 1 if issues else 0

    if a.before_json is not None and a.after_json is not None:
        def _load(v):
            """Принимаем и содержимое JSON, и путь к файлу: угадывать не должен вызывающий."""
            pth = pathlib.Path(v)
            if len(v) < 4096 and pth.is_file():
                return json.loads(pth.read_text(encoding="utf-8", errors="replace"))
            return json.loads(v)
        try:
            before = _load(a.before_json)
            after = _load(a.after_json)
        except ValueError as e:
            print(f"::error::check_be093_stale_fields: не разобрал --before/--after-json: {e} — не измерено (rc=2)")
            return 2
    else:
        token = a.token or os.environ.get("XDIARY_TOKEN")
        if not a.base or not token or (not a.partner_id and not a.direct and not a.check_deleted):
            print("::error::check_be093_stale_fields: нужны --base, --token (или XDIARY_TOKEN) и --partner-id — "
                  "не измерено (rc=2)")
            return 2
        tags = [t.strip() for t in a.tags.split(",") if t.strip()]
        before, after, err = stand_flow(a.base, token, a.partner_id, a.place, tags)
        if err:
            if err.startswith("READ_PATH:"):
                print(f"::error::check_be093_stale_fields: {err[10:].strip()} — запись создаётся, но не читается "
                      f"(read-модель не видит её: BE-088). Приёмку полей провести НЕЛЬЗЯ, пока это не исправлено")
                print("квитанция: READ_PATH — создано, но `GET` не отдаёт запись | нарушено: 1")
                return 1
            print(f"::error::check_be093_stale_fields: {err} — не измерено (rc=2)")
            return 2

    issues, stats = evaluate(before, after)
    if not stats["checked"]:
        # Холостой проход: сравнивать нечего. Это «не измерено» (rc=2), а не «в порядке» —
        # ровно тот дефект, который @DO поймал в моём же гейте 23:07.
        for m in issues:
            print(f"::error::check_be093_stale_fields: {m}")
        print(receipt_line(stats, issues))
        return 2
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
        rc = 2 if not stats["checked"] else (1 if issues else 0)
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
    case("поля не были заполнены — это НЕ измерено", nothing, nothing, 2,
         ["проверено полей 0", "нечего сравнивать"])

    case("регистр/порядок тегов не даёт ложного красного",
         {"place": "Дом", "tags": ["B", "a"]}, {"place": " дом ", "tags": ["a", "b"]}, 0, ["затерто 0"])

    def case_del(name, status, in_list, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate_deleted(status, "", in_list)
        text = "\n".join(issues) + "\n" + deleted_receipt(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    case_del("удаление спроецировано: GET 404", 404, False, 0, ["GET после delete → 404", "нарушено: 0"])
    case_del("удаление не спроецировано: GET 200", 200, None, 1, ["приватность", "не проецируется"])
    case_del("GET 404, но запись осталась в списке", 404, True, 1, ["в списке: True"])

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
