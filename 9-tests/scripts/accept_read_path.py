#!/usr/bin/env python3
"""XDiary: приёмка read-моделей по ЧИСЛАМ (`TE-050`, `BE-068`, `BE-069`, `BE-071`).

Зачем: критерий «`200` и непусто» проверяется числами, но вердикт каждый раз собирается вручную
по чату. Здесь он собирается один раз и печатается квитанцией с явной областью измерения:

    accept_read_path.py --partners-read 13 --partners-src 13 \
                        --journal-read 4  --journal-src 4 \
                        --partners-http 200 --journal-http 200 \
                        --contract-passed 4 --contract-failed 0 \
                        --rerun-partners-read 13 --rerun-journal-read 4

Правила (из `04-architecture/read-paths.md` §1/§2.1 и `edm-debt-plan.md` `1875e8a3`):
  * число read-эндпоинта сравнивается с `count(*)` **источника** (тот же предикат, что и чтение);
  * **пусто при непустом источнике → нарушение** (дыра `BE-071`); пусто при пустом источнике — норма;
  * повторный прогон миграции/бэкфилла **не меняет** число (идемпотентность);
  * `401`/`403` вместо числа → **НЕ ИЗМЕРЕНО** (`rc=2`), а не «зелено»;
  * `5xx` → нарушение с кодом в тексте.

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


def verdict(partners_read, partners_src, journal_read, journal_src,
            partners_http, journal_http, contract_passed, contract_failed,
            rerun_partners_read=None, rerun_journal_read=None,
            write_path=None, repair_declared_in=None, repair_command=None, repair_date=None):
    """Возвращает (issues, unmeasured) — списки строк. Пустые оба = приёмка пройдена."""
    issues, unmeasured = [], []

    def need(value, name):
        if value is None:
            unmeasured.append(f"{name}: не передано")
            return False
        return True

    # HTTP-коды: 401/403 = не измерено, 5xx = нарушение
    for name, code in (("/api/v1/partners", partners_http),
                       ("/internal/journal/records", journal_http)):
        if not need(code, f"HTTP {name}"):
            continue
        if code in (401, 403):
            unmeasured.append(f"HTTP {name} = {code} (нет доступа к измерению, не «зелено»)")
        elif code >= 500:
            issues.append(f"HTTP {name} = {code} — сервис отвечает ошибкой")
        elif code != 200:
            issues.append(f"HTTP {name} = {code}, ожидали 200")

    # Числа: read-модель против источника
    for label, got, src in (("partners_read", partners_read, partners_src),
                            ("journal_read", journal_read, journal_src)):
        if not need(got, f"{label}") or not need(src, f"{label} источник"):
            continue
        if src > 0 and got == 0:
            issues.append(f"{label}: 0 при непустом источнике ({src}) — дыра BE-071, «пусто» ≠ успех")
        elif got != src:
            issues.append(f"{label}: {got}, а в источнике {src} — проекция/бэкфилл неполны")
        elif got == 0 and src == 0:
            pass                              # пусто при пустом источнике — норма (чистая БД)

    # Контракт
    if need(contract_passed, "контракт: passed") and need(contract_failed, "контракт: failed"):
        if contract_failed > 0 or contract_passed == 0:
            issues.append(f"контракт: passed={contract_passed} failed={contract_failed}, ожидали 4/0")
        elif contract_passed != 4:
            issues.append(f"контракт: passed={contract_passed}, ожидали 4")

    # Идемпотентность повтора
    for label, first, again in (("partners_read", partners_read, rerun_partners_read),
                                ("journal_read", journal_read, rerun_journal_read)):
        if again is None:
            continue                          # не передали — критерий не проверялся
        if first is None:
            continue
        if again != first:
            issues.append(f"{label}: повтор изменил число {first} → {again} — бэкфилл неидемпотентен")

    # Путь записи в read-модель (@TL 09:51): проекция — канонный путь; прямой INSERT…SELECT —
    # второй путь, и он обязан быть ОБЪЯВЛЕН (карточка/ADR), иначе через месяц никто не вспомнит,
    # почему read-строки не совпадают с событиями.
    if write_path == "sql":
        # Объявление ремонта самодостаточно: ГДЕ объявлено + ЧЕМ выполнено + КОГДА.
        # Только «где» недостаточно: через месяц по нему не воспроизвести, а «объявлено» без команды
        # и даты — это ссылка, а не факт (@DO 10:10: «путь + дата + команда»).
        missing = [flag for flag, val in (("--repair-declared-in", repair_declared_in),
                                          ("--repair-command", repair_command),
                                          ("--repair-date", repair_date)) if not val]
        if missing:
            unmeasured.append("путь записи = sql (ремонт), но объявление неполное — нужны "
                              + ", ".join(missing))
    if write_path not in (None, "projection", "sql"):
        issues.append(f"путь записи '{write_path}' неизвестен: ожидали projection|sql")

    return issues, unmeasured


def receipt(args, issues, unmeasured):
    def num(v):
        return "НЕ ИЗМЕРЕНО" if v is None else str(v)

    print("квитанция приёмки read-моделей (TE-050 / BE-068 / BE-069 / BE-071)")
    print(f"  partners_read={num(args.partners_read)} из источника {num(args.partners_src)}"
          f" | journal_read={num(args.journal_read)} из источника {num(args.journal_src)}")
    print(f"  http: /partners={num(args.partners_http)}, /internal/journal/records={num(args.journal_http)}")
    print(f"  контракт: passed={num(args.contract_passed)} failed={num(args.contract_failed)}")
    print(f"  путь записи в read-модель: {args.write_path or 'НЕ ИЗМЕРЕНО'}"
          + (f" (объявлен в {args.repair_declared_in})" if args.repair_declared_in else ""))
    if args.write_path == "sql":
        print(f"    ремонт: команда={args.repair_command or 'НЕ УКАЗАНА'}, дата={args.repair_date or 'НЕ УКАЗАНА'}")
    print(f"  повтор: partners_read={num(args.rerun_partners_read)}, "
          f"journal_read={num(args.rerun_journal_read)}")
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
    print("PASS: числа совпадают, контракт 4/0, повтор не меняет число")
    return 0


# ───────────────────────────── selftest ─────────────────────────────
def selftest() -> int:
    base = dict(partners_read=13, partners_src=13, journal_read=4, journal_src=4,
                partners_http=200, journal_http=200, contract_passed=4, contract_failed=0,
                rerun_partners_read=13, rerun_journal_read=4)
    cases = [
        ("норма: 13/13, 4/4, 200, 4/0, повтор стабилен", dict(base), 0),
        ("пусто при непустом источнике (BE-071)", {**base, "partners_read": 0}, 1),
        ("проекция неполна (12 из 13)", {**base, "partners_read": 12}, 1),
        ("повтор изменил число (неидемпотентно)", {**base, "rerun_partners_read": 14}, 1),
        ("5xx", {**base, "journal_http": 500}, 1),
        ("401 = не измерено", {**base, "partners_http": 401}, 2),
        ("контракт 0/4", {**base, "contract_passed": 0, "contract_failed": 4}, 1),
        ("чистая БД: 0 при 0 — норма", {**base, "partners_read": 0, "partners_src": 0,
                                        "rerun_partners_read": 0}, 0),
        ("ремонт SQL без объявления — не засчитано", {**base, "write_path": "sql"}, 2),
        ("ремонт SQL: объявление без команды/даты — не засчитано",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071"}, 2),
        ("ремонт SQL с полным объявлением — проходит",
         {**base, "write_path": "sql", "repair_declared_in": "BE-071",
            "repair_command": "INSERT ... SELECT ... ON CONFLICT DO NOTHING", "repair_date": "2026-11-05"}, 0),
        ("неизвестный путь записи — нарушение", {**base, "write_path": "direct"}, 1),
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
        args.partners_read, args.partners_src, args.journal_read, args.journal_src,
        args.partners_http, args.journal_http, args.contract_passed, args.contract_failed,
        args.rerun_partners_read, args.rerun_journal_read,
        args.write_path, args.repair_declared_in, args.repair_command, args.repair_date,
    )
    return receipt(args, issues, unmeasured)


if __name__ == "__main__":
    raise SystemExit(main())
