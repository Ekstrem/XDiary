#!/usr/bin/env python3
"""XDiary: `read-paths.md` §2 (реестр экранов) ↔ `api-index.yaml` — машинная сверка (TE-057).

Задача (`04-architecture/read-paths.md` §3 п.3, §5 п.3): «список MV на экране = контракт».
Экран не должен обещать данных, которых нет в контракте, и MV-эндпоинт, который читает экран,
обязан быть назван в реестре §2. Раньше это проверялось ревью — здесь проверкой.

Как читается реестр: в §2 источники записаны как вызовы фронта (`Api.journal.recent()`), а в
индексе — как `operationId` (`Journal.Recent`). Соответствие задаёт **таблица `SOURCES`** ниже:
каждая строка — (вызов фронта, ожидаемый `operationId`). Таблица явная, потому что автоматическое
угадывание «Api.journal.remove → Journal.Delete» — это догадка, а не контракт; новая строка
реестра требует новой строки здесь, и это видно в диффе.

Проверки (обе стороны):
  A. §2 → индекс: каждый источник §2 обязан существовать в `api-index.yaml`;
  B. индекс → §2: каждый MV-эндпоинт индекса, который **читает фронт** (из `READS` ниже),
     обязан быть назван в §2 (по `operationId` или по вызову фронта).

Коды возврата: 0 — совпадает; 1 — расхождения (список с именами); 2 — вход не читается.

Запуск:
  python 9-tests/scripts/check_read_paths.py [--root .] [--selftest]
"""
from __future__ import annotations

import argparse
import os
import re
import sys

try:                                     # UTF-8 вывод в cp1251-консоли
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:                        # noqa: BLE001
    pass

# §2: вызов фронта → operationId из api-index.yaml
SOURCES = [
    ("Api.partners.list()", "Partners.List"),
    ("Api.journal.recent()", "Journal.Recent"),
    ("Api.journal.places()", "Journal.Places"),
    ("Api.catalog.list()", "Catalog.List"),
    ("Api.journal.list()", "Journal.List"),
    ("Api.clients.me()", "Client.Me"),
]

# MV-эндпоинты, которые читает фронт (направление B): operationId обязаны быть в §2
MV_READ_BY_FRONT = [
    "Journal.Recent",
    "Journal.Places",
]


def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8") as fh:
        return fh.read()


def load_operations(index_path: str) -> dict:
    """operationId → (method, path). Без PyYAML не падаем: простой разбор списка операций."""
    text = read_text(index_path)
    try:
        import yaml  # noqa: PLC0415
        doc = yaml.safe_load(text) or {}
        out = {}
        for op in doc.get("operations") or []:
            out[str(op.get("operationId"))] = (str(op.get("method")), str(op.get("path")))
        return out
    except ImportError:
        out = {}
        for m in re.finditer(r"operationId:\s*([\w.]+)", text):
            out[m.group(1)] = ("?", "?")
        return out


def registry_section(md: str) -> str:
    """Тело §2 «Реестр экранов» (между '## 2.' и '## 3.')."""
    start = md.find("## 2.")
    end = md.find("## 3.")
    if start < 0 or end < start:
        raise ValueError("в read-paths.md не найден §2 «Реестр экранов» (или §3 после него)")
    return md[start:end]


def check(md: str, operations: dict) -> list:
    """Возвращает список расхождений. Пустой список = совпадает."""
    problems = []
    section = registry_section(md)

    # A. §2 → индекс: источник назван в реестре, значит ручка обязана быть в контракте
    for call, op_id in SOURCES:
        if call not in section:
            continue                      # источник не заявлен в реестре — см. B
        if op_id not in operations:
            problems.append(f"§2 → индекс: `{call}` обещает операцию `{op_id}`, "
                            f"которой нет в api-index.yaml")

    # B. индекс → §2: MV, который читает фронт, обязан быть назван в реестре
    for op_id in MV_READ_BY_FRONT:
        if op_id not in operations:
            problems.append(f"индекс: ожидаемая MV-операция `{op_id}` отсутствует в api-index.yaml")
            continue
        calls = [call for call, oid in SOURCES if oid == op_id]
        mentioned = op_id in section or any(c in section for c in calls)
        if not mentioned:
            problems.append(f"индекс → §2: MV `{op_id}` читается фронтом, но не назван в §2 read-paths.md")

    # C. покрытие: каждый вызов `Api.*.*()` в §2 обязан быть в SOURCES.
    #    Иначе новая строка реестра молча выпадает из сверки, и гейт остаётся зелёным
    #    ровно на том, что должен ловить («контроль, который не может провалиться»).
    declared = {call for call, _ in SOURCES}
    in_registry = set(re.findall(r"`(Api\.[A-Za-z]+\.[A-Za-z]+\(\))`", section))
    for call in sorted(in_registry - declared):
        problems.append(f"покрытие: `{call}` есть в §2, но нет в таблице SOURCES — "
                        f"этот источник не сверяется с api-index.yaml (добавь строку в SOURCES)")
    return problems


def reads_from_text(text: str, operations: dict) -> list:
    """Какие GET-операции индекса встречаются в тексте фронта (чистая функция — тестируема).

    Считаются только `GET` и только **конкретные** пути: шаблон `{id}` обрезается до префикса,
    чтобы `/api/v1/journal/records/{id}` не «совпал» с `/api/v1/journal/records` случайно.
    """
    found = []
    for op_id, (method, path) in operations.items():
        if str(method).upper() != "GET":
            continue
        concrete = re.sub(r"\{[^}]+\}", "", str(path)).rstrip("/")
        if concrete and concrete in text:
            found.append(op_id)
    return sorted(set(found))


def front_reads(root: str, operations: dict) -> list:
    """Мера полноты реестра, которую требует @AR (`read-paths.md` §2.1, 2026-11-05).

    Гейт обязан печатать «зарегистрировано N из M чтений фронта» и падать **только** на расхождении
    зарегистрированного: неполнота §2 — видимый пробел, не красный гейт.
    """
    js_dir = os.path.join(root, "8-front", "js")
    text = ""
    try:
        for name in sorted(os.listdir(js_dir)):
            if name.endswith(".js"):
                text += read_text(os.path.join(js_dir, name))
    except OSError:
        return []                       # фронта рядом нет — полноту не измеряем
    return reads_from_text(text, operations)


def run(root: str) -> int:
    md_path = os.path.join(root, "04-architecture", "read-paths.md")
    idx_path = os.path.join(root, "04-architecture", "api-index.yaml")
    try:
        md = read_text(md_path)
        operations = load_operations(idx_path)
    except OSError as e:
        print(f"::error::check_read_paths: не читается вход ({e}); проверять нечего (rc=2)")
        return 2
    except ValueError as e:
        print(f"::error::check_read_paths: структура read-paths.md не та ({e}) — rc=2")
        return 2
    if not operations:
        print(f"::error::check_read_paths: в {idx_path} не найдено ни одной операции (rc=2)")
        return 2

    try:
        problems = check(md, operations)
        section = registry_section(md)
    except ValueError as e:
        print(f"::error::check_read_paths: структура read-paths.md не та ({e}) — rc=2")
        return 2

    # полнота реестра: N из M (§2.1) — печатаем всегда, на вердикт не влияет
    readings = front_reads(root, operations)
    registered = {op_id for call, op_id in SOURCES if call in section} & set(readings)
    outside = [op_id for op_id in readings if op_id not in registered]
    coverage = f"проверено {len(registered)} из {len(readings)} чтений фронта"

    for line in summary_lines(problems, coverage, len(operations), len(outside), outside):
        print(line)
    return 1 if problems else 0


def summary_lines(problems: list, coverage: str, ops: int, n_outside: int,
                  outside: list) -> list:
    """Текст сводки отдельной функцией — чтобы `--selftest` проверял **текст**, а не только `rc`.

    Норма @TL `chat/tl.md` `[2026-11-05 15:38]`: гейт, печатающий числа, обязан иметь кейс на текст
    (нужное значение присутствует, ложное — отсутствует). Здесь проверяются три вещи:
    покрытие «N из M», отдельные числа и отсутствие `None` в выводе.
    """
    lines = [f"::error::check_read_paths: {p}" for p in problems]
    if problems:
        lines.append(f"FAIL: {len(problems)} расхождений §2 ↔ api-index "
                     f"({coverage}; операций в индексе: {ops})")
        return lines
    lines.append(f"PASS: §2 и api-index согласованы ({coverage}; источников реестра: {len(SOURCES)}, "
                 f"MV читаются фронтом: {len(MV_READ_BY_FRONT)}; операций в индексе: {ops})")
    if outside:
        lines.append(f"INFO: вне реестра §2 {n_outside} чтений — известный пробел §2.1, не ошибка "
                     f"(не считаем красным, иначе гейт будут отключать): {', '.join(outside)}")
    return lines


# ───────────────────────────── selftest ─────────────────────────────
GOOD_MD = """# read-paths

## 2. Реестр экранов

| Экран / действие | Данные | Источник |
|---|---|---|
| Визард | партнёры | `Api.partners.list()` → `Partners.List` |
| | recent | `Api.journal.recent()` — MV |
| | places | `Api.journal.places()` — MV |
| | каталог | `Api.catalog.list()` → `Catalog.List` |
| Журнал | записи | `Api.journal.list()` |
| Профиль | свой профиль | `Api.clients.me()` |

## 3. Что требуется от @FE
"""

GOOD_OPS = {
    "Partners.List": ("GET", "/api/v1/partners"),
    "Journal.Recent": ("GET", "/api/v1/journal/recent"),
    "Journal.Places": ("GET", "/api/v1/journal/places"),
    "Catalog.List": ("GET", "/api/v1/catalog"),
    "Journal.List": ("GET", "/api/v1/journal/records"),
    "Client.Me": ("GET", "/api/v1/clients/me"),
}


def selftest() -> int:
    """Красный контроль: каждый кейс обязан падать по своей причине."""
    cases = []

    cases.append(("норма: §2 совпадает с индексом", GOOD_MD, dict(GOOD_OPS), 0))
    # лишний operationId в §2: реестр обещает ручку, которой нет в контракте
    ops_missing = dict(GOOD_OPS)
    ops_missing.pop("Journal.Recent")
    cases.append(("лишний в §2 → rc=1", GOOD_MD, ops_missing, 1))
    # пропавший из §2: MV читается фронтом, но в реестре не назван
    md_wo_recent = GOOD_MD.replace("| | recent | `Api.journal.recent()` — MV |\n", "")
    cases.append(("пропавший из §2 → rc=1", md_wo_recent, dict(GOOD_OPS), 1))
    # индекс вообще пуст → rc=2-семантика проверяется в run(), здесь — «MV нет в индексе»
    ops_no_places = dict(GOOD_OPS)
    ops_no_places.pop("Journal.Places")
    cases.append(("MV отсутствует в индексе → rc=1", GOOD_MD, ops_no_places, 1))
    # источник в §2, которого нет в SOURCES → сверка его «молча» не покрывает (направление C)
    md_new_source = GOOD_MD.replace(
        "| Профиль | свой профиль | `Api.clients.me()` |",
        "| Профиль | свой профиль | `Api.clients.me()` |\n| Фантазии | список | `Api.journal.fantasies()` |",
    )
    cases.append(("источник вне SOURCES → rc=1", md_new_source, dict(GOOD_OPS), 1))

    ok = 0
    for name, md, ops, want in cases:
        try:
            problems = check(md, ops)
            got = 1 if problems else 0
        except ValueError:
            got = 2
        if got == want:
            ok += 1
        else:
            print(f"  [FAIL] {name}: получили rc={got}, ждали {want}; расхождения={problems}")
    # структура: §2 без §3 обязана давать rc=2
    try:
        check("# read-paths\n\n## 2. Реестр экранов\n\n| a | b |\n", dict(GOOD_OPS))
        struct_ok = False
    except ValueError:
        struct_ok = True
    if struct_ok:
        ok += 1
    else:
        print("  [FAIL] структура: отсутствие §3 не дало ошибки чтения (ждали rc=2)")

    # Красный контроль подсчёта полноты: функция без своего контроля — «проверка, которая не
    # может провалиться». Проверяем **чистую** функцию на строках (без файловой системы:
    # sandbox не даёт создавать каталоги в %TEMP%, и это к лучшему — контроль не зависит от ФС).
    ops_case = {
        "Partners.List": ("GET", "/api/v1/partners"),
        "Journal.Get": ("GET", "/api/v1/journal/records/{id}"),
        "Journal.List": ("POST", "/api/v1/journal/records"),
    }
    # (а) конкретный GET-путь считается, POST — нет, шаблон {id} не «совпадает» сам с собой
    got = reads_from_text('fetch("/api/v1/partners");', ops_case)
    if got == ["Partners.List"]:
        ok += 1
    else:
        print(f"  [FAIL] reads_from_text: получили {got}, ждали ['Partners.List'] "
              f"(POST и {{id}}-шаблоны чтениями не считаются)")
    # (б) префикс шаблонного пути тоже считается (журнал читается как /records)
    got_prefix = reads_from_text('get("/api/v1/journal/records?limit=5");', ops_case)
    if got_prefix == ["Journal.Get"]:
        ok += 1
    else:
        print(f"  [FAIL] reads_from_text/префикс: получили {got_prefix}, ждали ['Journal.Get']")
    # (в) фронта рядом нет → пусто, а не падение
    got_none = front_reads("нет-такой-папки", ops_case)
    if got_none == []:
        ok += 1
    else:
        print(f"  [FAIL] front_reads без 8-front/js: получили {got_none}, ждали []")

    total = len(cases) + 1 + 3   # кейсы + структурный + три контроля полноты
    # кейс на ТЕКСТ (норма @TL `15:38`): покрытие, числа, отсутствие None
    cov = "проверено 6 из 18 чтений фронта"
    good = "\n".join(summary_lines([], cov, 80, 12, ["Journal.List", "Client.Me"]))
    bad = "\n".join(summary_lines(["A: расхождение"], cov, 80, 0, []))
    must = ["проверено 6 из 18", "источников реестра", "операций в индексе: 80",
            "вне реестра §2 12 чтений"]
    must_not = ["None", "FAIL:"]
    must_bad = ["FAIL:", "расхождений", "None"]
    if (all(m in good for m in must) and not any(m in good for m in must_not)
            and all(m in bad for m in must_bad[:2]) and "None" not in bad):
        ok += 1
    else:
        print(f"  [FAIL] текст: good={good!r} / bad={bad!r}")

    total += 1
    print(f"check_read_paths --selftest: {ok}/{total} (красный контроль на каждом направлении)")
    return 0 if ok == total else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    return run(args.root)


if __name__ == "__main__":
    raise SystemExit(main())
