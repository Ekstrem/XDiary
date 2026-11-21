"""check_e2e_command_read_path.py — командный путь не должен читать ТОЛЬКО read-модель.

Реальный дефект (`BE-088`, 2026-11-06): `JournalCommandRepository` и `PartnerCommandRepository` стали брать
`GetByIdAsync` из query-репозитория (read-модель), а read-модель наполняется проекцией **асинхронно**.
Итог на живом стенде: запись создана секунду назад → в `journal_read`/`partners_read` её ещё нет →
`GetByIdAsync` возвращает `null` → `EnsureOwns` отдаёт **404** (`when` и все мутации записи), а обработчики
Partners — `Fail("Partner not found.")` → **400** (`remove`, `link-client`). Один лаг объясняет оба симптома,
и из-за него красные `smoke-api`/`smoke-front`, а значит `deploy-prod` = skipped.

Архитектурное изменение (`bb2b4b275`, 2026-11-17, @BE): `JournalCommandRepository` **удалён** —
хендлеры теперь идут через AggregateProvider→Notifier→BusAdapter, и файлов `*CommandRepository.cs`
в кодовой базе больше нет. Поэтому гейт разделён на две части:

1. **Наследие (legacy):** если `*CommandRepository.cs` когда-либо вернётся — прежнее правило BE-088
   (чтение только read-модели без fallback в источник истины = красное). Пустой список таких файлов
   — это ожидание, а НЕ «не измерено».
2. **Инвариант (актуальный):** обращение к read-модели (`_readRepo`) разрешено **только** в файлах
   `*EventConsumer.cs` / `*Projection.cs` (они её и наполняют). Любой другой файл — нарушение того
   же класса `BE-088` в новом виде (командный путь подсматривает в read-модель).

Коды: `0` — нарушений нет (legacy-файлы с fallback или отсутствуют; `_readRepo` только у консьюмеров/проекций);
      `1` — есть нарушение (legacy-чтение без fallback ИЛИ `_readRepo` вне разрешённых файлов);
      `2` — не измерено (нет папки `6-msa`).
"""

import argparse
import pathlib
import sys as _sys
_sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from filelist_source import tracked_or_walk  # норма @AR 23:30: список из git ls-files, walk — фолбэк

import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

READ_MODEL = re.compile(r"_readRepo\.GetByIdAsync|_read\.GetByIdAsync|readRepo\.GetById")
FALLBACK = re.compile(r"_db\.|CommandDbContext|IEventStoreRepository|_store\.|fallback|EventStore", re.I)
METHOD = re.compile(r"GetByIdAsync\s*\(")
READREPO_ANY = re.compile(r"_readRepo\s*\.")

# bb2b4b275: чтение read-модели разрешено только тем, кто её наполняет (консьюмеры) и проекциям.
ALLOWED_READREPO = ("EventConsumer.cs", "Projection.cs")

# Известные исключения: асимметрия признана и исправляется владельцем зоны (@BE, `BE-088`).
# Печатаются в квитанции отдельным числом — не скрываются.
KNOWN_GAPS = {
    "6-msa/BC-Journal/src/Journal.Storage/JournalCommandRepository.cs":
        "BE-088: GetByIdAsync читает только read-модель, CommandDbContext в классе нет (404 на свежей записи)",
    "6-msa/BC-Partners/src/Partners.Storage/PartnerCommandRepository.cs":
        "BE-088: CommandDbContext есть, но GetByIdAsync читает read-модель (400 «Partner not found.»)",
}


def _skip_nested_repos(paths, root):
    """Пропустить файлы внутри вложенных worktree/клонов.

    Причина (@AR `2026-11-16 07:00` про `check_canon`): обход `glob`/`rglob` видит **вложенный worktree**
    как обычные файлы, и вердикт гейта начинает зависеть от того, кто где сделал `git worktree add`.
    «У автора зелено, у соседа красно» — это дефект гейта, а не кода. Файлы внутри любого каталога,
    где есть `.git`, кроме самого корня, не считаем.
    """
    root = pathlib.Path(root).resolve()
    out = []
    for p in paths:
        try:
            rel = pathlib.Path(p).resolve().relative_to(root)
        except ValueError:
            out.append(p)
            continue
        if any((root / part / ".git").exists() for part in [pathlib.Path(*rel.parts[:i + 1])
                                                            for i in range(len(rel.parts) - 1)]):
            continue
        out.append(p)
    return out


def _method_body(lines, start):
    """Тело метода начиная со строки start: до закрывающей `}` на том же уровне (грубо, по отступу)."""
    depth = 0
    body = []
    for i in range(start, min(start + 60, len(lines))):
        body.append(lines[i])
        depth += lines[i].count("{") - lines[i].count("}")
        if depth <= 0 and i > start:
            break
    return "\n".join(body)


def evaluate(files):
    """files: {path: text}. (issues, stats)."""
    issues, repos, known = [], 0, 0
    for path, text in sorted(files.items()):
        lines = text.splitlines()
        starts = [i for i, l in enumerate(lines) if METHOD.search(l) and "Task" in l]
        if not starts:
            continue
        repos += 1
        body = "\n".join(_method_body(lines, i) for i in starts)
        if not READ_MODEL.search(body):
            continue
        if FALLBACK.search(body):
            continue
        if path in KNOWN_GAPS:
            known += 1
            continue
        issues.append(f"{path}: `GetByIdAsync` читает только read-модель, без fallback в источник истины "
                      f"(лаг проекции → null → 404/400 на свежих данных)")
    stats = {"repos": repos, "known": known}
    return issues, stats


def evaluate_invariant(cs_files):
    """Инвариант: `_readRepo` только в *EventConsumer.cs / *Projection.cs.

    cs_files: {path: text}. Возвращает список нарушений."""
    issues = []
    for path, text in sorted(cs_files.items()):
        if not READREPO_ANY.search(text):
            continue
        if any(path.endswith(sfx) for sfx in ALLOWED_READREPO):
            continue
        issues.append(f"{path}: `_readRepo` вне *EventConsumer.cs/*Projection.cs — "
                      f"командный путь читает read-модель (класс BE-088 в новой форме)")
    return issues


FILES_SOURCE = ""


def collect(root):
    base = root / "6-msa"
    if not base.is_dir():
        return None, None, "нет папки 6-msa — не измерено"
    legacy, src1 = tracked_or_walk(root, "6-msa/**/*CommandRepository.cs")
    # bb2b4b275: старое имя удалено, новое — *EventSourcingRepository.cs (совет @DO 15:31):
    # если когда-либо вернётся чтение read-модели через GetByIdAsync без fallback — ловим и там.
    legacy2, src1b = tracked_or_walk(root, "6-msa/**/*EventSourcingRepository.cs")
    legacy = list(legacy) + [p for p in legacy2 if p not in set(legacy)]
    src1 = f"{src1}+{src1b}"
    all_cs, src2 = tracked_or_walk(root, "6-msa/**/*.cs")
    global FILES_SOURCE
    FILES_SOURCE = f"{src1}+{src2}"
    files = {}
    for p in legacy:
        try:
            files[str(p.relative_to(root)).replace("\\", "/")] = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
    cs = {}
    for p in all_cs:
        try:
            cs[str(p.relative_to(root)).replace("\\", "/")] = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
    return files, cs, None


def receipt_line(stats, issues, inv_issues):
    return (f"квитанция: командных репозиториев {stats['repos']} | без fallback: {len(issues)} | "
            f"известных исключений: {stats.get('known', 0)} | "
            f"инвариант `_readRepo` вне EventConsumer/Projection: {len(inv_issues)} | "
            f"источник: {FILES_SOURCE}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    files, cs, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_command_read_path: {err} (rc=2)")
        return 2
    issues, stats = evaluate(files)
    inv_issues = evaluate_invariant(cs)
    for m in issues + inv_issues:
        print(f"::error::check_e2e_command_read_path: {m}")
    print(receipt_line(stats, issues, inv_issues))
    return 1 if (issues or inv_issues) else 0


def selftest():
    ok = total = 0

    def case(name, files, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(files)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues, [])
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    def case_inv(name, files, want_n, must=()):
        nonlocal ok, total
        total += 1
        inv = evaluate_invariant(files)
        text = "\n".join(inv)
        miss = [m for m in must if m not in text]
        if len(inv) == want_n and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: нарушений={len(inv)} (ждали {want_n}), нет {miss}; text={text[:200]}")

    with_fb = {"src/BC-X/Storage/XCommandRepository.cs": (
        "public async Task<IModel?> GetByIdAsync(Guid id, CancellationToken ct = default)\n"
        "{\n"
        "  var read = await _readRepo.GetByIdAsync(id, ct);\n"
        "  if (read is null) return await _db.Events.GetById(id, ct);\n"
        "  return read;\n"
        "}\n"
    )}
    case("read-модель + fallback — зелёный", with_fb, 0, ["командных репозиториев 1", "без fallback: 0"])

    broken = {"src/BC-Y/Storage/YCommandRepository.cs": (
        "public async Task<IModel?> GetByIdAsync(Guid id, CancellationToken ct = default)\n"
        "{\n"
        "  var read = await _readRepo.GetByIdAsync(id, ct);\n"
        "  if (read is null) return null;\n"
        "  return read;\n"
        "}\n"
    )}
    case("только read-модель — красный", broken, 1, ["YCommandRepository.cs", "404/400"])

    case("без GetByIdAsync не считается",
         {"src/BC-Z/Storage/ZCommandRepository.cs": "public Task SaveAsync() => Task.CompletedTask;\n"},
         0, ["командных репозиториев 0"])

    case_inv("consumer с _readRepo — разрешено",
             {"6-msa/BC-Journal/src/Journal.Api/Events/JournalEventConsumer.cs":
              "var x = await _readRepo.GetByIdAsync(id);\n"},
             0)
    case_inv("проекция с _readRepo — разрешено",
             {"6-msa/BC-Journal/src/Journal.Api/Events/JournalReadProjection.cs":
              "_readRepo.Save(item);\n"},
             0)
    case_inv("хендлер с _readRepo — нарушение (BE-088 в новой форме)",
             {"6-msa/BC-Journal/src/Journal.Application/Handlers/WhenHandler.cs":
              "var rec = await _readRepo.GetByIdAsync(id, ct);\n"},
             1, ["WhenHandler.cs", "BE-088"])
    case_inv("файл без _readRepo — зелёный",
             {"6-msa/BC-Journal/src/Journal.Api/Program.cs": "var app = builder.Build();\n"},
             0)

    total += 1
    line = receipt_line({"repos": 3, "known": 1}, [], [])
    if "командных репозиториев 3" in line and "известных исключений: 1" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    total += 1
    line2 = receipt_line({"repos": 0, "known": 0}, [], [])
    if "командных репозиториев 0" in line2 and "инвариант `_readRepo` вне EventConsumer/Projection: 0" in line2:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции (пустой legacy): {line2}")

    print(f"check_e2e_command_read_path --selftest: {ok}/{total} "
          f"(командное чтение только из read-модели = rc=1; `_readRepo` вне EventConsumer/Projection = нарушение)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
