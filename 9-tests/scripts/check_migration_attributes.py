#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Гейт: каждая EF-миграция обязана быть ВИДИМА EF — атрибуты `[Migration(...)]` и `[DbContext(...)]`.

Причина (BE-127, @TL 2026-11-18 09:02): миграции `20261117210000_AddVersionToJournalRead` и
`20261117210000_AddVersionToPartnersRead` лежат в файлах, но у них **нет** `.Designer.cs` с
атрибутами, и в самом `.cs` атрибутов тоже нет. EF ищет миграции по атрибутам ⇒ `Database.Migrate()`
их **не видит** ⇒ колонки `Version` в `partners_read`/`journal_read` нет ⇒ read-модель делает
SELECT по несуществующей колонке ⇒ `42703` ⇒ 500 на `GET /partners` и `GET /journal/records`.

Почему это не ловит `DatabaseCreationTests`: он проверяет только
`Assert.Empty(GetPendingMigrations())` и `Assert.NotEmpty(GetAppliedMigrations())`, а невидимая
миграция не попадает НИ в pending, НИ в applied — оба утверждения проходят.

Правила: для каждого `*.cs` в каталогах `Migrations/` (кроме `*.Designer.cs` и `*ModelSnapshot.cs`)
атрибуты `[Migration(` и `[DbContext(` должны быть либо в нём, либо в парном `*.Designer.cs`.

Коды: 0 — все миграции видимы; 1 — есть невидимые; 2 — ошибка запуска (не найдена папка).
`--selftest` проверяет сам гейт на трёх фикстурах (inline-атрибуты / Designer / без атрибутов).
"""
import os
import re
import subprocess
import sys
import tempfile

ROOT_DEFAULT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RE_MIGRATION = re.compile(r"\[Migration\(")
RE_DBCONTEXT = re.compile(r"\[DbContext\(")


def migration_files(root):
    out = []
    msa = os.path.join(root, "6-msa")
    for dirpath, _dirnames, filenames in os.walk(msa):
        if os.sep + "Migrations" + os.sep not in dirpath + os.sep:
            continue
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            if name.endswith(".Designer.cs") or name.endswith("ModelSnapshot.cs"):
                continue
            out.append(os.path.join(dirpath, name))
    return sorted(out)


def has_attributes(path):
    """Атрибуты EF есть либо в самом файле, либо в парном Designer."""
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
    except OSError:
        return False, False
    mig = bool(RE_MIGRATION.search(text))
    ctx = bool(RE_DBCONTEXT.search(text))
    if mig and ctx:
        return True, True
    designer = path[:-3] + ".Designer.cs"
    if os.path.exists(designer):
        with open(designer, encoding="utf-8", errors="replace") as fh:
            dtext = fh.read()
        mig = mig or bool(RE_MIGRATION.search(dtext))
        ctx = ctx or bool(RE_DBCONTEXT.search(dtext))
    return mig, ctx


def check(root):
    files = migration_files(root)
    bad = []
    for path in files:
        mig, ctx = has_attributes(path)
        if not (mig and ctx):
            bad.append((os.path.relpath(path, root), mig, ctx))
    return files, bad


def selftest():
    cases = []
    with tempfile.TemporaryDirectory() as tmp:
        base = os.path.join(tmp, "6-msa", "BC-Test", "src", "Test.Storage", "Migrations")
        os.makedirs(base, exist_ok=True)

        # 1) атрибуты inline — видима
        with open(os.path.join(base, "20260101000000_Inline.cs"), "w", encoding="utf-8") as fh:
            fh.write('[DbContext(typeof(CommandDbContext))]\n[Migration("20260101000000_Inline")]\npublic partial class Inline : Migration { }\n')
        # 2) атрибуты только в Designer — видима
        with open(os.path.join(base, "20260102000000_Pair.cs"), "w", encoding="utf-8") as fh:
            fh.write('public partial class Pair : Migration { }\n')
        with open(os.path.join(base, "20260102000000_Pair.Designer.cs"), "w", encoding="utf-8") as fh:
            fh.write('[DbContext(typeof(CommandDbContext))]\n[Migration("20260102000000_Pair")]\npartial class Pair { }\n')
        # 3) без атрибутов вообще — НЕ видима (тот самый дефект)
        with open(os.path.join(base, "20260103000000_Orphan.cs"), "w", encoding="utf-8") as fh:
            fh.write('public partial class Orphan : Migration { }\n')
        # 4) снапшот и Designer не должны считаться миграциями
        with open(os.path.join(base, "CommandDbContextModelSnapshot.cs"), "w", encoding="utf-8") as fh:
            fh.write('partial class CommandDbContextModelSnapshot { }\n')

        files, bad = check(tmp)
        names = [os.path.basename(f) for f in files]
        cases.append(("миграций найдено 3 (снапшот и Designer не считаются)", len(files) == 3, str(names)))
        cases.append(("Orphan в списке миграций", any(n.endswith("Orphan.cs") for n in names), str(names)))
        cases.append(("Inline видима", not any("Inline" in b[0] for b in bad), str(bad)))
        cases.append(("Pair видима через Designer", not any("Pair" in b[0] for b in bad), str(bad)))
        cases.append(("Orphan невидима (дефект пойман)", any("Orphan" in b[0] for b in bad), str(bad)))

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
    if not os.path.isdir(os.path.join(root, "6-msa")):
        print("ОШИБКА: нет папки 6-msa в %s" % root)
        return 2
    files, bad = check(root)
    print("миграций проверено: %d | невидимых для EF: %d" % (len(files), len(bad)))
    for rel, mig, ctx in bad:
        print("  ! %s — [Migration]=%s [DbContext]=%s ⇒ EF её не применит (нужен .Designer.cs с атрибутами)"
              % (rel, "да" if mig else "НЕТ", "да" if ctx else "НЕТ"))
    if bad:
        print("ИТОГ: rc=1 — есть миграции, которые EF не видит (класс BE-127)")
        return 1
    print("ИТОГ: rc=0 — все миграции видимы EF")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
