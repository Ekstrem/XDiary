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
                violations.append(("б", rel, "ProjectReference на чужой BC: %s"
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
        got = len(scan(case)) + len(scan_broken_refs(case))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] %s -> нарушений %d, ждали %d" % (sorted(files), got, expect))
    shutil.rmtree(base, ignore_errors=True)
    print("check_test_isolation --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


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
    for kind, rel, why in v:
        print("::error::(%s) %s: %s" % (kind, rel, why))
    projects = sorted({rel for _, rel, _ in v})
    print("check_test_isolation: нарушений %d в %d проект(ах) — б: чужие ProjectReference, "
          "в: ссылка сервиса на E2E, д: запрещённые общие папки "
          "(6-msa/E2E, 6-msa/ApiTests, Contract.<A>_<B>), ж: ProjectReference в никуда"
          % (len(v), len(projects)))
    return 1 if v else 0


if __name__ == "__main__":
    sys.exit(main())
