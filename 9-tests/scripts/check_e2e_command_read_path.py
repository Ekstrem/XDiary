"""check_e2e_command_read_path.py — командный репозиторий не должен читать ТОЛЬКО read-модель.

Реальный дефект (`BE-088`, 2026-11-06): `JournalCommandRepository` и `PartnerCommandRepository` стали брать
`GetByIdAsync` из query-репозитория (read-модель), а read-модель наполняется проекцией **асинхронно**.
Итог на живом стенде: запись создана секунду назад → в `journal_read`/`partners_read` её ещё нет →
`GetByIdAsync` возвращает `null` → `EnsureOwns` отдаёт **404** (`when` и все мутации записи), а обработчики
Partners — `Fail("Partner not found.")` → **400** (`remove`, `link-client`). Один лаг объясняет оба симптома,
и из-за него красные `smoke-api`/`smoke-front`, а значит `deploy-prod` = skipped.

Правило: файл `*CommandRepository.cs` с `GetByIdAsync`, который читает `_readRepo` (read-модель), обязан
иметь **второй шаг** — обращение к источнику истины при промахе (`_db`, `CommandDbContext`, event-store,
`IEventStoreRepository`, `_store`, `SaveEventAsync`-совместимый репозиторий) или явный комментарий `fallback`.

Коды: `0` — все командные чтения с fallback (или исключения объявлены); `1` — есть чтение только read-модели;
`2` — не измерено (нет исходников).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

READ_MODEL = re.compile(r"_readRepo\.GetByIdAsync|_read\.GetByIdAsync|readRepo\.GetById")
FALLBACK = re.compile(r"_db\.|CommandDbContext|IEventStoreRepository|_store\.|fallback|EventStore", re.I)
METHOD = re.compile(r"GetByIdAsync\s*\(")

# Известные исключения: асимметрия признана и исправляется владельцем зоны (@BE, `BE-088`).
# Печатаются в квитанции отдельным числом — не скрываются.
KNOWN_GAPS = {
    "6-msa/BC-Journal/src/Journal.Storage/JournalCommandRepository.cs":
        "BE-088: GetByIdAsync читает только read-модель, CommandDbContext в классе нет (404 на свежей записи)",
    "6-msa/BC-Partners/src/Partners.Storage/PartnerCommandRepository.cs":
        "BE-088: CommandDbContext есть, но GetByIdAsync читает read-модель (400 «Partner not found.»)",
}


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


def collect(root):
    base = root / "6-msa"
    if not base.is_dir():
        return None, "нет папки 6-msa — не измерено"
    files = {}
    for p in base.rglob("*CommandRepository.cs"):
        try:
            files[str(p.relative_to(root)).replace("\\", "/")] = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
    if not files:
        return None, "не найдено ни одного *CommandRepository.cs — не измерено"
    return files, None


def receipt_line(stats, issues):
    return (f"квитанция: командных репозиториев {stats['repos']} | "
            f"без fallback: {len(issues)} | известных исключений: {stats.get('known', 0)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    files, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_command_read_path: {err} (rc=2)")
        return 2
    issues, stats = evaluate(files)
    for m in issues:
        print(f"::error::check_e2e_command_read_path: {m}")
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

    total += 1
    line = receipt_line({"repos": 3, "known": 1}, [])
    if "командных репозиториев 3" in line and "известных исключений: 1" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_command_read_path --selftest: {ok}/{total} "
          f"(командное чтение только из read-модели = rc=1: лаг проекции даёт 404/400 на свежих данных)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
