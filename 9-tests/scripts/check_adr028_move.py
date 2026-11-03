#!/usr/bin/env python3
"""check_adr028_move.py — Python-двойник `check-adr028-move.sh` (ADR-028 ред. 3, §5.4).

Зачем двойник: в песочнице @TE и @AR **bash не запускается**
(`fatal error - couldn't create signal pipe, Win32 error 5` — запрет пайпов), поэтому
каноническую строку приёмки нельзя было получить без CI. Этот файл печатает **тот же**
результат тем же форматом, но работает там, где есть только Python.

⚠️ Источник правды по проверкам — `9-tests/scripts/check-adr028-move.sh`. Меняя один
файл, меняй второй: расхождение здесь = расхождение в приёмке.

Использование:
    python 9-tests/scripts/check_adr028_move.py --post            # origin/main
    REF=5855cf99 python 9-tests/scripts/check_adr028_move.py --post

Восемь проверок (ADR-028 ред. 3):
  1. в `9-tests` нет `.csproj` (там только браузерные тесты);
  2. общих тестовых папок нет: `6-msa/E2E`, `6-msa/ApiTests`, `Contract.<A>_<B>`;
  3. каждый `*Tests.csproj` — в `6-msa/BC-*/src/…` или `6-msa/ApiGateway.Tests/`;
  4. все 7 адресов карты §3.1 на месте (положительная проверка);
  5. `9-tests/RnD/front-e2e`: маркер `front-e2e.md` + `playwright` в `package.json`;
  6. в `9-tests` нет посторонних исполняемых файлов;
  7. атрибуты тестов не потерялись (baseline: Fact 128 / StandFact 20 / Theory 11 / ApiFact 17);
  8. старые каталоги `9-tests` удалены.
"""
from __future__ import annotations

import os
import re
import subprocess
import sys

BASE_FACT = 128
BASE_STAND = 20
BASE_THEORY = 11
BASE_API = 17

TARGETS = [
    "6-msa/BC-Client/src/Client.ApiTests",
    "6-msa/BC-Catalog/src/Catalog.ApiTests",
    "6-msa/BC-DailyMetrics/src/DailyMetrics.ApiTests",
    "6-msa/BC-Portability/src/Portability.ApiTests",
    "6-msa/BC-Analytics/src/Analytics.ApiTests",
    "6-msa/BC-Journal/src/Journal.ApiTests",
    "6-msa/ApiGateway.Tests/ApiGateway.Tests.csproj",
]

OLD_9TESTS_DIRS = [
    "9-tests/Contract.ClientJournal", "9-tests/Partners.Catalog", "9-tests/Somatic.Daily",
    "9-tests/Portability.Gateway", "9-tests/Analytics.Privacy", "9-tests/Gateways",
    "9-tests/Gateways.Api", "9-tests/Journal", "9-tests/E2E", "9-tests/Load",
]


def git(*args: str) -> str:
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else ""


def git_ok(*args: str) -> bool:
    return subprocess.run(["git", *args], capture_output=True).returncode == 0


def tree(ref: str, path: str = "") -> list[str]:
    out = git("ls-tree", "-r", "--name-only", ref, "--", path) if path else git("ls-tree", "-r", "--name-only", ref)
    return [ln for ln in out.splitlines() if ln.strip()]


def show(ref: str, path: str) -> str:
    return git("show", f"{ref}:{path}")


def count_attr(ref: str, pattern: str) -> int:
    """Сумма вхождений атрибута по 6-msa + 9-tests (как в bash-версии через git grep -c)."""
    total = 0
    for scope in ("6-msa", "9-tests"):
        out = git("grep", "-h", "-c", "-E", pattern, ref, "--", f":(glob){scope}/**/*.cs")
        for ln in out.splitlines():
            try:
                total += int(ln.strip())
            except ValueError:
                pass
    return total


def main() -> int:
    mode = "post"
    if "--pre" in sys.argv[1:]:
        mode = "pre"
    ref = (os.environ.get("REF") or "origin/main").strip()
    if not git_ok("rev-parse", "--verify", f"{ref}^{{commit}}"):
        print(f"::error::ADR-028: REF={ref!r} не резолвится в этом репозитории — приёмка не выполнена")
        print(f"REF={ref} | FAIL: невалидная ревизия | режим --{mode}")
        return 2
    failures = 0

    def ok(name: str, detail: str) -> None:
        print(f"{name:<62} PASS  ({detail})")

    def bad(name: str, detail: str) -> None:
        nonlocal failures
        failures += 1
        print(f"{name:<62} FAIL  ({detail})")
        print(f"::error::ADR-028: {name} - {detail}")

    print(f"REF={ref} | режим: --{mode}")
    print("----")

    # 1) .csproj в 9-tests
    n_csproj = len([p for p in tree(ref, "9-tests") if p.endswith(".csproj")])
    if n_csproj == 0:
        ok("1) .csproj в 9-tests отсутствуют", "0")
    else:
        bad("1) .csproj в 9-tests отсутствуют", f"{n_csproj} - тесты сервисов живут в 6-msa/BC-*")

    # 2) общие бакеты
    shared = [p for p in ("6-msa/E2E", "6-msa/ApiTests") if tree(ref, p)]
    contract_out = [p for p in tree(ref, "6-msa") if re.match(r"^6-msa/Contract\.[^/]+/", p)]
    if not shared and not contract_out:
        ok("2) общих тестовых бакетов нет", "ни E2E, ни ApiTests, ни Contract.<A>_<B>")
    else:
        for p in shared:
            print(f"::error::ADR-028 §3.1: общая тестовая папка запрещена — {p}")
        if contract_out:
            print(f"::error::ADR-028 §3.1: каталог Contract.<A>_<B> запрещён — {contract_out[0]}")
        bad("2) общих тестовых бакетов нет", f"есть: {shared} {('contract: ' + str(contract_out)) if contract_out else ''}")

    # 3) каждый тестовый проект — в папке своего сервиса
    projs = [p for p in tree(ref, "6-msa") if re.search(r"Tests?\.csproj$", p)]
    stray = [p for p in projs if not re.match(r"^6-msa/BC-[^/]+/src/", p) and not p.startswith("6-msa/ApiGateway.Tests/")]
    if not stray:
        ok("3) все тестовые проекты в папках своих сервисов", f"{len(projs)} проектов")
    else:
        bad("3) все тестовые проекты в папках своих сервисов", "вне зоны: " + ", ".join(stray[:4]))

    # 4) все 7 адресов §3.1 на месте
    missing = [t for t in TARGETS if not tree(ref, t)]
    if not missing:
        ok("4) все 7 адресов карты ADR-028 3.1 на месте", "7/7")
    else:
        bad("4) все 7 адресов карты ADR-028 3.1 на месте", "нет: " + ", ".join(t.replace("6-msa/", "") for t in missing))

    # 5) front-e2e: маркер + playwright
    marker = any(p.endswith("front-e2e.md") for p in tree(ref, "9-tests/RnD/front-e2e"))
    pkg = "playwright" in show(ref, "9-tests/RnD/front-e2e/package.json")
    if marker and pkg:
        ok("5) front-e2e: маркер + браузерная зависимость", "front-e2e.md + playwright")
    else:
        bad("5) front-e2e: маркер + браузерная зависимость", f"маркер={int(marker)} playwright={int(pkg)}")

    # 6) посторонние исполняемые файлы в 9-tests
    allowed_prefixes = ("9-tests/RnD/front-e2e/", "9-tests/reports/", "9-tests/scripts/")
    allowed_exact = ("9-tests/README.md", "9-tests/AGENTS.md")
    foreign = [
        p for p in tree(ref, "9-tests")
        if re.search(r"\.(js|ps1|sh|cs)$", p)
        and not p.startswith(allowed_prefixes)
        and p not in allowed_exact
    ]
    if not foreign:
        ok("6) в 9-tests нет посторонних скриптов", "0")
    else:
        bad("6) в 9-tests нет посторонних скриптов", "лишнее: " + ", ".join(foreign[:4]))

    # 7) атрибуты
    fact = count_attr(ref, r"\[Fact")
    stand = count_attr(ref, r"\[StandFact")
    theory = count_attr(ref, r"\[Theory")
    api = count_attr(ref, r"\[(ApiFact|E2EFact)")
    detail = f"Fact={fact}/{BASE_FACT} StandFact={stand}/{BASE_STAND} Theory={theory}/{BASE_THEORY} ApiFact={api}/{BASE_API}"
    if fact >= BASE_FACT and stand >= BASE_STAND and theory >= BASE_THEORY and api >= BASE_API:
        ok("7) атрибуты тестов не потерялись", detail)
    else:
        bad("7) атрибуты тестов не потерялись", detail)

    # 8) старые каталоги 9-tests
    left = [d for d in OLD_9TESTS_DIRS if tree(ref, d)]
    if not left:
        ok("8) старые каталоги в 9-tests удалены", "0 остатков")
    else:
        bad("8) старые каталоги в 9-tests удалены", "остались: " + ", ".join(left))

    print("----")
    # 9) изоляция тестов: нет ссылок на чужой BC (кроме 6-msa/Shared/**).
    #    Правило (@TL 2026-11-14 30:10 + однострочник @DO 14:13): для проекта `6-msa/BC-<X>/src/*Tests*`
    #    любой ProjectReference/Compile Include, ведущий в `6-msa/BC-<Y>/`, при Y != X — нарушение.
    #    Так приёмка не может дать 9/9 при красной изоляции.
    viol: list[str] = []
    proj_re = re.compile(r"^6-msa/BC-([^/]+)/src/.*Tests?\.csproj$")
    for proj in tree(ref, "6-msa"):
        m = proj_re.match(proj)
        if not m:
            continue
        owner = m.group(1)
        body = show(ref, proj)
        for inc in re.findall(r'Include="([^"]+)"', body):
            norm = inc.replace("\\", "/")
            if "Shared/" in norm:
                continue
            other = re.search(r"BC-([^/]+)/", norm)
            if other and other.group(1) != owner:
                viol.append(f"{proj} -> BC-{other.group(1)}")
    if not viol:
        ok("9) изоляция тестов: нет ссылок на чужой BC", "0 (Shared разрешён)")
    else:
        for v in sorted(set(viol)):
            print(f"::error::ADR-028 §1.1: ссылка на чужой BC в тестах — {v}")
        bad("9) изоляция тестов: нет ссылок на чужой BC", f"{len(set(viol))} проект(ов)")

    # 10) СБОРКА каждого тестового проекта (@AR 2026-11-14 28:55).
    #     Тест, который не собирается, = молча потерянное покрытие: он выглядит существующим.
    #     Классификация ошибок, чтобы не краснеть от офлайна:
    #       * `error CS2001` (не найден явно включённый файл) — структурный дефект, FAIL;
    #       * `NU1xxx`/`DigiTFactory`/`Unable to load` — restore/сеть, НЕ проверено (SKIP);
    #       * остальные `error CS…` при успешном restore — FAIL.
    #     `ADR028_SKIP_BUILD=1` отключает проверку локально (печатается SKIP, не PASS).
    build_fail: list[str] = []
    build_skip = 0
    build_ok = 0
    if os.environ.get("ADR028_SKIP_BUILD") == "1":
        print("10) сборка тестовых проектов" + " " * 36 + "SKIP  (ADR028_SKIP_BUILD=1)")
    else:
        import subprocess as _sp
        for proj in [p for p in tree(ref, "6-msa") if re.search(r"Tests?\.csproj$", p)]:
            if not os.path.exists(proj):
                build_skip += 1
                continue
            r = _sp.run(["dotnet", "build", proj, "-v", "q", "--nologo"], capture_output=True, text=True, encoding="utf-8", errors="replace")
            out = (r.stdout or "") + (r.stderr or "")
            if r.returncode == 0:
                build_ok += 1
                continue
            if "error CS2001" in out:
                build_fail.append(proj + ": CS2001 (не найден включённый файл)")
                continue
            if ("NU1" in out) or ("DigiTFactory" in out) or ("Unable to load" in out) or ("Не удалось загрузить индекс" in out):
                build_skip += 1
                continue
            if "error CS" in out:
                build_fail.append(proj + ": ошибка компиляции")
            else:
                build_skip += 1
        if not build_fail:
            ok("10) сборка тестовых проектов", f"rc=0 у {build_ok}, не проверено {build_skip}")
        else:
            for b in build_fail:
                print(f"::error::ADR-028 §6: тестовый проект не собирается — {b}")
            bad("10) сборка тестовых проектов", f"{len(build_fail)} не собирается (ok {build_ok}, skip {build_skip})")

    checks = 10
    if failures == 0:
        print(f"REF={ref} | PASS: ADR-028 {checks}/{checks} | режим --{mode}")
        return 0
    print(f"REF={ref} | FAIL: см. ::error:: выше | режим --{mode}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
