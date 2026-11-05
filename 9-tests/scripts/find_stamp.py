#!/usr/bin/env python3
"""XDiary: «метка существует?» — проверка по ИСТОРИИ, а не по текущему состоянию.

Зачем (правило `chat/tl.md` `[2026-11-05 14:05]`, случай `fe.md` `2026-11-16 11:45`):
проверка «метки нет в `origin`» **не доказывает**, что метки не было. Если роль правила метки
(например, приводила штампы к реальному времени), исправление **стирает улику**: сообщение
остаётся в истории, но в текущем файле его нет. Я на этом ошибся ровно так — объявил метку
«фантомом», хотя она была реальной и адресована мне.

Правильная проверка — pickaxe по истории:

    git log --all -S"<метка>" -- <файл>        # в каких коммитах метка появлялась/исчезала
    git show <commit>^:<файл>                  # как файл выглядел ДО правки метки

Использование:
    python 9-tests/scripts/find_stamp.py --stamp "2026-11-16 11:45" --file 5-tech-backlog/chat/fe.md
    python 9-tests/scripts/find_stamp.py --stamp "2026-11-05 15:45"          # по всем чатам
    python 9-tests/scripts/find_stamp.py --selftest

Вердикты: `СЕЙЧАС` (есть в текущем файле), `ТОЛЬКО В ИСТОРИИ` (было, потом исправлено),
`НЕТ НИГДЕ` (действительно не существует).
Коды возврата: 0 — метка существует (сейчас или в истории); 1 — не найдена нигде; 2 — не git-репозиторий.
"""
from __future__ import annotations

import argparse
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:                             # noqa: BLE001
    pass


def git(*args: str) -> tuple:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.returncode, r.stdout


def classify(now_hits: int, history_commits: int) -> str:
    """Вся логика вердикта — в чистой функции, чтобы проверить без git и без ФС."""
    if now_hits > 0:
        return "СЕЙЧАС"
    if history_commits > 0:
        return "ТОЛЬКО В ИСТОРИИ"
    return "НЕТ НИГДЕ"


def history_commits(stamp: str, path: str = "") -> list:
    """Коммиты, где подстрока метки менялась (pickaxe). Это КАНДИДАТЫ, а не доказательство:
    метка может встречаться в цитате («несуществующая 2026-11-05 23:59»), и тогда `-S` найдёт
    коммит, хотя штампа в нём не было. Поэтому кандидаты проверяются ниже каноническим поиском.
    """
    args = ["log", "--all", "-S", stamp, "--format=%H|%ad|%s", "--date=short"]
    if path:
        args += ["--", path]
    rc, out = git(*args)
    if rc != 0:
        return []
    rows = []
    for line in out.splitlines()[:12]:
        parts = line.split("|", 2)
        if len(parts) == 3:
            rows.append(tuple(parts))
    return rows


def filter_canonical(candidates: list, grep_rc) -> list:
    """Оставляет кандидатов, где по ревизии нашлась КАНОНИЧЕСКАЯ строка.

    `grep_rc(sha)` возвращает код `git grep`: 0 — нашёл, ≠0 — нет. Вынесено в параметр,
    чтобы `--selftest` проверял **логику фильтра** без git и без состояния репозитория
    (норма @TL `chat/tl.md` `[2026-11-05 14:30]`: кейс не должен зависеть от дерева).
    """
    return [c for c in candidates if grep_rc(c[0]) == 0]


def canonical_in_history(stamp: str, candidates: list, path: str = "") -> list:
    """Оставляет только те коммиты, где существовала КАНОНИЧЕСКАЯ строка `- [<stamp>] …`.

    Проверка дешёвая (`git grep -c -F` по конкретной ревизии), поэтому не читаем мегабайтные
    блобы целиком. Так «цитата в тексте» перестаёт создавать ложную историю.
    """
    cache = {}
    for sha, _date, _subject in candidates:
        # `-e` обязателен: шаблон начинается с `-`, иначе git считает его опцией (rc=129).
        args = ["grep", "-c", "-F", "-e", "- [%s]" % stamp, sha]
        if path:
            args += ["--", path]
        cache[sha] = git(*args)[0]

    def grep_rc(sha: str) -> int:
        return cache.get(sha, 1)

    return [(sha[:8], date, subject)
            for sha, date, subject in filter_canonical(candidates, grep_rc)]


def is_canonical_line(content: str, stamp: str) -> bool:
    """Штамп — это начало строки `- [<stamp>] …`, а НЕ любое упоминание метки в тексте.

    Различение критично: @FE в `fe.md` объяснял, что `2026-11-16 11:45` не фантом, и в тексте
    этих объяснений метка встречается — но **сообщения** с такой меткой в текущем файле нет.
    Счёт по подстроке дал бы ложное «СЕЙЧАС» (та же ошибка, что «122» против «59»).
    """
    return content.lstrip().startswith("- [" + stamp + "]")


def parse_grep_line(line: str, stamp: str) -> tuple:
    """`git grep -n` → (это_штамп, файл, строка). Разбор отдельно, чтобы проверить без git."""
    parts = line.split(":", 2)
    if len(parts) != 3:
        return (False, "", "")
    path, lineno, content = parts
    return (is_canonical_line(content, stamp), path, lineno)


def now_scan(stamp: str, path: str = "") -> tuple:
    """Возвращает (штампы [(файл, строка)], число упоминаний в тексте).

    Печатать `файл:строку` предложил @FE (`chat/fe.md` `[2026-11-05 14:31]`): при поиске
    по всем чатам вердикт «СЕЙЧАС» без адреса неоднозначен, а «упоминаний» и «штампов» —
    разные числа (та же ловушка «122 против 59»).
    """
    args = ["grep", "-n", "-F", stamp]
    if path:
        args += ["--", path]
    rc, out = git(*args)
    stamps, mentions = [], 0
    for line in out.splitlines():
        ok, f, n = parse_grep_line(line, stamp)
        if ok:
            stamps.append((f, n))
        elif f:
            mentions += 1
    return stamps, mentions


def selftest() -> int:
    line_cases = [
        ("- [2026-11-16 11:45] → @TE: текст", "2026-11-16 11:45", True, "штамп в начале строки"),
        ("- [2026-11-05 14:04] → @TE: метка `2026-11-16 11:45` не фантом", "2026-11-16 11:45", False,
         "упоминание в тексте — НЕ штамп"),
        ("  - [2026-11-16 11:45] → @TL: с отступом", "2026-11-16 11:45", True, "отступ допустим"),
        ("[2026-11-16 11:45] без дефиса", "2026-11-16 11:45", False, "нет канон-префикса"),
    ]
    cases = [
        (3, 0, "СЕЙЧАС", "есть в текущем файле"),
        (1, 5, "СЕЙЧАС", "есть и сейчас, и в истории"),
        (0, 3, "ТОЛЬКО В ИСТОРИИ", "исправлена, но была"),
        (0, 0, "НЕТ НИГДЕ", "не существует"),
        (0, 1, "ТОЛЬКО В ИСТОРИИ", "одного коммита достаточно"),
    ]
    parse_cases = [
        ("5-tech-backlog/chat/fe.md:250:- [2026-11-16 11:45] → @TE: текст", "2026-11-16 11:45",
         (True, "5-tech-backlog/chat/fe.md", "250"), "штамп с адресом"),
        ("5-tech-backlog/chat/fe.md:251:- [2026-11-05 14:04] → @TE: метка 2026-11-16 11:45", "2026-11-16 11:45",
         (False, "5-tech-backlog/chat/fe.md", "251"), "упоминание с адресом"),
        ("мусор без двоеточий", "2026-11-16 11:45", (False, "", ""), "не parseable"),
    ]
    ok = 0
    for line, stamp, want, label in parse_cases:
        got = parse_grep_line(line, stamp)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    for content, stamp, want, label in line_cases:
        got = is_canonical_line(content, stamp)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    for now, hist, want, label in cases:
        got = classify(now, hist)
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    filter_cases = [
        ([("a1", "2026-01-01", "канон"), ("a2", "2026-01-02", "цитата")],
         {"a1": 0, "a2": 1}, ["a1"], "цитата отброшена, канон оставлен"),
        ([("b1", "2026-01-01", "только цитаты")], {"b1": 1}, [],
         "нет канона в истории -> НЕТ НИГДЕ (это и был дефект @TL 14:32)"),
        ([("c1", "2026-01-01", "канон 1"), ("c2", "2026-01-02", "канон 2")],
         {"c1": 0, "c2": 0}, ["c1", "c2"], "оба каноны"),
    ]
    for cands, rc_map, want, label in filter_cases:
        got = [c[0] for c in filter_canonical(cands, lambda sha, m=rc_map: m.get(sha, 1))]
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {label}: got={got}, want={want}")
    # кейсы на ТЕКСТ (норма @TL `15:38`): нужное значение есть, ложное — отсутствует
    text_cases = [
        (0, 0, [], [], "НЕТ НИГДЕ",
         ["НЕТ НИГДЕ", "штампов 0"], ["ТОЛЬКО В ИСТОРИИ", "None"],
         "нет нигде: вердикт есть, ложного вердикта и None нет"),
        (0, 3, [], [("bcc6e822", "2026-11-05", "фикс")],
         "ТОЛЬКО В ИСТОРИИ",
         ["ТОЛЬКО В ИСТОРИИ", "штампов 0", "с КАНОНИЧЕСКИМ штампом 1", "метка была и исчезла"],
         ["НЕТ НИГДЕ", "None"],
         "только в истории: вердикт + предупреждение, «нет нигде» и None отсутствуют"),
        (2, 4, [("fe.md", "246"), ("fe.md", "247")], [], "СЕЙЧАС",
         ["СЕЙЧАС", "штампов 2", "fe.md:246", "упоминаний в тексте: 4"],
         ["ТОЛЬКО В ИСТОРИИ", "НЕТ НИГДЕ", "None"],
         "сейчас: адрес штампа напечатан, ложных вердиктов и None нет"),
    ]
    for now, mentions, stamps, hist, verdict, must, must_not, label in text_cases:
        cands = [("c%02d" % i, "2026-01-01", "s") for i in range(len(hist))]
        text = "\n".join(report_lines("2026-01-01 00:00", "chat", stamps, mentions,
                                       cands, hist, verdict))
        miss = [m for m in must if m not in text]
        bad = [m for m in must_not if m in text]
        if not miss and not bad:
            ok += 1
        else:
            print(f"  [FAIL] текст: {label}: нет {miss}, лишнее {bad}")

    broken_text = "\n".join(report_lines("2026-01-01 00:00", "None", [], 0, [], [], "НЕТ НИГДЕ"))
    if "None" in broken_text:
        ok += 1
    else:
        print("  [FAIL] мета: подмена None не детектируется — текстовый кейс ничего не проверяет")
    meta_cases = 1

    total = (meta_cases + len(text_cases) + len(filter_cases) + len(parse_cases)
             + len(line_cases) + len(cases))
    print(f"find_stamp --selftest: {ok}/{total} (история + различение «штамп / упоминание»)")
    return 0 if ok == total else 1


def report_lines(stamp: str, path: str, stamps: list, mentions: int,
                 candidates: list, hist: list, verdict: str) -> list:
    """Текст квитанции отдельной функцией — чтобы `--selftest` проверял **текст**, а не только логику.

    Норма @TL `chat/tl.md` `[2026-11-05 15:38]`: гейт, печатающий числа, обязан иметь кейс на текст
    (нужное значение присутствует, ложное — отсутствует) с негативным контролем. Иначе подкласс
    «гейт считает верно, а текст врёт» живёт в чате, а не в CI.
    """
    where = ", ".join(f"{f}:{n}" for f, n in stamps[:3])
    lines = [
        f"find_stamp: метка «{stamp}» в {path or 'репозитории'}",
        f"  текущее состояние: штампов {len(stamps)}" + (f" ({where})" if where else "")
        + f" | упоминаний в тексте: {mentions}",
        f"  история: кандидатов {len(candidates)}, с КАНОНИЧЕСКИМ штампом {len(hist)}",
    ]
    for h in hist:
        lines.append(f"    {h[0]} {h[1]} {str(h[2])[:80]}")
    lines.append(f"  ВЕРДИКТ: {verdict}")
    if verdict == "ТОЛЬКО В ИСТОРИИ":
        lines.append("  ::warning::find_stamp: метка была и исчезла (правка меток) — "
                     "«нет в origin» ≠ «фантом»; смотреть `git show <commit>^:<file>`")
    return lines


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--stamp", help="метка целиком, например '2026-11-16 11:45'")
    ap.add_argument("--file", default="", help="ограничить одним файлом (по умолчанию все чаты)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    if not args.stamp:
        print("::error::find_stamp: нужен --stamp (rc=2)")
        return 2
    rc, _ = git("rev-parse", "--is-inside-work-tree")
    if rc != 0:
        print("::error::find_stamp: не git-репозиторий (rc=2)")
        return 2

    path = args.file or "5-tech-backlog/chat"
    stamps, mentions = now_scan(args.stamp, path)
    now = len(stamps)
    candidates = history_commits(args.stamp, path)
    hist = canonical_in_history(args.stamp, candidates, path)
    verdict = classify(now, len(hist))
    print(f"find_stamp: метка «{args.stamp}» в {path or 'репозитории'}")
    for line in report_lines(args.stamp, path, stamps, mentions, candidates, hist, verdict):
        print(line)
    if verdict == "НЕТ НИГДЕ":
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
