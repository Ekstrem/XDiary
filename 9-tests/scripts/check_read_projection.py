#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Гейт: если BC регистрирует read-store/read-модель — у него должен быть консьюмер проекции.

Причина (класс `BE-134`/`BE-137`/`BE-138`, найдено 2026-11-19): в `BC-Catalog` read store был
зарегистрирован (`Program.cs`), события писались, но **консьюмера не было вообще** — поэтому
`catalog_read` оставалась пустой, а `GET /catalog` отдавал `[]`, `GET /catalog/<id>` — 404.
То же было в `Journal` (stale-skip) и `Client` (дифф ключей). Проверять это глазами — раунд
за раундом; дешевле статическим гейтом.

Правило: для каждого `6-msa/BC-*/src/*.Api`:
  * если в `Program.cs` есть регистрация read-модели/read-контекста
    (`*ReadDb*`, `*ReadStore*`, `AddDbContext<*Read*`, `AddReadModel`, `IReadModel`),
  * то в этом же `*.Api` (или в `*.Application`) обязан быть файл консьюмера/проекции
    (`*EventConsumer.cs` / `*Projection.cs` / `*EventHandler.cs`) ИЛИ явная пометка
    `// read-projection: none` в `Program.cs` (осознанный отказ, чтобы не «случайно забыли»).

Коды: 0 — нарушений нет; 1 — есть BC с read-store и без консьюмера; 2 — не найдена папка.
`--selftest` — 4 фикстуры (есть/нет консьюмера/осознанный отказ/без read-store).
"""
import os
import re
import sys

ROOT_DEFAULT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
READ_MARKERS = (
    "AddDbContext<",
    "ReadDb",
    "ReadStore",
    "AddReadModel",
    "IReadModel",
    "ReadDbContext",
)
READ_CONTEXT = re.compile(r"ReadDb|ReadStore|ReadModel|ReadDbContext|AddDbContext<\w*Read")
CONSUMER = re.compile(r"(EventConsumer|Projection|EventHandler)\.cs$")
OPT_OUT = "// read-projection: none"


def bc_projects(root):
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return None
    return sorted(d for d in os.listdir(base)
                  if d.startswith("BC-") and os.path.isdir(os.path.join(base, d, "src")))


def api_project(base, bc):
    src = os.path.join(base, "6-msa", bc, "src")
    if not os.path.isdir(src):
        return None
    for d in os.listdir(src):
        if d.endswith(".Api") and os.path.isdir(os.path.join(src, d)):
            return os.path.join(src, d)
    return None


def read_projection_state(api_dir, base, bc):
    """(has_read_store, has_consumer, opted_out)."""
    prog = os.path.join(api_dir, "Program.cs")
    has_read, opted = False, False
    if os.path.isfile(prog):
        with open(prog, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        has_read = any(m in text for m in READ_MARKERS) and bool(READ_CONTEXT.search(text))
        opted = OPT_OUT in text
    # консьюмер может лежать в *.Api или в соседнем *.Application/*.Domain
    src = os.path.join(base, "6-msa", bc, "src")
    has_consumer = False
    for root_dir, _dirs, files in os.walk(src):
        if any(CONSUMER.search(n) for n in files):
            has_consumer = True
            break
    return has_read, has_consumer, opted


def scan(root):
    bcs = bc_projects(root)
    if bcs is None:
        return None
    rows, bad = [], []
    for bc in bcs:
        api = api_project(root, bc)
        if not api:
            continue
        has_read, has_consumer, opted = read_projection_state(api, root, bc)
        rows.append((bc, has_read, has_consumer, opted))
        if has_read and not has_consumer and not opted:
            bad.append(bc)
    return rows, bad


def selftest():
    import tempfile
    import shutil
    cases = []

    def make(tmp, bc, program, consumer=False, opt_out=False):
        api = os.path.join(tmp, "6-msa", bc, "src", bc.replace("BC-", "") + ".Api")
        os.makedirs(api, exist_ok=True)
        prog = program
        if opt_out:
            prog += "\n" + OPT_OUT + "\n"
        with open(os.path.join(api, "Program.cs"), "w", encoding="utf-8") as fh:
            fh.write(prog)
        if consumer:
            os.makedirs(os.path.join(api, "Events"), exist_ok=True)
            with open(os.path.join(api, "Events", "XEventConsumer.cs"), "w", encoding="utf-8") as fh:
                fh.write("class XEventConsumer {}\n")

    with tempfile.TemporaryDirectory() as tmp:
        make(tmp, "BC-WithConsumer", "builder.Services.AddDbContext<ReadDbContext>(o => { });", consumer=True)
        make(tmp, "BC-NoConsumer", "builder.Services.AddDbContext<ReadDbContext>(o => { });")
        make(tmp, "BC-OptOut", "builder.Services.AddDbContext<ReadDbContext>(o => { });", opt_out=True)
        make(tmp, "BC-NoReadStore", "builder.Services.AddControllers();")
        rows, bad = scan(tmp)
        names = [r[0] for r in rows]
        cases.append(("4 BC найдены", len(rows) == 4, str(names)))
        cases.append(("BC-NoConsumer в нарушениях", "BC-NoConsumer" in bad, str(bad)))
        cases.append(("BC-WithConsumer не нарушает", "BC-WithConsumer" not in bad, str(bad)))
        cases.append(("осознанный отказ не нарушает", "BC-OptOut" not in bad, str(bad)))
        cases.append(("без read-store не нарушает", "BC-NoReadStore" not in bad, str(bad)))
    failed = [c for c in cases if not c[1]]
    for name, ok, detail in cases:
        print("  %s %s" % ("OK  " if ok else "FAIL", name))
        if not ok:
            print("       деталь: " + detail)
    print("selftest: %d/%d" % (len(cases) - len(failed), len(cases)))
    return 0 if not failed else 1


def main(argv):
    if "--selftest" in argv:
        return selftest()
    root = ROOT_DEFAULT
    for i, a in enumerate(argv):
        if a == "--root" and i + 1 < len(argv):
            root = argv[i + 1]
    res = scan(root)
    if res is None:
        print("ОШИБКА: нет папки 6-msa в %s" % root)
        return 2
    rows, bad = res
    with_read = [r for r in rows if r[1]]
    with_consumer = [r for r in rows if r[2]]
    print("квитанция: BC всего %d | с read-store %d | с консьюмером %d | нарушений %d"
          % (len(rows), len(with_read), len(with_consumer), len(bad)))
    for bc, has_read, has_consumer, opted in rows:
        state = "read-store %s | консьюмер %s | отказ %s" % (
            "есть" if has_read else "нет",
            "есть" if has_consumer else "НЕТ",
            "да" if opted else "нет",
        )
        print("  %-22s %s" % (bc, state))
    for bc in bad:
        print("  ! %s — read-store зарегистрирован, консьюмера нет: класс BE-138 "
              "(события пишутся, read-модель не заполняется)" % bc)
    if bad:
        print("ИТОГ: rc=1 — есть BC с read-store без консьюмера проекции")
        return 1
    print("ИТОГ: rc=0 — у каждого BC с read-store есть консьюмер (или осознанный отказ)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
