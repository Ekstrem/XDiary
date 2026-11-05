#!/usr/bin/env python3
"""check_test_isolation.py — гейты изоляции жизненного цикла тестов (ADR-028).

Принцип ADR-028: тест, который нельзя собрать и прогнать вместе с компонентом, — не тест
компонента. Отсюда три машинно-проверяемых правила (пункты «б», «в», «д» комплаенса):

  б) тестовый проект внутри `6-msa/**` не ссылается `ProjectReference` на **чужой** `BC-*`;
  в) `6-msa/E2E` не подключён `ProjectReference` ни в один сервис (иначе сервис тихо
     тянет за собой межсервисный набор, и «изолированная сборка» — фикция);
  д) каталога вида `Contract.<A>_<B>` **нет** (такая папка не собирается без обоих
     сервисов и превращается в «общий тест ни за кем»).

Пункт «а» (`dotnet test 6-msa/BC-<X>` без других сервисов) — прогонный, не статический:
его нельзя проверить парсингом, нужен реальный запуск; см. `7-deploy/runbooks/tests-isolation.md`.

    python tools/check_test_isolation.py
    python tools/check_test_isolation.py --root <dir>
    python tools/check_test_isolation.py --selftest
"""
import argparse
import os
import re
import sys

SKIP = {".git", "node_modules", "bin", "obj", ".loopmaker"}
REF = re.compile(r'<ProjectReference\s+Include="([^"]+)"', re.I)
CONTRACT_DIR = re.compile(r'Contract\.[A-Za-z]+_[A-Za-z]+')
# ред. 3 ADR-028 (AGENTS.md §1, 2026-11-13): общих тестовых папок НЕТ.
FORBIDDEN_DIRS = {"E2E", "ApiTests"}  # только под 6-msa/


def bc_of(path):
    """`6-msa/BC-Journal/src/X` -> `BC-Journal` (иначе None)."""
    parts = path.replace("\\", "/").split("/")
    for p in parts:
        if p.startswith("BC-"):
            return p
    return None


def csproj_files(root):
    out = []
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return out
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP]
        for fn in filenames:
            if fn.endswith(".csproj"):
                out.append(os.path.join(dirpath, fn))
    return out


API_GATEWAY_TESTS = os.path.join("6-msa", "ApiGateway.Tests")


def scan(root):
    violations = []
    e2e_dir = os.path.join(root, "6-msa", "E2E")

    # д) запрещённые общие тестовые каталоги (ред. 3 ADR-028)
    msа = os.path.join(root, "6-msa")
    for dirpath, dirnames, _ in os.walk(msа):
        dirnames[:] = [d for d in dirnames if d not in SKIP]
        for d in list(dirnames):
            rel = os.path.relpath(os.path.join(dirpath, d), root).replace("\\", "/")
            if CONTRACT_DIR.fullmatch(d):
                violations.append(("д", rel, "каталог Contract.<A>_<B> запрещён ADR-028"))
            elif d in FORBIDDEN_DIRS and os.path.dirname(rel) == "6-msa":
                violations.append(("д", rel, "общая тестовая папка 6-msa/%s запрещена "
                                             "(ред. 3 ADR-028: тест живёт в папке сервиса)" % d))

    for proj in csproj_files(root):
        rel = os.path.relpath(proj, root).replace("\\", "/")
        try:
            text = open(proj, encoding="utf-8-sig").read()
        except OSError:
            continue
        refs = REF.findall(text)
        my_bc = bc_of(rel)
        is_test = ".Tests" in rel or "IntegrationTests" in rel or "/E2E/" in rel

        # б) тест не ссылается на чужой BC
        # ВАЖНО (@TE 2026-11-14 19:13): один csproj может ссылаться на чужой BC несколькими
        # ProjectReference — печатать строку на каждую ссылку значит выдавать «8 нарушений»
        # там, где проблемных проектов 3. Агрегируем по проекту: один проект = одна строка.
        if is_test and my_bc:
            foreign_bcs = []
            for r in refs:
                target_bc = bc_of(r.replace("\\", "/"))
                if target_bc and target_bc != my_bc and target_bc not in foreign_bcs:
                    foreign_bcs.append(target_bc)
            if foreign_bcs:
                violations.append(("б", rel, "ProjectReference на чужой BC: %s "
                                   "(разрешена только ссылка на 6-msa/Shared/**)"
                                   % ", ".join(foreign_bcs)))

        # е) тестовый проект лежит в папке своего сервиса (или 6-msa/ApiGateway.Tests)
        if ".Tests" in rel or "Tests/" in rel:
            in_bc = bool(re.search(r"^6-msa/BC-[^/]+/", rel))
            in_gw = rel.replace("\\", "/").startswith("6-msa/ApiGateway.Tests/")
            if not (in_bc or in_gw):
                violations.append(("е", rel, "тест вне папки сервиса (ADR-028 ред.3 §3.1)"))

        # в) E2E не подключён к сервису
        if not is_test:
            for r in refs:
                norm = os.path.normpath(os.path.join(os.path.dirname(proj), r))
                if os.path.abspath(norm).startswith(os.path.abspath(e2e_dir)):
                    violations.append(("в", rel, "сервис ссылается на 6-msa/E2E"))
    return violations


def selftest():
    import shutil
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-isolation")
    cases = [
        # (файлы{путь: содержимое}, ожидаемое число нарушений)
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../BC-B/src/B.csproj" /></ItemGroup></Project>',
          "6-msa/BC-A/BC-B/src/B.csproj": "<Project />"}, 1),
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../BC-A/src/A.csproj" /></ItemGroup></Project>',
          "6-msa/BC-A/BC-A/src/A.csproj": "<Project />"}, 0),
        ({"6-msa/BC-A/src/A.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../E2E/E2E.csproj" /></ItemGroup></Project>',
          "6-msa/E2E/E2E.csproj": "<Project />"}, 2),
        ({"6-msa/Contract.Client_Journal/x.csproj": "<Project />"}, 1),
        ({"6-msa/E2E/x.csproj": "<Project />"}, 1),
        ({"6-msa/ApiTests/x.csproj": "<Project />"}, 2),
        ({"6-msa/BC-Journal/src/Journal.Tests/x.csproj": "<Project />"}, 0),
        ({"6-msa/Stray.Tests/x.csproj": "<Project />"}, 1),
        ({"6-msa/ApiGateway.Tests/x.csproj": "<Project />"}, 0),
        # (11) вложенный тестовый проект = ошибка (ADR-028 §3.0.1)
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj": "<Project />",
          "6-msa/BC-A/src/A.Tests/Inner/Inner.Tests.csproj": "<Project />"}, 1),
        # (з) Compile Include тянет исходник чужого BC (@TE 2026-11-14 23:22)
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><Compile Include="../../../BC-B/src/B.cs" /></ItemGroup></Project>',
          "6-msa/BC-B/src/B.cs": "// code"}, 1),
        # (з)+(ж): чужой исходник и пути нет — два нарушения
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><Compile Include="../../../BC-B/src/Missing.cs" /></ItemGroup></Project>'},
         2),
        # (б) уточнение @TL 2026-11-14 28:11: ссылка на 6-msa/Shared/** РАЗРЕШЕНА
        # (общий пакет контрактов), запрещена только реализация чужого BC.
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup>'
          '<ProjectReference Include="../../../Shared/XDiary.Contracts/XDiary.Contracts.csproj" />'
          '</ItemGroup></Project>',
          "6-msa/Shared/XDiary.Contracts/XDiary.Contracts.csproj": "<Project />"}, 0),
        # (ж): include с лишним `6-msa/` (типичный след переезда) ведёт в никуда
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../../6-msa/BC-A/src/A.Domain/A.Domain.csproj" />'
          '</ItemGroup></Project>'}, 1),
    ]
    shutil.rmtree(base, ignore_errors=True)
    for files, expect in cases:
        shutil.rmtree(os.path.join(base, "case"), ignore_errors=True)
        case = os.path.join(base, "case")
        for rel, content in files.items():
            full = os.path.join(case, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, "w", encoding="utf-8").write(content)
        got = (len(scan(case)) + len(scan_broken_refs(case))
               + len(scan_compile_includes(case)) + len(scan_nested_projects(case)))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] %s -> нарушений %d, ждали %d" % (sorted(files), got, expect))
    shutil.rmtree(base, ignore_errors=True)
    # @TL 2026-11-05 13:34: раскладка групповой папки (6-msa/ApiGateway.Tests/)
    group_cases = [
        # дефект: csproj в корне группы (ровно наш случай) -> 1
        ({"6-msa/ApiGateway.Tests/ApiGateway.Tests.csproj": "<Project />"}, 1),
        # норма: проект в своей подпапке с совпадающим именем -> 0
        ({"6-msa/ApiGateway.Tests/Routes/Routes.csproj": "<Project />"}, 0),
        # неверно: имя проекта не совпадает с подпапкой -> 1
        ({"6-msa/ApiGateway.Tests/Routes/Other.csproj": "<Project />"}, 1),
    ]
    for files, expect in group_cases:
        shutil.rmtree(os.path.join(base, "grp"), ignore_errors=True)
        grp = os.path.join(base, "grp")
        for rel, content in files.items():
            full = os.path.join(grp, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, "w", encoding="utf-8").write(content)
        got = len(scan_group_layout(grp))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] групповая раскладка %s -> нарушений %d, ждали %d"
                  % (sorted(files), got, expect))
    total_grp = len(group_cases)

    # (13) @TL 2026-11-05 14:48 / DO-092: Compile Include обязан точно совпадать с .cs в папке
    set_cases = [
        ({"P/P.csproj": '<Project><PropertyGroup><EnableDefaultCompileItems>false'
                        '</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
                        '<Compile Include="A.cs" /></ItemGroup></Project>',
          "P/A.cs": "// code"}, 0),
        ({"P/P.csproj": '<Project><PropertyGroup><EnableDefaultCompileItems>false'
                        '</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
                        '<Compile Include="A.cs" /></ItemGroup></Project>',
          "P/A.cs": "// code", "P/B.cs": "// code"}, 1),
        ({"P/P.csproj": '<Project><PropertyGroup><EnableDefaultCompileItems>false'
                        '</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
                        '<Compile Include="Missing.cs" /></ItemGroup></Project>'}, 1),
        ({"P/P.csproj": '<Project><ItemGroup><Compile Include="A.cs" /></ItemGroup></Project>',
          "P/A.cs": "// code", "P/B.cs": "// code"}, 0),
    ]
    for files, expect in set_cases:
        set_dir = os.path.join(base, "set")
        shutil.rmtree(set_dir, ignore_errors=True)
        for rel, content in files.items():
            full = os.path.join(set_dir, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, "w", encoding="utf-8").write(content)
        got = len(scan_compile_set(set_dir))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] compile-set %s -> нарушений %d, ждали %d"
                  % (sorted(files), got, expect))
    total_set = len(set_cases)

    # @TL 2026-11-05 13:30: «нечего проверять» ≠ «проверил, чисто».
    # Кейс «нет csproj» обязан давать rc=2, иначе неверный --root снова станет зелёным.
    empty = os.path.join(base, "empty")
    os.makedirs(empty, exist_ok=True)
    total = len(cases) + 1 + total_grp + total_set
    if not csproj_files(empty):
        ok += 1
    else:
        print("  [FAIL] пустой root должен давать 0 проектов (нечего проверять)")

    print("check_test_isolation --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def scan_compile_includes(root):
    """(з) `<Compile Include>` не тянет исходник чужого BC, и цель существует (ж).

    Почему: `Compile Include="..\\BC-Client\\src\\Client.ApiTests\\...E2EInfra.cs"` — это
    чужой код в тесте **мимо** `ProjectReference`, гейт (б) его не видит. Плюс после
    переименования папки такой путь устаревает и проект **не собирается** (CS2001) —
    то есть ошибка проявляется как «тест есть, но CI падает на сборке».
    Идея — @TE 2026-11-14 23:22.
    """
    out = []
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return out
    pattern = re.compile(r'<Compile\s+Include="([^"]+)"', re.I)
    for dirpath, _dirs, names in os.walk(base):
        for name in names:
            if not name.endswith(".csproj"):
                continue
            path = os.path.join(dirpath, name)
            try:
                txt = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            rel_proj = os.path.relpath(path, root).replace(os.sep, "/")
            my_bc = _bc_of_path(rel_proj)
            for inc in pattern.findall(txt):
                if "*" in inc or "?" in inc:
                    continue
                target = os.path.normpath(os.path.join(dirpath, inc.replace("\\", os.sep)))
                rel_target = os.path.relpath(target, root).replace(os.sep, "/")
                foreign = _bc_of_path(rel_target)
                if foreign and my_bc and foreign != my_bc:
                    out.append(("з", rel_proj, "Compile Include в чужой BC: %s" % rel_target))
                if not os.path.isfile(target):
                    out.append(("ж", rel_proj, "Compile Include на несуществующий путь: %s" % inc))
    return out


def _bc_of_path(rel):
    m = re.search(r'(?:^|/)6-msa/(BC-[A-Za-z0-9]+)/', rel)
    return m.group(1) if m else None


def scan_nested_projects(root):
    """(11) тестовый проект не содержит внутри другой `*.csproj` (ADR-028 §3.0.1).

    Почему: родительский `*Tests.csproj` по умолчанию глобит `**/*.cs` и затягивает
    `obj/` **вложенного** проекта, где `[assembly: TargetFramework(...)]` → `CS0579`
    (дубликат атрибута). Дефект зависит от порядка сборки: на чистом клоне соберётся,
    после сборки вложенного — упадёт. Поэтому проверяем структуру, а не результат.
    """
    out = []
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return out
    for dirpath, _dirs, names in os.walk(base):
        for name in names:
            if not re.search(r"Tests?\.csproj$", name):
                continue
            rel_parent = os.path.relpath(dirpath, root).replace(os.sep, "/")
            # любой другой csproj глубже в этой папке = вложенный проект
            for sub_dir, _sd, sub_names in os.walk(dirpath):
                if sub_dir == dirpath:
                    continue
                for sub in sub_names:
                    if not sub.endswith(".csproj"):
                        continue
                    rel_sub = os.path.relpath(os.path.join(sub_dir, sub), root).replace(os.sep, "/")
                    out.append((rel_parent + "/" + name, rel_sub))
    return out


GROUP_TEST_DIRS = ("ApiGateway.Tests",)


def scan_group_layout(root):
    """Групповая папка тестов (`6-msa/ApiGateway.Tests/`) — раскладка по @TL 2026-11-05 13:34.

    Правило: в корне группы `csproj` быть НЕ ДОЛЖНО, каждый проект живёт в своей подпапке
    и называется по ней: `ApiGateway.Tests/<Имя>/<Имя>.csproj`. Причина: несколько проектов
    в корне одной папки смешивают файлы и ломают `dotnet test` по папке — тест «пары»
    оказывается рядом с тестом сервиса, и `bc_of()` перестаёт различать, кто чей.
    """
    out = []
    for group in GROUP_TEST_DIRS:
        base = os.path.join(root, "6-msa", group)
        if not os.path.isdir(base):
            continue
        for fn in sorted(os.listdir(base)):
            full = os.path.join(base, fn)
            if not os.path.isfile(full) or not fn.endswith(".csproj"):
                continue
            out.append(("12", "6-msa/%s/%s" % (group, fn),
                        "csproj в корне групповой папки — перенеси в подпапку "
                        "`%s/<Имя>/<Имя>.csproj`" % group))
        for sub in sorted(os.listdir(base)):
            subfull = os.path.join(base, sub)
            if not os.path.isdir(subfull):
                continue
            for fn in sorted(os.listdir(subfull)):
                if not fn.endswith(".csproj"):
                    continue
                if os.path.splitext(fn)[0] != sub:
                    out.append(("12", "6-msa/%s/%s/%s" % (group, sub, fn),
                                "имя проекта не совпадает с подпапкой — ожидали `%s.csproj`" % sub))
    return out


COMPILE_INC = re.compile(r'<Compile\s+Include="([^"]+)"', re.I)
FLAG_OFF = re.compile(r'<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>', re.I)


def scan_compile_set(root):
    """(13) @TL 2026-11-05 14:48 / DO-092: при выключенном автоглобе Compile Include точны.

    Если в csproj стоит `EnableDefaultCompileItems=false`, список `<Compile Include="...">`
    обязан **точно** совпадать с множеством `.cs` в папке проекта (без `bin`/`obj`).
    Расхождение в любую сторону — ошибка: недостающий файл молча не попадёт в сборку,
    лишний — сборка упадёт непонятно где.
    """
    out = []
    skip = {'bin', 'obj'}
    projects = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP]
        for fn in filenames:
            if fn.endswith('.csproj'):
                projects.append(os.path.join(dirpath, fn))
    for proj in sorted(projects):
        try:
            text = open(proj, encoding='utf-8', errors='replace').read()
        except OSError:
            continue
        if not (FLAG_OFF.search(text) and 'Compile Include' in text):
            continue
        rel_proj = os.path.relpath(proj, root).replace(os.sep, '/')
        base_dir = os.path.dirname(proj)
        declared = set()
        for m in COMPILE_INC.finditer(text):
            raw = m.group(1).replace('\\', '/').lstrip('./')
            declared.add(os.path.normpath(os.path.join(base_dir, raw)))
        actual = set()
        for dirpath, dirnames, filenames in os.walk(base_dir):
            dirnames[:] = [d for d in dirnames if d not in skip]
            for fn in filenames:
                if fn.endswith('.cs'):
                    actual.add(os.path.normpath(os.path.join(dirpath, fn)))
        for missing in sorted(actual - declared):
            out.append(("13", rel_proj, "есть .cs, но нет в Compile Include: %s"
                        % os.path.relpath(missing, base_dir).replace(os.sep, '/')))
        for extra in sorted(declared - actual):
            if not os.path.exists(extra):
                out.append(("13", rel_proj, "Compile Include на несуществующий файл: %s"
                            % os.path.relpath(extra, base_dir).replace(os.sep, '/')))
    return out


def scan_broken_refs(root):
    """(ж) ProjectReference в тестовом проекте указывает на существующий файл.

    Почему: перенос файлов без правки относительных путей оставляет
    `..\\..\\..\\6-msa\\BC-X\\src\\...` от старого места. Проект не собирается, а гейт
    «(б) чужой BC» при этом молчит: ссылка-то ведёт «в свой» BC, просто в никуда.
    Ловит и переезд, и опечатку, и удалённый проект.
    """
    out = []
    base = os.path.join(root, "6-msa")
    if not os.path.isdir(base):
        return out
    pattern = re.compile(r'<ProjectReference\s+Include="([^"]+)"', re.I)
    for dirpath, _dirs, names in os.walk(base):
        for name in names:
            if not re.search(r"Tests?\.csproj$", name):
                continue
            path = os.path.join(dirpath, name)
            try:
                txt = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            rel_proj = os.path.relpath(path, root).replace(os.sep, "/")
            for inc in pattern.findall(txt):
                target = os.path.normpath(os.path.join(dirpath, inc.replace("\\", os.sep)))
                if not os.path.isfile(target):
                    out.append((rel_proj, inc))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    v = scan(root)
    for rel_proj, inc in scan_broken_refs(root):
        v.append(("ж", rel_proj, "ProjectReference на несуществующий путь: %s" % inc))
    for kind, rel_proj, why in scan_compile_includes(root):
        v.append((kind, rel_proj, why))
    for parent, nested in scan_nested_projects(root):
        v.append(("11", parent, "вложенный csproj внутри тестового проекта: %s" % nested))
    for kind, rel, why in scan_group_layout(root):
        v.append((kind, rel, why))
    for kind, rel, why in scan_compile_set(root):
        v.append((kind, rel, why))
    for kind, rel, why in v:
        print("::error::(%s) %s: %s" % (kind, rel, why))
    # Форма отчёта — требование @TL 2026-11-14 34:11: ДВА числа и дедуп по пути.
    # «8 нарушений» читается как «8 проблемных проектов», хотя их 3, и следующий читатель
    # делает неверный вывод об объёме работы. Канон: `нарушений 8 (3 проекта): (б) …`.
    projects = sorted({rel for _, rel, _ in v})
    # @TL 2026-11-05 13:30, требование (1): «нечего проверять» ≠ «проверил, чисто».
    # Раньше при неверном `--root` (например `--root 6-msa`, где поиск идёт в `6-msa/6-msa`)
    # находилось 0 csproj, печаталось «в норме» и возвращался rc=0 — зелёный на пустоте.
    checked = csproj_files(root)
    if not checked:
        print("check_test_isolation: НЕ ИЗМЕРЕНО: найдено 0 проектов (root=%s) — "
              "это не «чисто», а «нечего проверять»" % root)
        print("::error::check_test_isolation: НЕ ИЗМЕРЕНО — найдено 0 csproj, "
              "проверка не состоялась (проверь --root)")
        return 2

    if v:
        import collections
        kinds = collections.Counter(k for k, _, _ in v)
        titles = {"б": "ProjectReference на чужой BC", "в": "ссылка сервиса на E2E",
                  "д": "запрещённая общая папка", "е": "тест вне папки сервиса",
                  "ж": "include в никуда", "з": "Compile Include чужого BC",
                  "11": "вложенный проект внутри тестового",
                  "12": "раскладка групповой папки тестов",
                  "13": "Compile Include не совпадает с .cs в папке"}
        parts = []
        for kind, count in sorted(kinds.items()):
            in_proj = len({rel for k, rel, _ in v if k == kind})
            parts.append("(%s) %s — %d в %d проект(ах)" % (kind, titles.get(kind, kind),
                                                           count, in_proj))
        # @TL 2026-11-05 13:30, требование (2): ДВА числа — сколько проверено и сколько нарушений.
        # «0 проектов» читалось как «нет проектов», хотя означало «нет проектов С нарушениями».
        print("check_test_isolation: проверено проектов: %d | нарушений: %d (%d %s): %s"
              % (len(checked), len(v), len(projects),
                 "проект" if len(projects) == 1 else "проекта" if len(projects) < 5 else "проектов",
                 "; ".join(parts)))
    else:
        print("check_test_isolation: проверено проектов: %d | нарушений: 0: "
              "изоляция и раскладка по ADR-028 в норме" % len(checked))
    return 1 if v else 0


if __name__ == "__main__":
    sys.exit(main())
