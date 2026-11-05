"""accept_pipeline_stages.py — приёмка конвейера «команда → событие → представление» по ступеням.

Порядок зафиксирован @TE и принят @TL (`chat/tl.md` `[2026-11-05 16:20]`):
`subscribed` → `migrations` → `[handle]` → `partners_read`. Каждая ступень — **отдельное число**,
и **ни одна не подменяется выводом**. Гейт отказывается засчитывать следующую ступень,
пока предыдущая не пройдена, — иначе два дефекта читаются одним числом.

Квитанция: `ступеней 4 | засчитано: N | не засчитано: M | нарушено: K`, `rc=0` только когда
все ступени пройдены. `НЕ ИЗМЕРЕНО` (нет данных) — это **не** пройдено и не нуль.

Требование провенанса (@TL `[2026-11-05 14:50]`): поведенческий результат (`[handle]`, `read`)
засчитывается только с `--measured-on <образ/SHA>`; без него ступень уходит в «не засчитано».
"""

import argparse
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

STAGES = ("шина", "схема", "обработчик", "представление")


def yes_no(value):
    return value if value in ("yes", "no") else None


def evaluate(subscribed, migrations, handle, read, measured_on):
    """Возвращает (rows, counted, not_counted, failed). rows: (ступень, вердикт, причина)."""
    rows = []
    counted = not_counted = failed = 0
    blocked = None

    # Ступень 1 — шина и подписка.
    if subscribed is None:
        rows.append((STAGES[0], "НЕ ИЗМЕРЕНО", "не передан --subscribed"))
        not_counted += 1
        blocked = STAGES[0]
    elif subscribed == "no":
        rows.append((STAGES[0], "НАРУШЕНО", "подписки нет: маркер не появился"))
        failed += 1
        blocked = STAGES[0]
    else:
        counted += 1
        rows.append((STAGES[0], "PASS", "подписка есть (маркер в логе)"))

    # Ступень 2 — схема event store (migrations=).
    if migrations is None:
        rows.append((STAGES[1], "НЕ ИЗМЕРЕНО", "не передан --migrations"))
        not_counted += 1
        blocked = blocked or STAGES[1]
    elif migrations == 0:
        rows.append((STAGES[1], "НАРУШЕНО",
                     "EF видит 0 миграций event store -> Migrate() — молчаливый no-op (BE-075)"))
        failed += 1
        blocked = blocked or STAGES[1]
    else:
        counted += 1
        rows.append((STAGES[1], "PASS", f"миграций видно: {migrations}"))

    # Ступень 3 — доставка до обработчика.
    if handle is None:
        rows.append((STAGES[2], "НЕ ИЗМЕРЕНО", "не передан --handle-entered"))
        not_counted += 1
        blocked = blocked or STAGES[2]
    elif measured_on is None:
        rows.append((STAGES[2], "НЕ ЗАСЧИТАНО", "поведенческий факт без --measured-on"))
        not_counted += 1
        blocked = blocked or STAGES[2]
    elif handle == 0:
        rows.append((STAGES[2], "НАРУШЕНО",
                     "`[handle] entered` = 0: событие не дошло до обработчика"))
        failed += 1
        blocked = STAGES[2]
    else:
        counted += 1
        rows.append((STAGES[2], "PASS", f"`[handle] entered` = {handle}"))

    # Ступень 4 — представление. Засчитывается только при пройденных 1–3.
    if read is None:
        rows.append((STAGES[3], "НЕ ИЗМЕРЕНО", "не передан --partners-read"))
        not_counted += 1
    elif blocked:
        rows.append((STAGES[3], "НЕ ЗАСЧИТАНО",
                     f"предусловие не выполнено ({blocked}) — число не читается отдельно"))
        not_counted += 1
    elif read == 0:
        rows.append((STAGES[3], "НАРУШЕНО", "представление пусто при работающем конвейере"))
        failed += 1
    else:
        counted += 1
        rows.append((STAGES[3], "PASS", f"строк в представлении: {read}"))

    return rows, counted, not_counted, failed


def output_lines(rows, counted, not_counted, failed, measured_on):
    lines = []
    width = max(len(r[0]) for r in rows)
    for name, verdict, why in rows:
        lines.append(f"  {name:<{width}}  {verdict:<12} {why}")
    lines.append(
        f"квитанция: ступеней {len(rows)} | засчитано: {counted} | "
        f"не засчитано: {not_counted} | нарушено: {failed}"
        + (f" | measured-on: {measured_on}" if measured_on else " | measured-on: НЕ УКАЗАН")
    )
    return lines


def main(argv=None):
    ap = argparse.ArgumentParser(description="приёмка конвейера по ступеням")
    ap.add_argument("--subscribed", choices=["yes", "no"])
    ap.add_argument("--migrations", type=int,
                    help="миграции, которые видит EventStoreDbContext (строка [migrate] migrations=); провижининг BE-075, read-схема BE-072")
    ap.add_argument("--handle-entered", type=int)
    ap.add_argument("--partners-read", type=int)
    ap.add_argument("--measured-on")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)

    if a.selftest:
        return selftest()

    rows, counted, not_counted, failed = evaluate(
        yes_no(a.subscribed), a.migrations, a.handle_entered, a.partners_read, a.measured_on)
    for line in output_lines(rows, counted, not_counted, failed, a.measured_on):
        print(line)
    if failed:
        return 1
    return 0 if counted == len(rows) else 2


def selftest():
    ok = total = 0

    def case(name, args, want_rc, must, must_not=()):
        nonlocal ok, total
        total += 1
        rows, c, n, f = evaluate(*args)
        text = "\n".join(output_lines(rows, c, n, f, args[4]))
        rc = 1 if f else (0 if c == len(rows) else 2)
        miss = [m for m in must if m not in text]
        bad = [m for m in must_not if m in text]
        if rc == want_rc and not miss and not bad:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, лишнее {bad}")

    case("всё пройдено", ("yes", 3, 5, 7, "dev@abc"), 0,
         ["засчитано: 4", "нарушено: 0", "measured-on: dev@abc", "`[handle] entered` = 5"])
    case("шина не доставила", ("yes", 3, 0, 0, "dev@abc"), 1,
         ["шин", "`[handle] entered` = 0", "НАРУШЕНО"],
         ["ступеней 4 | засчитано: 4"])
    case("нет миграций", ("yes", 0, 5, 7, "dev@abc"), 1,
         ["EF видит 0 миграций event store", "молчаливый no-op", "BE-075"])
    case("нет подписки", ("no", 3, 5, 7, "dev@abc"), 1, ["подписки нет"])
    case("без measured-on", ("yes", 3, 5, 7, None), 2,
         ["без --measured-on", "не засчитано"], ["PASS 5"])
    case("представление не читается при провале раньше", ("yes", 3, 0, 9, "dev@abc"), 1,
         ["предусловие не выполнено (обработчик)"], ["строк в представлении: 9"])
    case("нет данных по чтению", ("yes", 3, 5, None, "dev@abc"), 2, ["не передан --partners-read"])
    case("пусто при работающем конвейере", ("yes", 3, 5, 0, "dev@abc"), 1,
         ["представление пусто"])

    # кейс на ТЕКСТ по норме §5.4: квитанция обязана сходиться и не содержать None
    total += 1
    rows, c, n, f = evaluate("yes", 3, 5, 7, "dev@abc")
    text = "\n".join(output_lines(rows, c, n, f, "dev@abc"))
    if (c + n + f == len(rows)) and "None" not in text:
        ok += 1
    else:
        print("  [FAIL] текст: квитанция не сходится или содержит None")

    print(f"accept_pipeline_stages --selftest: {ok}/{total} (каждая ступень краснеет по своей причине)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
