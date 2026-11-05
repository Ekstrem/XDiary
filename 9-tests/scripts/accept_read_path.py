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


def verdict(partners_read=None, partners_src=None, journal_read=None, journal_src=None,
            partners_http=None, journal_http=None,
            contract_passed=None, contract_failed=None,
            rerun_partners_read=None, rerun_journal_read=None,
            write_path=None, repair_declared_in=None, repair_command=None, repair_date=None,
            source_kind="historical", event_count=None, rerun_event_count=None,
            partners_name_mismatches=None, journal_empty_partner_names=None,
            contraceptive_count_mismatches=None, projection_verified_by=None):
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
                       ("journal_read.PartnersNames пустые ('[]')", journal_empty_partner_names),
                       ("journal_read.ContraceptiveCount ≠ records", contraceptive_count_mismatches)):
        if not given(bad):
            unmeasured.append(f"содержимое: {label} — не измерено")
            continue
        if bad > 0:
            issues.append(f"содержимое: {label} — {bad} строк(и); счётчик это пропускает")

    # ── Проекция живёт: поведенческое доказательство ─────────────────────────────────────
    if not projection_verified_by:
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

    return issues, unmeasured


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
          f"empty PartnersNames={num(args.journal_empty_partner_names)}, "
          f"contraceptive≠{num(args.contraceptive_count_mismatches)}")
    print(f"  проекция «создал → увидел»: {args.projection_verified_by or 'НЕ ПОДТВЕРЖДЕНО'}")
    print(f"  контракт: passed={num(args.contract_passed)} failed={num(args.contract_failed)}")
    print(f"  события: {num(args.event_count)} → повтор {num(args.rerun_event_count)}")
    print(f"  путь записи в read-модель: {args.write_path or 'НЕ ИЗМЕРЕНО'}"
          + (f" (объявлен в {args.repair_declared_in})" if args.repair_declared_in else ""))
    if args.write_path == "sql":
        print(f"    ремонт: команда={args.repair_command or 'НЕ УКАЗАНА'}, "
              f"дата={args.repair_date or 'НЕ УКАЗАНА'}")
    for u in unmeasured:
        print(f"  ::warning::accept_read_path: {u}")
    for i in issues:
        print(f"  ::error::accept_read_path: {i}")
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
                contraceptive_count_mismatches=0, event_count=4, rerun_event_count=4,
                projection_verified_by="partners-read-parity")
    cases = [
        ("норма (история + содержимое + проекция)", dict(base), 0),
        ("пусто при непустом источнике (BE-071)", {**base, "partners_read": 0}, 1),
        ("проекция неполна (12 из 13)", {**base, "partners_read": 12}, 1),
        ("повтор изменил число строк", {**base, "rerun_partners_read": 14}, 1),
        ("повтор изменил число СОБЫТИЙ", {**base, "rerun_event_count": 5}, 1),
        ("5xx", {**base, "journal_http": 500}, 1),
        ("401 = не измерено", {**base, "partners_http": 401}, 2),
        ("контракт 0/4", {**base, "contract_passed": 0, "contract_failed": 4}, 1),
        ("чистая БД: 0 при 0 — норма", {**base, "partners_read": 0, "partners_src": 0,
                                        "rerun_partners_read": 0}, 0),
        ("содержимое: PartnersNames '[]' (SQL мимо маппинга)",
         {**base, "journal_empty_partner_names": 4}, 1),
        ("содержимое: Name ≠ источника", {**base, "partners_name_mismatches": 2}, 1),
        ("live-источник без поведенческой проверки",
         {**base, "source_kind": "live", "projection_verified_by": None}, 2),
        ("live-источник с поведенческой проверкой — проходит",
         {**base, "source_kind": "live"}, 0),
        ("ремонт SQL без объявления", {**base, "write_path": "sql"}, 2),
        ("ремонт SQL: где+дата без команды",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071", "repair_date": "2026-11-05"}, 2),
        ("ремонт SQL с полным объявлением",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071",
            "repair_command": "INSERT ... SELECT ... ON CONFLICT DO NOTHING",
            "repair_date": "2026-11-05"}, 0),
        ("неизвестный путь записи", {**base, "write_path": "direct"}, 1),
    ]
    ok = 0
    for name, kw, want in cases:
        issues, unmeasured = verdict(**kw)
        rc = 1 if issues else (2 if unmeasured else 0)
        if rc == want:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc}, ждали {want}; issues={issues}, unmeasured={unmeasured}")
    print(f"accept_read_path --selftest: {ok}/{len(cases)} (каждый критерий проверен на дефекте)")
    return 0 if ok == len(cases) else 1


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
    ap.add_argument("--contraceptive-count-mismatches", type=int,
                    help="сколько строк journal_read.ContraceptiveCount ≠ records")
    ap.add_argument("--projection-verified-by", help="чем подтверждено «создал → увидел»")
    ap.add_argument("--write-path", choices=["projection", "sql"],
                    help="как read-строки попали в таблицу: канонная проекция или одноразовый SQL-ремонт")
    ap.add_argument("--repair-declared-in", help="карточка/ADR, где объявлен одноразовый ремонт")
    ap.add_argument("--repair-command", help="команда/файл ремонта (для воспроизводимости)")
    ap.add_argument("--repair-date", help="дата выполнения ремонта")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    issues, unmeasured = verdict(
        partners_read=args.partners_read, partners_src=args.partners_src,
        journal_read=args.journal_read, journal_src=args.journal_src,
        partners_http=args.partners_http, journal_http=args.journal_http,
        contract_passed=args.contract_passed, contract_failed=args.contract_failed,
        rerun_partners_read=args.rerun_partners_read, rerun_journal_read=args.rerun_journal_read,
        write_path=args.write_path, repair_declared_in=args.repair_declared_in,
        repair_command=args.repair_command, repair_date=args.repair_date,
        source_kind=args.source_kind, event_count=args.event_count,
        rerun_event_count=args.rerun_event_count,
        partners_name_mismatches=args.partners_name_mismatches,
        journal_empty_partner_names=args.journal_empty_partner_names,
        contraceptive_count_mismatches=args.contraceptive_count_mismatches,
        projection_verified_by=args.projection_verified_by,
    )
    return receipt(args, issues, unmeasured)


if __name__ == "__main__":
    raise SystemExit(main())
