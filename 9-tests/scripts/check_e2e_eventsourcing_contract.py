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

# TE-089: тип может быть «используется, но не определён» — это ошибка компиляции (CS0246), из-за которой
# сборка сервиса красная, job `docker` = skipped и образы не публикуются (а значит `images.lock` не двигается
# и `deploy-prod` не запускается). Проверяется статически: если есть ссылки на `IValueObjectJsonConverter`,
# где-то в `6-msa` должно быть `class IValueObjectJsonConverter`.
CONVERTER_TYPE = "IValueObjectJsonConverter"
# Исключение: владелец зоны уже правит (BE-089: конвертер вынесен в Shared, файл ещё не закоммичен).
KNOWN_UNDEFINED = {
    CONVERTER_TYPE: "BE-089: файл 6-msa/Shared/XDiary.Contracts/IValueObjectJsonConverter.cs ещё не в origin "
                    "(CS0246 в ci-partners/ci-journal → docker skipped → образы не публикуются)",
}

# Известные исключения: асимметрия признана и исправляется владельцем зоны.
# Исключение НЕ скрывает проблему: оно печатается в квитанции отдельным числом, и запись удаляется,
# когда фикс попал в `origin`.
# Список пуст: 2026-11-06 конвертер добавлен в `BC-Journal` (`JournalEventConsumer` пишет с `jsonOptions`),
# поэтому исключение снято — гейт снова краснеет на любой новой асимметрии без исключений.
KNOWN_GAPS = {}


def collect_definitions(root):
    """{тип: [файлы с определением]} по всем .cs в `6-msa`."""
    base = root / "6-msa"
    defs = {}
    if not base.is_dir():
        return defs
    for f in base.rglob("*.cs"):
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if "class " + CONVERTER_TYPE in text:
            defs.setdefault(CONVERTER_TYPE, []).append(str(f.relative_to(root)).replace("\\", "/"))
    return defs


def check_definitions(root):
    """(issues, known). Ссылки без определения = ошибка компиляции."""
    base = root / "6-msa"
    if not base.is_dir():
        return [], []
    refs = []
    for f in base.rglob("*.cs"):
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if CONVERTER_TYPE in text and "class " + CONVERTER_TYPE not in text:
            refs.append(str(f.relative_to(root)).replace("\\", "/"))
    if not refs:
        return [], []
    if collect_definitions(root).get(CONVERTER_TYPE):
        return [], []
    msg = (f"`{CONVERTER_TYPE}` используется в {len(refs)} файле(ах) "
           f"({', '.join(sorted(r.split('/')[-1] for r in refs))}), но НИГДЕ не определён — "
           f"это ошибка компиляции CS0246: сборка сервиса красная, образы не публикуются")
    if CONVERTER_TYPE in KNOWN_UNDEFINED:
        return [], [msg]
    return [msg], []


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
    def_issues, def_known = check_definitions(pathlib.Path(a.root))
    issues = issues + def_issues
    stats["known"] = stats.get("known", 0) + len(def_known)
    for m in def_known:
        print(f"::warning::check_e2e_eventsourcing_contract: {m} (известное исключение, владелец зоны в курсе)")
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

    # TE-089: контроль детектора «используется, но не определено» (класс CS0246).
    import os as _os
    import shutil as _sh
    root = pathlib.Path(_os.environ.get("TEMP", ".")) / ("esdef-" + str(_os.getpid()))
    _sh.rmtree(root, ignore_errors=True)
    (root / "6-msa" / "BC-X").mkdir(parents=True)
    (root / "6-msa" / "BC-X" / "Consumer.cs").write_text(
        "jsonOptions.Converters.Add(new IValueObjectJsonConverter());", encoding="utf-8")
    saved = dict(KNOWN_UNDEFINED)
    KNOWN_UNDEFINED.clear()
    total += 1
    d_issues, d_known = check_definitions(root)
    if d_issues and not d_known and "CS0246" in d_issues[0]:
        ok += 1
    else:
        print(f"  [FAIL] определение: без `class` должен быть issue с CS0246, а вышло {d_issues} / {d_known}")

    total += 1
    (root / "6-msa" / "BC-X" / "Conv.cs").write_text(
        "public sealed class IValueObjectJsonConverter {}", encoding="utf-8")
    d_issues2, _k2 = check_definitions(root)
    if not d_issues2:
        ok += 1
    else:
        print(f"  [FAIL] определение: с `class` проверка должна молчать, а вышло {d_issues2}")

    total += 1
    KNOWN_UNDEFINED.update(saved)
    d_issues3, d_known3 = check_definitions(root.parent / "nonexistent")
    if not d_issues3 and not d_known3:
        ok += 1
    else:
        print(f"  [FAIL] без 6-msa проверка должна молчать")

    print(f"check_e2e_eventsourcing_contract --selftest: {ok}/{total} "
          f"(писатель событий без `IValueObjectJsonConverter` = rc=1: иначе модифицирующие ручки дают 404)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
