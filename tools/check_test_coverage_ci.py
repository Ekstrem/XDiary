#!/usr/bin/env python
"""check_test_coverage_ci.py — каждый *Tests*.csproj покрыт хотя бы одним workflow (DO-156).

Класс (замер @DO 2026-11-21 по запросу владельца «все ли тесты в CI»): «написали тест,
но не включили в CI» ловится только проверкой по запросу. Живой пример: per-BC цикл
`src/*Tests*/*.csproj` (один уровень) не доставал вложенные `src/<X>.ApiTests/<Nested>/…csproj`
(5 проектов) — их вытягивал только `ci-backend find`, а per-BC workflow оставался бы зелёным
на сломанном вложенном ApiTests. Тот же класс, что «гейт ищет то, чего нет», зеркально:
«тест есть, а гонятеля нет».

Логика:
  1. Проекты: `git ls-files` по `6-msa/**/*.csproj`, у которых имя файла содержит `Tests`
     (как `find 6-msa -name '*Tests*.csproj'` в ci-backend).
  2. Покрытие: парсинг `.github/workflows/*.yml`:
     - `find <dir> -name '*Tests*.csproj'`           → glob `<dir>/**/*Tests*.csproj`
     - `for p in <glob ...>; do` + `working-directory: <wd>` → `<wd>/<glob>` (bash-glob,
       `*` не пересекает `/`; несовпавший glob CI пропускает по `[ -e "$p" ]`)
     - `dotnet test <путь>`                          → точный путь
  3. Проект НЕ совпадает ни с одним паттерном → «не покрыт» → rc=1 (--strict) или
     ::warning (по умолчанию, переходный период DO-156).

Коды: 0 — все покрыты (или advisory); 1 — есть непокрытые (--strict); 2 — не измерено.

    python tools/check_test_coverage_ci.py [--strict]
    python tools/check_test_coverage_ci.py --selftest
"""
import argparse
import pathlib
import re
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

ROOT = pathlib.Path(__file__).resolve().parents[1]
WF_DIR = ROOT / ".github" / "workflows"

_FIND_RE = re.compile(r"find\s+(\S+)\s+-name\s+'?\*Tests\*\.csproj'?")
_FOR_RE = re.compile(r"for\s+\w+\s+in\s+([^;]+);")
_DOTNET_TEST_RE = re.compile(r"dotnet\s+test\s+([^\s|&;]+\.csproj)")
_WD_RE = re.compile(r"working-directory:\s*(\S+)")


def glob_to_re(pat):
    """glob с `**` и `*` → regex (tracked-пути, разделитель `/`)."""
    out, i = [], 0
    while i < len(pat):
        c = pat[i]
        if c == "*":
            if i + 1 < len(pat) and pat[i + 1] == "*":
                out.append(".*")
                i += 2
            else:
                out.append("[^/]*")
                i += 1
        elif c == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(c))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def projects():
    """Все *Tests*.csproj в 6-msa (tracked)."""
    r = subprocess.run(["git", "-C", str(ROOT), "ls-files", "6-msa"],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        return None
    return sorted(p for p in r.stdout.splitlines()
                  if p.endswith(".csproj") and "Tests" in pathlib.PurePosixPath(p).name)


def coverage_patterns():
    """(workflow, паттерн) из всех workflows: find / for-glob(+wd) / dotnet test."""
    pats = []
    for wf in sorted(WF_DIR.glob("*.yml")) + sorted(WF_DIR.glob("*.yaml")):
        try:
            text = wf.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        wds = _WD_RE.findall(text) or [""]
        for m in _FIND_RE.finditer(text):
            base = m.group(1).rstrip("/")
            pats.append((wf.name, f"{base}/**/*Tests*.csproj"))
        for m in _FOR_RE.finditer(text):
            for tok in m.group(1).split():
                tok = tok.strip("'\"")
                if ".csproj" not in tok:
                    continue
                for wd in wds:
                    if tok.startswith(("6-msa", "./", "/")) or wd == "":
                        pats.append((wf.name, tok.lstrip("./")))
                    else:
                        pats.append((wf.name, f"{wd.rstrip('/')}/{tok}"))
        for m in _DOTNET_TEST_RE.finditer(text):
            tok = m.group(1).strip("'\"")
            if not tok.startswith("$"):
                pats.append((wf.name, tok.lstrip("./")))
    return pats


def covered_by(project, pats):
    """Список (workflow, паттерн), покрывающих проект."""
    hits = []
    for wf, pat in pats:
        rx = glob_to_re(pat)
        if rx.match(project):
            hits.append((wf, pat))
    return hits


def receipt(total, uncovered, advisory):
    mode = "advisory (::warning)" if advisory else "strict"
    return (f"квитанция: тест-проектов {total} | не покрыто ни одним workflow: {len(uncovered)} | режим {mode}")


def selftest():
    ok = total = 0

    def case(name, cond):
        nonlocal ok, total
        total += 1
        if cond:
            ok += 1
        else:
            print(f"  [FAIL] {name}")

    rx = glob_to_re("6-msa/BC-X/src/*Tests*/*.csproj")
    case("glob wd: один уровень совпадает",
         bool(rx.match("6-msa/BC-X/src/Foo.Domain.Tests/Foo.Domain.Tests.csproj")))
    case("glob wd: вложенный НЕ совпадает (одноуровневый дефект)",
         not rx.match("6-msa/BC-X/src/Foo.ApiTests/Nested/Nested.IntegrationTests.csproj"))
    rx2 = glob_to_re("6-msa/BC-X/src/*Tests*/*/*.csproj")
    case("glob wd двухуровневый: вложенный совпадает",
         bool(rx2.match("6-msa/BC-X/src/Foo.ApiTests/Nested/Nested.IntegrationTests.csproj")))
    case("find-паттерн: всё рекурсивно",
         bool(glob_to_re("6-msa/**/*Tests*.csproj").match("6-msa/BC-X/a/b/c.Tests.csproj")))
    case("точный путь dotnet test",
         bool(glob_to_re("6-msa/BC-Journal/src/Journal.ApiTests/Journal.ApiTests.csproj")
              .match("6-msa/BC-Journal/src/Journal.ApiTests/Journal.ApiTests.csproj")))

    projs = ["6-msa/BC-X/src/Foo.Domain.Tests/Foo.Domain.Tests.csproj",
             "6-msa/BC-X/src/Foo.ApiTests/Nested/Nested.IntegrationTests.csproj",
             "6-msa/BC-Y/src/Orphan.ApiTests/Orphan.ApiTests.csproj"]
    pats = [("ci-x.yml", "6-msa/BC-X/src/*Tests*/*.csproj"),
            ("ci-x.yml", "6-msa/BC-X/src/*Tests*/*/*.csproj")]
    unc = [p for p in projs if not covered_by(p, pats)]
    case("покрытие: два покрыты, сирота — нет",
         unc == ["6-msa/BC-Y/src/Orphan.ApiTests/Orphan.ApiTests.csproj"])
    case("гейт ловит «добавил ApiTests, не добавил в workflow»",
         "ci-x.yml" in {wf for wf, _ in covered_by(projs[1], pats)}
         and not covered_by(projs[2], pats))

    line = receipt(31, ["x"], True)
    case("квитанция: числа и режим", "31" in line and "не покрыто" in line and "advisory" in line)

    print(f"check_test_coverage_ci --selftest: {ok}/{total}")
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict", action="store_true",
                    help="непокрытые → rc=1 (по умолчанию — ::warning, переходный период DO-156)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    projs = projects()
    if projs is None:
        print("::error::check_test_coverage_ci: git ls-files не сработал (rc=2)")
        return 2
    pats = coverage_patterns()
    uncovered = []
    for p in projs:
        hits = covered_by(p, pats)
        if not hits:
            uncovered.append(p)
            print(f"::warning::check_test_coverage_ci: НЕ ПОКРЫТ ни одним workflow: {p} "
                  f"(добавил тест — добавь и в workflow, DO-156)")
    print(receipt(len(projs), uncovered, not a.strict))
    if uncovered and a.strict:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
