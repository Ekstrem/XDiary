"""te_review_packet.py — пакет для ревью карточек @TE: «что проверять» одной командой.

Зачем: в `5-tech-backlog/tester/` накопилось 20+ карточек в статусе `На проверке`, и ревьюеру (@TL/@AR)
приходится открывать каждую, чтобы понять: (а) чем это проверяется, (б) нужен ли для проверки браузерный
прогон. Без такого пакета ревью становится дорогим и откладывается — а «На проверке» превращается в свалку.

Что делает:

- читает `5-tech-backlog/tester/TE-*.md`;
- вытаскивает статус, приоритет, «Проверяет», первую команду проверки (строки с `python 9-tests/scripts/…`,
  `node …`, `npx playwright test …`) и признак «нужен прогон» (слова `нужен dispatch`/`только прогоном`/
  `не проверено`);
- печатает markdown-таблицу `карточка | статус | проверяет | чем проверяется | нужен прогон`;
- `--check`: каждая карточка в `На проверке` обязана иметь **либо** команду проверки, **либо** явную пометку
  «требует прогона»; иначе `rc=1` (карточка «на проверке» без способа проверки — это «зелено на бумаге»).

Коды: `0` — пакет собран (и `--check` чист); `1` — `--check` нашёл карточки без способа проверки;
`2` — не измерено (нет папки карточек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

STATUS = re.compile(r"\*\*Статус:\*\*\s*([^\n|]+)")
PRIORITY = re.compile(r"\*\*Приоритет:\*\*\s*([^\n|]+)")
REVIEWER = re.compile(r"\*\*Проверяет:\*\*\s*([^\n|]+)")
CMD = re.compile(r"^(?:\s{0,8})(python|node|npx)\s+9?-?[\w./-]*", re.M)
NEEDS_RUN = re.compile(r"(нужен\s+dispatch|только\s+прогоном|не\s+проверено|требует\s+прогона|без\s+браузерного\s+прогона)", re.I)


# AGENTS.md §2 (2026-11-16): у `На проверке` обязательны `Проверяет:` + `На проверке с:` + ссылка на
# артефакт. Гейт проверяет наличие поля, чтобы правило не держалось на памяти.
REVIEW_SINCE = re.compile(r"^-\s*\*\*На проверке с:\*\*\s*(.+?)\s*$", re.M)


def parse_card(name, text):
    st = STATUS.search(text)
    pr = PRIORITY.search(text)
    rv = REVIEWER.search(text)
    cmd = ""
    for line in text.splitlines():
        s = line.strip()
        if s.startswith(("python ", "node ", "npx ")) and "scripts/" in s or s.startswith("npx playwright"):
            cmd = s.strip("` ")
            break
    if not cmd:
        m = re.search(r"`((?:python|node|npx)\s+[^`]{5,120})`", text)
        cmd = m.group(1) if m else ""
    return {
        "name": name,
        "status": (st.group(1).strip().replace("*", "") if st else "?"),
        "priority": (pr.group(1).strip().replace("*", "") if pr else ""),
        "reviewer": (rv.group(1).strip().replace("*", "") if rv else ""),
        "command": cmd,
        "needs_run": bool(NEEDS_RUN.search(text)),
        "review_since": (REVIEW_SINCE.search(text).group(1) if REVIEW_SINCE.search(text) else ""),
    }


def collect(root, all_roles=False):
    """Карточки для пакета. По умолчанию — только моя папка `tester/`; `all_roles` — все папки ролей.

    `all_roles` нужен для плана прогонов (`--dispatch`): один `front-e2e` закрывает и `FE-*`, и `TE-*`,
    и «совместно» должно считаться инструментом, а не памятью.
    """
    base = pathlib.Path(root) / "5-tech-backlog"
    if not base.is_dir():
        return None, "нет папки 5-tech-backlog — не измерено"
    if all_roles:
        cards = {}
        for path in sorted(base.glob("*/*.md")):
            if path.parent.name == "chat":
                continue
            # только карточки вида `XX-123.md`: `ERRATA.md`, `STATUS-*.md` — документы, не задачи
            if not re.fullmatch(r"[A-Z]{2}-\d+\.md", path.name):
                continue
            cards[path.name] = path.read_text(encoding="utf-8", errors="replace")
        if not cards:
            return None, "в 5-tech-backlog нет карточек — не измерено"
        return cards, None
    d = base / "tester"
    if not d.is_dir():
        return None, "нет папки 5-tech-backlog/tester — не измерено"
    return {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(d.glob("TE-*.md"))}, None


def evaluate(cards):
    rows = [parse_card(n, t) for n, t in sorted(cards.items())]
    pending = [r for r in rows if r["status"].lower().startswith("на проверке")]
    missing = [r for r in pending if not r["command"] and not r["needs_run"]]
    no_since = [r for r in pending if not r.get("review_since")]
    stats = {"cards": len(rows), "pending": len(pending), "missing": len(missing),
             "needs_run": len([r for r in pending if r["needs_run"]]),
             "no_since": len(no_since)}
    return rows, pending, missing, stats, no_since


def table(pending):
    out = ["| Карточка | Статус | Проверяет | Чем проверяется | Нужен прогон |",
           "|---|---|---|---|---|"]
    for r in pending:
        cmd = r["command"].replace("|", "\\|")[:70] if r["command"] else "—"
        out.append(f"| {r['name']} | {r['status']} | {r['reviewer'] or '—'} | {('`' + cmd + '`') if r['command'] else '—'} | "
                   f"{'да' if r['needs_run'] else 'нет'} |")
    return "\n".join(out)


def receipt_line(stats, missing):
    return (f"квитанция: карточек {stats['cards']} | на проверке {stats['pending']} | "
            f"из них требуют прогона {stats['needs_run']} | без способа проверки: {len(missing)} | "
            f"без поля `На проверке с:` {stats.get('no_since', 0)}")


# Класс прогона по команде из карточки. Порядок важен: браузер проверяем раньше «локально», иначе
# `npx playwright test` попадёт в статические и попросит не тот прогон.
RUN_CLASSES = (
    ("front-e2e (браузер, dispatch)", ("playwright", "npx playwright")),
    ("ci-backend / dotnet test", ("dotnet test", "ApiTests", "IntegrationTests")),
    ("сервер/стенд (--base)", ("--base", "https://", "accept_read_path", "accept_te_data_points")),
    ("локально (статические гейты)", ("--selftest", "python", "node")),
)


def run_class(command):
    """Класс прогона для команды проверки; `None`, если команды нет."""
    if not command or command.strip() in ("—", ""):
        return None
    low = command.lower()
    for label, needles in RUN_CLASSES:
        for needle in needles:
            if needle.lower() in low:
                return label
    return "локально (статические гейты)"


def dispatch_plan(pending):
    """{класс прогона: [имена карточек]} — «один прогон закрывает N карточек»."""
    plan = {}
    for r in pending:
        label = run_class(r.get("command") or "")
        if label is None:
            continue
        plan.setdefault(label, []).append(r["name"].replace(".md", ""))
    return plan


def dispatch_report(pending):
    plan = dispatch_plan(pending)
    if not plan:
        return "план прогонов: карточек «На проверке» нет"
    lines = ["план прогонов (одна команда закрывает несколько карточек):"]
    for label, cards in sorted(plan.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        lines.append(f"  {label}: {len(cards)} → {', '.join(sorted(cards))}")
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--check", action="store_true", help="rc=1, если у карточки «На проверке» нет способа проверки")
    ap.add_argument("--all", action="store_true", help="показать все карточки, не только «На проверке»")
    ap.add_argument("--all-roles", action="store_true",
                    help="считать карточки всех ролей (для --dispatch: что закроет один прогон)")
    ap.add_argument("--dispatch", action="store_true",
                    help="сгруппировать «На проверке» по одному прогону (что запускать и что это закроет)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    cards, err = collect(a.root, a.all_roles)
    if err:
        print(f"::error::te_review_packet: {err} (rc=2)")
        return 2
    rows, pending, missing, stats, no_since = evaluate(cards)
    print(table(rows if a.all else pending))
    for r in missing:
        print(f"::error::te_review_packet: {r['name']} — статус «На проверке», но нет ни команды проверки, "
              f"ни пометки «требует прогона»")
    for r in no_since:
        print(f"::warning::te_review_packet: {r['name']} — статус «На проверке», но нет поля "
              f"`На проверке с:` (AGENTS.md §2)")
    print(receipt_line(stats, missing))
    if a.dispatch:
        print(dispatch_report(pending))
    return 1 if (a.check and missing) else 0


def selftest():
    ok = total = 0

    # TE-101р: группировка по прогону — отдельный кейс, иначе `--dispatch` не проверен
    _cards = [
        {"name": "TE-001.md", "command": "npx playwright test tests/a.spec.js"},
        {"name": "TE-002.md", "command": "npx playwright test tests/b.spec.js"},
        {"name": "TE-003.md", "command": "dotnet test 6-msa/BC-X/src/X.ApiTests"},
        {"name": "TE-004.md", "command": "python 9-tests/scripts/x.py --selftest"},
    ]
    plan = dispatch_plan(_cards)
    total += 1
    if (plan.get("front-e2e (браузер, dispatch)") == ["TE-001", "TE-002"]
            and plan.get("ci-backend / dotnet test") == ["TE-003"]
            and plan.get("локально (статические гейты)") == ["TE-004"]):
        ok += 1
        print("  [OK]   --dispatch: браузер / dotnet / локально разложены по классам")
    else:
        print(f"  [FAIL] --dispatch: неожиданная группировка {plan}")
    total += 1
    if run_class("—") is None and run_class("") is None:
        ok += 1
        print("  [OK]   --dispatch: карточка без команды не попадает в план")
    else:
        print("  [FAIL] --dispatch: пустая команда дала класс")


    def case(name, cards, check, want_rc, must=()):
        nonlocal ok, total
        total += 1
        rows, pending, missing, stats, _no_since = evaluate(cards)
        text = table(pending) + "\n" + receipt_line(stats, missing)
        rc = 1 if (check and missing) else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    good = {"TE-001.md": "- **Статус:** На проверке\n- **Проверяет:** @TL\n"
                         "```\npython 9-tests/scripts/check_e2e_all.py\n```\n"}
    case("нет поля «На проверке с» — видно числом",
         {"TE-900.md": "- **Статус:** На проверке\n- **Проверяет:** @TL\npython 9-tests/scripts/x.py --selftest\n"},
         True, 0, ["без поля `На проверке с:` 1"])

    case("карточка с командой — зелёная", good, True, 0, ["на проверке 1", "без способа проверки: 0"])

    bare = {"TE-002.md": "- **Статус:** На проверке\n- **Проверяет:** @TL\nПросто текст без команды.\n"}
    case("карточка без команды и без пометки — красная", bare, True, 1, ["TE-002.md"])

    runner = {"TE-003.md": "- **Статус:** На проверке\nНужен dispatch: браузерного прогона нет.\n"}
    case("помечено «требует прогона» — зелёная", runner, True, 0, ["без способа проверки: 0"])

    closed = {"TE-004.md": "- **Статус:** Закрыта\n"}
    case("закрытые не считаются", closed, True, 0, ["на проверке 0"])

    total += 1
    line = receipt_line({"cards": 5, "pending": 2, "needs_run": 1}, [])
    if "карточек 5" in line and "требуют прогона 1" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"te_review_packet --selftest: {ok}/{total} (карточка «На проверке» без способа проверки = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
