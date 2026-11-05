#!/usr/bin/env python3
"""XDiary: приёмка read-моделей по числам И содержимому (`TE-050`, `BE-068`, `BE-069`, `BE-071`).

Зачем: критерий «`200` и непусто» проверяется числами, но числа не проверяют содержимое, а
сравнение с командной таблицей для **новых** данных вообще неприменимо (правки @AR `20:00`/`20:20`):

  * с E1 командный путь пишет **событием** (`PartnerCommandRepository.SaveAsync` — только
    `PublishAsync`), командные EF-таблицы больше не наполняются ⇒ «read == count(*) командной
    таблицы» верно только для **исторических** строк, для новых даёт **ложное нарушение**;
  * «проекция живёт» доказывается **поведенчески** («создал → увидел»), а не счётчиком;
  * идемпотентность бэкфилла — повтор **не меняет число событий в event store**, а не число строк
    read-модели;
  * SQL-ремонт идёт **мимо** проекции и её маппинга, поэтому его результат сверяется с
    **источником по содержимому** (`PartnersNames` не `'[]'`, `Name` совпадает, `ContraceptiveCount`
    равен источнику), а не измеряется счётчиком.

Коды возврата: 0 — приёмка пройдена; 1 — нарушение (перечень); 2 — не измерено (нет данных).
`--selftest` прогоняет квитанцию на синтетических наборах без сети и без БД.
"""
from __future__ import annotations

import argparse
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:                             # noqa: BLE001
    pass


CRITERIA = (
    "HTTP /api/v1/partners",
    "HTTP /internal/journal/records",
    "число partners_read = источник",
    "число journal_read = источник",
    "содержимое: partners_read.Name",
    "содержимое: journal_read.PartnersNames",
    "содержимое: ContraceptiveCount",
    "проекция «создал → увидел»",
    "контракт passed/failed",
    "идемпотентность по событиям",
    "идемпотентность по строкам",
    "путь записи и объявление ремонта",
    "критерий A: без схемы POST не 200",
)


def verdict(partners_read=None, partners_src=None, journal_read=None, journal_src=None,
            partners_http=None, journal_http=None,
            contract_passed=None, contract_failed=None,
            rerun_partners_read=None, rerun_journal_read=None,
            write_path=None, repair_declared_in=None, repair_command=None, repair_date=None,
            repair_file_found=None,
            source_kind="historical", event_count=None, rerun_event_count=None,
            partners_name_mismatches=None, journal_empty_partner_names=None,
            journal_empty_partner_names_src=None,
            contraceptive_count_mismatches=None, projection_verified_by=None,
            measured_on=None, schema_present=None, post_partners_status=None,
            delivery_reached=None,
            projection_result=None, projection_waited_ms=None,
            projection_provenance=None, computed_provenance=None,
            computed_why=None):
    """Возвращает (issues, unmeasured). Пустые оба = приёмка пройдена."""
    issues, unmeasured = [], []

    def given(value):
        return value is not None

    # ── HTTP-коды: 401/403 = не измерено, 5xx = нарушение ──────────────────────────────────
    for name, code in (("/api/v1/partners", partners_http),
                       ("/internal/journal/records", journal_http)):
        if not given(code):
            unmeasured.append(f"HTTP {name}: не передано")
            continue
        if code in (401, 403):
            unmeasured.append(f"HTTP {name} = {code} (нет доступа к измерению, не «зелено»)")
        elif code >= 500:
            issues.append(f"HTTP {name} = {code} — сервис отвечает ошибкой")
        elif code != 200:
            issues.append(f"HTTP {name} = {code}, ожидали 200")

    # ── Числа: read-модель против источника ───────────────────────────────────────────────
    # Для `live`-источника (командная таблица больше не пишется, E1) числовая сверка не применима:
    # зафиксировано @AR 20:00, иначе получим ложное «read обгоняет источник».
    count_check_applicable = source_kind == "historical"
    for label, got, src in (("partners_read", partners_read, partners_src),
                            ("journal_read", journal_read, journal_src)):
        if not count_check_applicable:
            # Числовая сверка к live-источнику не применима — это НЕ «не измерено»,
            # а «критерий не имеет смысла» (E1): доказательство даёт поведенческая проверка.
            continue
        if not given(got) or not given(src):
            unmeasured.append(f"{label}: не передано (read или источник)")
            continue
        if src > 0 and got == 0:
            issues.append(f"{label}: 0 при непустом источнике ({src}) — дыра BE-071, «пусто» ≠ успех")
        elif got != src:
            issues.append(f"{label}: {got}, а в источнике {src} — проекция/бэкфилл неполны")

    # ── Содержимое: SQL-ремонт идёт мимо маппинга, поэтому сверяем данные (@AR 20:20) ────
    for label, bad in (("partners_read.Name ≠ partners.Name", partners_name_mismatches),
                       ("journal_read.ContraceptiveCount ≠ records", contraceptive_count_mismatches)):
        if not given(bad):
            unmeasured.append(f"содержимое: {label} — не измерено")
            continue
        if bad > 0:
            issues.append(f"содержимое: {label} — {bad} строк(и); счётчик это пропускает")

    # Имена партнёров: пусто в read — норма, ЕСЛИ пусто и в источнике (`both_empty`).
    # Потеря — это read пуст, а источник непуст (`lost_names`). Различение @TL 10:11 / @DO 10:17:
    # без него критерий даёт ложный красный на реальных данных (в источнике `PartnersJson='[]'`
    # у всех 4 строк, то есть `PartnersNames='[]'` в read — верное отражение источника).
    if not given(journal_empty_partner_names):
        unmeasured.append("содержимое: journal_read.PartnersNames — не измерено")
    elif not given(journal_empty_partner_names_src):
        unmeasured.append("содержимое: journal_read.PartnersNames — не передано, сколько пусто "
                          "в ИСТОЧНИКЕ (`--journal-empty-partner-names-src`): без пары "
                          "`both_empty` не отличить от `lost_names`")
    else:
        read_empty = journal_empty_partner_names
        src_empty = journal_empty_partner_names_src
        if read_empty > src_empty:
            issues.append(f"содержимое: lost_names — read пуст у {read_empty} строк(и), "
                          f"а источник пуст только у {src_empty}: потеряны имена")
        elif read_empty < src_empty:
            issues.append(f"содержимое: read содержит имена там, где источник пуст "
                          f"({read_empty} < {src_empty}) — расхождение с источником")
        # read_empty == src_empty → both_empty: норма, ничего не добавляем

    # ── Проекция живёт: поведенческое доказательство ─────────────────────────────────────
    # «Не проверяли» и «проверили — не работает» — разные состояния: первое НЕ ИЗМЕРЕНО,
    # второе НАРУШЕНИЕ. Данные для второго уже есть: @DO 11:14 — POST /partners 200,
    # опрос GET /partners 30 000 мс → count=0.
    # Версия, на которой сделан замер (@TL `chat/tl.md` `[2026-11-05 14:50]` п.3):
    # замер 11:14 был сделан на варианте B, а вердикт применяли к A — именно так родилось
    # «проекция не пишет». Поэтому поведенческий результат без образа/коммита не засчитывается.
    # §5 п.3 (@AR `chat/ar.md` `[2026-11-05 16:51]`): «представление строится проекцией» доказывается
    # ПЕРЕСБОРКОЙ (удалили строки -> POST -> числа вернулись) или ВРЕМЕНЕМ (`UpdatedAt` позже починки).
    # Непустая таблица после ручного ремонта — не доказательство.
    if computed_provenance == "repair":
        issues.append(f"представление: провенанс по данным — ремонт ({computed_why}) — "
                      "§5 п.3 не выполнен; нужна пересборка")
    elif computed_provenance == "time" and projection_provenance == "rebuild":
        issues.append(f"объявлено `rebuild`, а данные показывают `time` ({computed_why}) — "
                      "принято, но объявление не совпало с фактом")
    elif projection_provenance == "repair":
        issues.append("представление: строки появились ручным ремонтом (SQL), это НЕ проекция — "
                      "§5 п.3 не выполнен; доказательство: пересборка либо UpdatedAt позже починки")
    elif projection_provenance in (None, "none"):
        if projection_verified_by:
            unmeasured.append("представление: провенанс строк не указан "
                              "(--projection-provenance rebuild|time|repair)")
    elif projection_provenance not in ("rebuild", "time"):
        issues.append(f"провенанс представления '{projection_provenance}' неизвестен: "
                      "ожидали rebuild|time|repair")

    if (projection_result or projection_verified_by) and not measured_on:
        unmeasured.append("версия замера: поведенческий результат без `--measured-on` "
                          "(образ/коммит) не засчитывается — на какой версии мерили, такой вердикт и верен")
    if schema_present == "no" and post_partners_status is not None and post_partners_status >= 500:
        pass                              # при A поведенческая проверка неприменима: без схемы записи нет
    elif projection_result == "failed":
        waited = f" за {projection_waited_ms} мс" if projection_waited_ms else ""
        issues.append(f"проекция: «создал → увидел» ПРОВАЛЕН (POST 200, но read-модель пуста{waited}) — "
                      f"новые строки не пишутся")
    elif not projection_verified_by:
        unmeasured.append("проекция: «создал → увидел» не подтверждено (--projection-verified-by)")

    # ── Контракт ─────────────────────────────────────────────────────────────────────────
    if given(contract_passed) and given(contract_failed):
        if contract_failed > 0 or contract_passed == 0:
            issues.append(f"контракт: passed={contract_passed} failed={contract_failed}, ожидали 4/0")
        elif contract_passed != 4:
            issues.append(f"контракт: passed={contract_passed}, ожидали 4")
    else:
        unmeasured.append("контракт: passed/failed не передано")

    # ── Идемпотентность: по событиям (канон), по строкам — legacy ────────────────────────
    if given(event_count) and given(rerun_event_count):
        if event_count != rerun_event_count:
            issues.append(f"идемпотентность: повтор изменил число событий {event_count} → {rerun_event_count}")
    elif given(event_count):
        unmeasured.append("идемпотентность по событиям: нет --rerun-event-count")
    for label, first, again in (("partners_read", partners_read, rerun_partners_read),
                                ("journal_read", journal_read, rerun_journal_read)):
        if given(again) and given(first) and again != first:
            issues.append(f"{label}: повтор изменил число строк {first} → {again} (legacy-критерий)")

    # ── Критерий A (@TL `chat/tl.md` `[2026-11-05 15:02]`) ───────────────────────────────
    # «Схемы нет ⇒ `POST` не 200». Наблюдение @DO: схемы `Commands` нет, а `POST` вернул 200 —
    # это НЕ «не измерено», а НАРУШЕНИЕ: значит событие не доходит до консьюмера, либо в образе
    # остался `try/catch`. Обе причины проверяемы, но сам факт — уже дефект.
    if delivery_reached == "no":
        # @DO `chat/do.md` `[2026-11-05 15:45]`: `[handle] entered` = 0 при `subscribed`=1 и
        # `[Save] event=<guid>` — шина НЕ доставляет. Значит критерий A («без схемы → 5xx»)
        # **неприменим**: он предполагает, что обработчик исполняется. `POST` 200 здесь —
        # не нарушение A, а следствие разрыва доставки, и виновата не схема.
        unmeasured.append("критерий A не применим: до консьюмера не дошло (`[handle] entered`=0) — "
                          "сначала доставка события, схема/маппинг/A проверяются после неё")
    elif schema_present == "no" and post_partners_status is not None:
        if post_partners_status == 200:
            issues.append("критерий A НАРУШЕН: схемы `Commands` нет, а `POST /partners` вернул 200 — "
                          "событие не доходит до консьюмера (или в образе остался `try/catch`)")
        elif post_partners_status >= 500:
            pass                                     # A работает: падает громко
        else:
            issues.append(f"критерий A: без схемы `POST` вернул {post_partners_status}, "
                          f"ожидали `5xx` (или 200 только при наличии схемы)")

    # ── Путь записи и объявление ремонта ─────────────────────────────────────────────────
    if write_path not in (None, "projection", "sql"):
        issues.append(f"путь записи '{write_path}' неизвестен: ожидали projection|sql")
    if write_path == "sql":
        missing = [flag for flag, val in (("--repair-declared-in", repair_declared_in),
                                          ("--repair-command", repair_command),
                                          ("--repair-date", repair_date)) if not val]
        if missing:
            unmeasured.append("путь записи = sql (ремонт), но объявление неполное — нужны "
                              + ", ".join(missing))
        # `.sql`-путь должен существовать (@TL 10:19): карточка — объяснение, файл — артефакт.
        if (repair_command or "").strip().endswith(".sql") and repair_file_found is False:
            unmeasured.append(f"файл ремонта не найден: {repair_command}")

    return issues, unmeasured


def compute_provenance(read_max_updated_at, repaired_at):
    """Провенанс по ДАННЫМ, а не по объявлению (@DO `chat/do.md` `[2026-11-05 16:58]`).

    `(значение, почему)` либо `(None, причина)`, если данных нет или даты не разбираются.
    Правило одно у двух инструментов: строки, обновлённые раньше ремонта, проекцию не доказывают.
    """
    if not read_max_updated_at or not repaired_at:
        return None, "нет данных: нужны --read-max-updated-at и --repaired-at"
    try:
        from datetime import datetime
        a = read_max_updated_at.strip().replace("Z", "+00:00")
        b = repaired_at.strip()
        ta = datetime.fromisoformat(a)
        tb = datetime.fromisoformat(b if "T" in b else b + "T00:00:00")
        if ta.tzinfo and not tb.tzinfo:
            tb = tb.replace(tzinfo=ta.tzinfo)
        if ta > tb:
            return "time", f"max(UpdatedAt)={read_max_updated_at} позже ремонта {repaired_at}"
        return "repair", f"max(UpdatedAt)={read_max_updated_at} раньше ремонта {repaired_at}"
    except ValueError:
        return None, f"даты не разбираются: {read_max_updated_at!r} / {repaired_at!r}"


def time_provenance_warning(provenance, partners_read):
    """`time` как доказательство требует колонки `UpdatedAt`.

    Факт @DO `chat/do.md` `[2026-11-05 16:54]`: у `partners_read` **нет** `UpdatedAt`
    (`journal_read` имеет), поэтому для partners доказательство временем невозможно —
    только пересборка. Предупреждаем, а не принимаем молча.
    """
    if provenance != "time" or partners_read is None:
        return None
    return ("::warning::accept_read_path: провенанс `time` заявлен для `partners_read`, "
            "а у этой таблицы НЕТ `UpdatedAt` (есть только у `journal_read`) — "
            "для partners доказательство временем невозможно, нужна пересборка "
            "(`--projection-provenance rebuild`)")


def receipt(args, issues, unmeasured):
    def num(v):
        return "НЕ ИЗМЕРЕНО" if v is None else str(v)

    print("квитанция приёмки read-моделей (TE-050 / BE-068 / BE-069 / BE-071)")
    print(f"  источник чисел: {args.source_kind}"
          + (" (командная таблица; сверка read==count применима)" if args.source_kind == "historical"
             else " (live; числовая сверка не применима — E1)"))
    print(f"  partners_read={num(args.partners_read)} из источника {num(args.partners_src)}"
          f" | journal_read={num(args.journal_read)} из источника {num(args.journal_src)}")
    print(f"  http: /partners={num(args.partners_http)}, /internal/journal/records={num(args.journal_http)}")
    print(f"  содержимое: name≠{num(args.partners_name_mismatches)}, "
          f"empty PartnersNames={num(args.journal_empty_partner_names)}"
          f" (пусто в источнике: {num(args.journal_empty_partner_names_src)}), "
          f"contraceptive≠{num(args.contraceptive_count_mismatches)}")
    proj = args.projection_verified_by or "НЕ ПОДТВЕРЖДЕНО"
    if args.projection_result == "failed":
        proj = f"ПРОВАЛЕН (ждали {args.projection_waited_ms or '?'} мс, count=0)"
    if args.measured_on:
        proj += f" [замер на {args.measured_on}]"
    print(f"  проекция «создал → увидел»: {proj}")
    print(f"  провенанс представления: {args.projection_provenance or 'НЕ УКАЗАН'}")
    if computed[0]:
        print(f"  провенанс по данным: {computed[0]} ({computed[1]})")
    _warn = time_provenance_warning(args.projection_provenance, args.partners_read)
    if _warn:
        print(_warn)
    print(f"  контракт: passed={num(args.contract_passed)} failed={num(args.contract_failed)}")
    print(f"  события: {num(args.event_count)} → повтор {num(args.rerun_event_count)}")
    print(f"  доставка до консьюмера: "
          f"{'ДОШЛО' if args.delivery_reached == 'yes' else 'НЕ ДОШЛО' if args.delivery_reached == 'no' else 'НЕ ИЗМЕРЕНО'}"
          + (" (критерий A не применим)" if args.delivery_reached == "no" else ""))
    print(f"  критерий A: схема={'есть' if args.schema_present == 'yes' else 'нет' if args.schema_present == 'no' else 'НЕ ИЗМЕРЕНО'}"
          f" | POST /partners={num(args.post_partners_status)}")
    print(f"  путь записи в read-модель: {args.write_path or 'НЕ ИЗМЕРЕНО'}"
          + (f" (объявлен в {args.repair_declared_in})" if args.repair_declared_in else ""))
    if args.write_path == "sql":
        print(f"    ремонт: команда={args.repair_command or 'НЕ УКАЗАНА'}, "
              f"дата={args.repair_date or 'НЕ УКАЗАНА'}")
    for u in unmeasured:
        print(f"  ::warning::accept_read_path: {u}")
    for i in issues:
        print(f"  ::error::accept_read_path: {i}")
    # Норма @AR `round-checklist.md` 3.6: три числа раздельно + причина на каждого не засчитанного.
    total = len(CRITERIA)
    passed = max(0, total - len(unmeasured) - len(issues))
    print(f"  квитанция: критериев {total} | засчитано: {passed} | "
          f"не засчитано: {len(unmeasured)} | нарушено: {len(issues)}")
    if issues:
        print(f"НАРУШЕНИЕ: {len(issues)} (не измерено: {len(unmeasured)})")
        return 1
    if unmeasured:
        print(f"НЕ ЗАСЧИТАНО: {len(unmeasured)} критериев не измерено («неизвестно» ≠ «ок»)")
        return 2
    print("PASS: числа и содержимое в норме, проекция подтверждена, контракт 4/0, повтор не меняет события")
    return 0


# ───────────────────────────── selftest ─────────────────────────────
def selftest() -> int:
    base = dict(partners_read=13, partners_src=13, journal_read=4, journal_src=4,
                partners_http=200, journal_http=200, contract_passed=4, contract_failed=0,
                rerun_partners_read=13, rerun_journal_read=4,
                partners_name_mismatches=0, journal_empty_partner_names=0,
                journal_empty_partner_names_src=0,
                contraceptive_count_mismatches=0, event_count=4, rerun_event_count=4,
                projection_verified_by="partners-read-parity",
                measured_on="xdiary-partners:47dfd808",
                schema_present="yes", post_partners_status=200, delivery_reached="yes",
                projection_provenance="rebuild")
    cases = [
        ("норма (история + содержимое + проекция, доказано пересборкой)", dict(base), 0),
        ("представление из ручного ремонта = НЕ проекция (§5 п.3)",
         {**base, "projection_provenance": "repair"}, 1),
        ("провенанс представления не указан -> не засчитано",
         {**base, "projection_provenance": None}, 2),
        ("пусто при непустом источнике (BE-071)", {**base, "partners_read": 0}, 1),
        ("проекция неполна (12 из 13)", {**base, "partners_read": 12}, 1),
        ("повтор изменил число строк", {**base, "rerun_partners_read": 14}, 1),
        ("повтор изменил число СОБЫТИЙ", {**base, "rerun_event_count": 5}, 1),
        ("5xx", {**base, "journal_http": 500}, 1),
        ("401 = не измерено", {**base, "partners_http": 401}, 2),
        ("контракт 0/4", {**base, "contract_passed": 0, "contract_failed": 4}, 1),
        ("чистая БД: 0 при 0 — норма", {**base, "partners_read": 0, "partners_src": 0,
                                        "rerun_partners_read": 0}, 0),
        ("содержимое: both_empty (пусто в источнике и в read) — норма",
         {**base, "journal_empty_partner_names": 4, "journal_empty_partner_names_src": 4}, 0),
        ("содержимое: lost_names (read пуст, источник непуст)",
         {**base, "journal_empty_partner_names": 4, "journal_empty_partner_names_src": 0}, 1),
        ("содержимое: read полнее источника",
         {**base, "journal_empty_partner_names": 0, "journal_empty_partner_names_src": 3}, 1),
        ("содержимое: пусто в read без пары по источнику — не засчитано",
         {k: v for k, v in base.items() if k != "journal_empty_partner_names_src"}
         | {"journal_empty_partner_names": 4}, 2),
        ("содержимое: Name ≠ источника", {**base, "partners_name_mismatches": 2}, 1),
        ("live-источник без поведенческой проверки",
         {**base, "source_kind": "live", "projection_verified_by": None}, 2),
        ("live-источник с поведенческой проверкой — проходит",
         {**base, "source_kind": "live"}, 0),
        ("проекция: проверено и ПРОВАЛЕНО (POST 200, count=0 за 30000 мс)",
         {**base, "projection_result": "failed", "projection_waited_ms": 30000,
            "projection_verified_by": None}, 1),
        ("поведенческий замер без версии — не засчитано",
         {k: v for k, v in base.items() if k != "measured_on"}, 2),
        ("поведенческий замер с версией — проходит", dict(base), 0),
        ("доставки нет: критерий A не применим (200 не нарушение)",
         {**base, "delivery_reached": "no", "schema_present": "no", "post_partners_status": 200,
            "projection_result": None, "projection_verified_by": None, "measured_on": None}, 2),
        ("критерий A нарушен: схемы нет, POST 200",
         {**base, "schema_present": "no", "post_partners_status": 200}, 1),
        ("критерий A работает: схемы нет, POST 500",
         {**base, "schema_present": "no", "post_partners_status": 500,
            "projection_result": None, "projection_verified_by": None,
            "measured_on": None}, 0),
        ("критерий A: схемы нет, POST 400 — не по правилу",
         {**base, "schema_present": "no", "post_partners_status": 400}, 1),
        ("ремонт SQL без объявления", {**base, "write_path": "sql"}, 2),
        ("ремонт SQL: где+дата без команды",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071", "repair_date": "2026-11-05"}, 2),
        ("ремонт SQL с полным объявлением",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071",
            "repair_command": "INSERT ... SELECT ... ON CONFLICT DO NOTHING",
            "repair_date": "2026-11-05"}, 0),
        ("ремонт .sql, файла нет — не засчитано",
         {**base, "write_path": "sql", "repair_declared_in": "DO-079",
            "repair_command": "7-deploy/sql/repair-2026-11-05-readmodels.sql",
            "repair_date": "2026-11-05", "repair_file_found": False}, 2),
        ("ремонт .sql, файл есть — проходит",
         {**base, "write_path": "sql", "repair_declared_in": "DO-079",
            "repair_command": "7-deploy/sql/repair-2026-11-05-readmodels.sql",
            "repair_date": "2026-11-05", "repair_file_found": True}, 0),
        ("неизвестный путь записи", {**base, "write_path": "direct"}, 1),
    ]
    # §3.6: три числа обязаны складываться в число критериев
    issues0, unmeasured0 = verdict(**base)
    _total = len(CRITERIA)
    _passed = max(0, _total - len(unmeasured0) - len(issues0))
    if len(issues0) == 0 and len(unmeasured0) == 0 and _passed == _total:
        print(f"  [OK] квитанция сходится: критериев {_total} = "
              f"засчитано {_passed} + не засчитано 0 + нарушено 0")
    else:
        print(f"  [FAIL] квитанция не сходится: критериев {_total}, "
              f"засчитано {_passed}, не засчитано {len(unmeasured0)}, нарушено {len(issues0)}")
        return 1
    ok = 0
    for name, kw, want in cases:
        issues, unmeasured = verdict(**kw)
        rc = 1 if issues else (2 if unmeasured else 0)
        if rc == want:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc}, ждали {want}; issues={issues}, unmeasured={unmeasured}")

    # кейсы на ВЫЧИСЛЕНИЕ провенанса по данным (@DO 16:58) + текст предупреждения
    _c = [
        (compute_provenance("2026-11-06T10:00:00", "2026-11-05")[0], "time"),
        (compute_provenance("2026-11-03T17:03:18", "2026-11-05")[0], "repair"),
        (compute_provenance(None, "2026-11-05")[0], None),
        (compute_provenance("не дата", "2026-11-05")[0], None),
    ]
    if all(got == want for got, want in _c):
        ok += 4
    else:
        print(f"  [FAIL] вычисление провенанса: {_c}")
    _w = time_provenance_warning("time", 13)
    if _w and "НЕТ `UpdatedAt`" in _w:
        ok += 1
    else:
        print("  [FAIL] текст: предупреждение о `time` для partners_read не сформировано")

    print(f"accept_read_path --selftest: {ok}/{len(cases) + 5} (каждый критерий проверен на дефекте)")
    return 0 if ok == len(cases) + 5 else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--partners-read", type=int)
    ap.add_argument("--partners-src", type=int)
    ap.add_argument("--journal-read", type=int)
    ap.add_argument("--journal-src", type=int)
    ap.add_argument("--partners-http", type=int)
    ap.add_argument("--journal-http", type=int)
    ap.add_argument("--contract-passed", type=int)
    ap.add_argument("--contract-failed", type=int)
    ap.add_argument("--rerun-partners-read", type=int)
    ap.add_argument("--rerun-journal-read", type=int)
    ap.add_argument("--source-kind", choices=["historical", "live"], default="historical",
                    help="источник чисел: историческая таблица миграции или живые данные (E1)")
    ap.add_argument("--event-count", type=int, help="число событий в event store до повтора")
    ap.add_argument("--rerun-event-count", type=int, help="число событий после повтора бэкфилла")
    ap.add_argument("--partners-name-mismatches", type=int,
                    help="сколько строк partners_read.Name не совпадает с источником")
    ap.add_argument("--journal-empty-partner-names", type=int,
                    help="сколько строк journal_read.PartnersNames = '[]'")
    ap.add_argument("--journal-empty-partner-names-src", type=int,
                    help="сколько строк ИСТОЧНИКА имеют пустые имена (для различения both_empty/lost_names)")
    ap.add_argument("--contraceptive-count-mismatches", type=int,
                    help="сколько строк journal_read.ContraceptiveCount ≠ records")
    ap.add_argument("--projection-verified-by", help="чем подтверждено «создал → увидел»")
    ap.add_argument("--projection-result", choices=["passed", "failed"],
                    help="результат поведенческой проверки: создали → увидели (passed) или нет (failed)")
    ap.add_argument("--projection-waited-ms", type=int, help="сколько ждали появления в read-модели")
    ap.add_argument("--read-max-updated-at",
                    help="max(UpdatedAt) строк read-модели (ISO): провенанс вычисляется по данным")
    ap.add_argument("--repaired-at", help="дата ручного ремонта read-модели (YYYY-MM-DD)")
    ap.add_argument("--projection-provenance", choices=["rebuild", "time", "repair", "none"],
                    help="чем доказано, что представление строится проекцией: rebuild (пересборка), time (UpdatedAt позже починки), repair (ручной SQL — не доказательство), none")
    ap.add_argument("--delivery-reached", choices=["yes", "no"],
                    help="дошло ли событие до обработчика (маркер `[handle] entered` в логе)")
    ap.add_argument("--schema-present", choices=["yes", "no"],
                    help="есть ли схема Commands в базе (to_regclass по DomainEvents)")
    ap.add_argument("--post-partners-status", type=int,
                    help="HTTP-код POST /partners (для критерия A: без схемы ожидаем 5xx)")
    ap.add_argument("--measured-on", help="образ/коммит, на котором сделан поведенческий замер "
                                          "(обязателен, если передан --projection-result/--projection-verified-by)")
    ap.add_argument("--write-path", choices=["projection", "sql"],
                    help="как read-строки попали в таблицу: канонная проекция или одноразовый SQL-ремонт")
    ap.add_argument("--repair-declared-in", help="карточка/ADR, где объявлен одноразовый ремонт")
    ap.add_argument("--repair-command", help="команда/файл ремонта (для воспроизводимости)")
    ap.add_argument("--repair-date", help="дата выполнения ремонта")
    ap.add_argument("--repo-root", default=".", help="корень репозитория для проверки .sql-файла ремонта")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    computed = compute_provenance(args.read_max_updated_at, args.repaired_at)
    if args.read_max_updated_at:
        print(f"  провенанс вычислен: {computed[0] or 'НЕ ИЗМЕРЕНО'} — {computed[1]}")
    issues, unmeasured = verdict(
        partners_read=args.partners_read, partners_src=args.partners_src,
        journal_read=args.journal_read, journal_src=args.journal_src,
        partners_http=args.partners_http, journal_http=args.journal_http,
        contract_passed=args.contract_passed, contract_failed=args.contract_failed,
        rerun_partners_read=args.rerun_partners_read, rerun_journal_read=args.rerun_journal_read,
        write_path=args.write_path, repair_declared_in=args.repair_declared_in,
        repair_command=args.repair_command, repair_date=args.repair_date,
        repair_file_found=(__import__("os").path.exists(
            __import__("os").path.join(args.repo_root, args.repair_command))
            if (args.repair_command or "").strip().endswith(".sql") else None),
        source_kind=args.source_kind, event_count=args.event_count,
        rerun_event_count=args.rerun_event_count,
        partners_name_mismatches=args.partners_name_mismatches,
        journal_empty_partner_names=args.journal_empty_partner_names,
        journal_empty_partner_names_src=args.journal_empty_partner_names_src,
        contraceptive_count_mismatches=args.contraceptive_count_mismatches,
        projection_verified_by=args.projection_verified_by,
        projection_result=args.projection_result,
        projection_waited_ms=args.projection_waited_ms,
        projection_provenance=args.projection_provenance,
        computed_provenance=computed[0], computed_why=computed[1],
        measured_on=args.measured_on,
        schema_present=args.schema_present,
        delivery_reached=args.delivery_reached,
        post_partners_status=args.post_partners_status,
    )
    return receipt(args, issues, unmeasured)


if __name__ == "__main__":
    raise SystemExit(main())
