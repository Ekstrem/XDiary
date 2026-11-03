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
        if is_test and my_bc:
            for r in refs:
                target_bc = bc_of(r.replace("\\", "/"))
                if target_bc and target_bc != my_bc:
                    violations.append(("б", rel, "ProjectReference на чужой %s" % target_bc))

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
          '<Project><ItemGroup><ProjectReference Include="../../BC-B/src/B.csproj" /></ItemGroup></Project>'}, 1),
        ({"6-msa/BC-A/src/A.Tests/A.Tests.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../BC-A/src/A.csproj" /></ItemGroup></Project>'}, 0),
        ({"6-msa/BC-A/src/A.csproj":
          '<Project><ItemGroup><ProjectReference Include="../../E2E/E2E.csproj" /></ItemGroup></Project>',
          "6-msa/E2E/E2E.csproj": "<Project />"}, 2),
        ({"6-msa/Contract.Client_Journal/x.csproj": "<Project />"}, 1),
        ({"6-msa/E2E/x.csproj": "<Project />"}, 1),
        ({"6-msa/ApiTests/x.csproj": "<Project />"}, 2),
        ({"6-msa/BC-Journal/src/Journal.Tests/x.csproj": "<Project />"}, 0),
        ({"6-msa/Stray.Tests/x.csproj": "<Project />"}, 1),
        ({"6-msa/ApiGateway.Tests/x.csproj": "<Project />"}, 0),
    ]
    shutil.rmtree(base, ignore_errors=True)
    for files, expect in cases:
        shutil.rmtree(os.path.join(base, "case"), ignore_errors=True)
        case = os.path.join(base, "case")
        for rel, content in files.items():
            full = os.path.join(case, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, "w", encoding="utf-8").write(content)
        got = len(scan(case))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] %s -> нарушений %d, ждали %d" % (sorted(files), got, expect))
    shutil.rmtree(base, ignore_errors=True)
    print("check_test_isolation --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    v = scan(root)
    for kind, rel, why in v:
        print("::error::(%s) %s: %s" % (kind, rel, why))
    print("check_test_isolation: нарушений %d (б: чужие ProjectReference, в: ссылка сервиса "
          "на E2E, д: запрещённые общие папки 6-msa/E2E, 6-msa/ApiTests, Contract.<A>_<B>)"
          % len(v))
    return 1 if v else 0


if __name__ == "__main__":
    sys.exit(main())
