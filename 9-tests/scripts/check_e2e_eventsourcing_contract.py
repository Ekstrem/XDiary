"""check_e2e_eventsourcing_contract.py — если сервис пишет `ChangedValueObjectsJson`, он обязан уметь его читать.

Реальный дефект (2026-11-06): в `BC-Partners` @BE добавил `IValueObjectJsonConverter` (`b5213f3d`), и запись
события идёт с `jsonOptions` (`PartnerEventConsumer.cs:47-50`). В `BC-Journal` то же место осталось голым:

    ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects),

Без `$type`-дискриминанта библиотека не может восстановить конкретный `IValueObject`, поэтому
`JournalCommandRepository.GetById` (`:15` → `LastOrDefault()`) возвращает `null`, и любой модифицирующий
эндпоинт отвечает `404` (`when`, `place`, `duration`, …), хотя read-модель (список) при этом работает.

Правило: файл, который присваивает `ChangedValueObjectsJson = JsonSerializer.Serialize(...)`, обязан
упоминать конвертер (`IValueObjectJsonConverter`) или `Converters.Add` в том же вызове/файле.
Иначе — `rc=1` с путём и строкой.

Коды: `0` — все писатели событий симметричны; `1` — есть асимметрия; `2` — не измерено (нет исходников).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

WRITE = re.compile(r"ChangedValueObjectsJson\s*=\s*JsonSerializer\.Serialize")
CONVERTER = re.compile(r"IValueObjectJsonConverter|Converters\.Add")

# где лежат сервисы (читаем только, ничего не правим)
SRC_ROOT = ("6-msa",)

# Известные исключения: асимметрия признана и исправляется владельцем зоны.
# Исключение НЕ скрывает проблему: оно печатается в квитанции отдельным числом, и запись удаляется,
# когда фикс попал в `origin`.
KNOWN_GAPS = {
    "6-msa/BC-Journal/src/Journal.Api/Events/JournalEventConsumer.cs":
        "конвертер уже в рабочем дереве @BE, но в origin ещё нет (найдено в прогонах #1638/#1639: when -> 404)",
}


def evaluate(files, root_hint=""):
    """files: {path: text}. (issues, stats)."""
    issues, writers, known = [], 0, 0
    for path, text in sorted(files.items()):
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if not WRITE.search(line):
                continue
            writers += 1
            # конвертер должен быть в этом же файле (в 30 строках выше — там options собирают)
            window = "\n".join(lines[max(0, i - 30):i + 3])
            if not CONVERTER.search(window):
                if path in KNOWN_GAPS:
                    known += 1
                    continue
                issues.append(
                    f"{path}:{i + 1}: пишет `ChangedValueObjectsJson` без `IValueObjectJsonConverter` — "
                    f"event-sourced `GetById` не восстановит `IValueObject`, и модифицирующие ручки ответят 404"
                )
    stats = {"files": len(files), "writers": writers, "known": known}
    return issues, stats


def collect(root):
    base = root / SRC_ROOT[0]
    if not base.is_dir():
        return None, "нет папки 6-msa — не измерено"
    files = {}
    for p in base.rglob("*.cs"):
        try:
            text = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if "ChangedValueObjectsJson" in text:
            files[str(p.relative_to(root)).replace("\\", "/")] = text
    if not files:
        return None, "ни одного писателя `ChangedValueObjectsJson` не найдено — не измерено"
    return files, None


def receipt_line(stats, issues):
    known = stats.get("known", 0)
    return (f"квитанция: файлов с событиями {stats['files']} | писателей {stats['writers']} | "
            f"без конвертера: {len(issues)} | известных исключений: {known}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    files, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_eventsourcing_contract: {err} (rc=2)")
        return 2
    issues, stats = evaluate(files)
    for m in issues:
        print(f"::error::check_e2e_eventsourcing_contract: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, files, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(files)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    good = {
        "src/BC-Partners/Events/PartnerEventConsumer.cs": (
            "var jsonOptions = new JsonSerializerOptions();\n"
            "jsonOptions.Converters.Add(new IValueObjectJsonConverter());\n"
            "var entry = new DomainEventEntry {\n"
            "  ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects, jsonOptions),\n"
            "};\n"
        ),
    }
    case("Partners (конвертер рядом) — зелёный", good, 0, ["писателей 1", "без конвертера: 0"])

    bad = {
        "src/BC-Journal/Events/JournalEventConsumer.cs": (
            "var entry = new DomainEventEntry {\n"
            "  ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects),\n"
            "};\n"
        ),
    }
    case("Journal (без конвертера) — красный", bad, 1,
         ["BC-Journal/Events/JournalEventConsumer.cs:2", "восстановит"])

    case("файл без записи событий — не считается",
         {"src/Other/Thing.cs": "class Thing {}"}, 0, ["писателей 0"])

    total += 1
    line = receipt_line({"files": 5, "writers": 2}, [])
    if "писателей 2" in line and "без конвертера: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_eventsourcing_contract --selftest: {ok}/{total} "
          f"(писатель событий без `IValueObjectJsonConverter` = rc=1: иначе модифицирующие ручки дают 404)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
