#!/usr/bin/env python3
"""check_front_e2e.py — гейт «в `9-tests/` только то, что требует фронта» (ADR-028).

Правило владельца (2026-11-13, ADR-028): критерий один — **участвует ли фронт/браузер**.
Без фронта тесты живут в `6-msa/**` (зона владельца сервиса); с фронтом — в `9-tests/**`.

Гейт: у каждого проекта в `9-tests/**` (каталог с `*.csproj`, `package.json` или
`playwright.config.*`) обязательны:
  1. маркер `front-e2e.md` — иначе непонятно, какие экраны он проверяет;
  2. браузерная зависимость (`playwright` в `package.json`, `Microsoft.Playwright`
     в `.csproj`, `playwright` в `dependencies`) — иначе это API-тест, его место в `6-msa`.

Зачем: без гейта API-тесты возвращаются в `9-tests` «по привычке», и зона @TE снова
становится «все тесты», что и было отменено ADR-028.

Обратная проверка (@AR 2026-11-14 06:50): гейт смотрел только «есть ли маркер у проекта»
и не замечал **постороннее** — например `9-tests/Load/` (нагрузочные `*.js`/`*.ps1` без
браузера). Теперь в `9-tests/**` разрешён только предсказуемый набор, а любой исполняемый
каталог без Playwright — ошибка.

    python tools/check_front_e2e.py            # по репозиторию
    python tools/check_front_e2e.py --root d   # другой корень (негативный контроль)
    python tools/check_front_e2e.py --selftest
"""
import argparse
import json
import os
import re
import sys

MARKER = "front-e2e.md"
SKIP_DIRS = {".git", "node_modules", "bin", "obj", ".loopmaker", ".tmp-front-e2e"}
# Что вообще может лежать в 9-tests (верхний уровень): только фронтовые E2E и служебное.
ALLOWED_TOP = {"RnD", "scripts", "reports", "Load"}
ALLOWED_FILES = {"README.md", "AGENTS.md", "front-e2e.md", "package.json", "package-lock.json",
                 "playwright.config.js", "playwright.config.ts", ".gitignore"}
EXEC_EXT = (".js", ".mjs", ".cjs", ".ts", ".ps1", ".sh", ".cs", ".csproj", ".py")
PLAYWRIGHT_CS = re.compile(r"Microsoft\.Playwright", re.I)


def projects(root):
    """Каталоги-проекты внутри 9-tests: (путь, вид)."""
    base = os.path.join(root, "9-tests")
    out = []
    if not os.path.isdir(base):
        return out
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        kind = None
        if any(f.endswith(".csproj") for f in filenames):
            kind = "csproj"
        elif "package.json" in filenames:
            kind = "node"
        if kind:
            out.append((dirpath, kind))
    return out


def has_browser(dirpath, kind):
    if kind == "node":
        try:
            pkg = json.load(open(os.path.join(dirpath, "package.json"), encoding="utf-8"))
        except (OSError, ValueError):
            return False
        deps = {}
        for key in ("dependencies", "devDependencies"):
            deps.update(pkg.get(key) or {})
        return any("playwright" in k.lower() for k in deps)
    for name in os.listdir(dirpath):
        if name.endswith(".csproj"):
            try:
                text = open(os.path.join(dirpath, name), encoding="utf-8-sig").read()
            except OSError:
                continue
            if PLAYWRIGHT_CS.search(text):
                return True
    return False


def scan(root):
    """[(rel, marks_ok, browser_ok)]"""
    res = []
    for dirpath, kind in projects(root):
        rel = os.path.relpath(dirpath, root).replace("\\", "/")
        marks_ok = os.path.exists(os.path.join(dirpath, MARKER))
        res.append((rel, marks_ok, has_browser(dirpath, kind)))
    return res


def foreign_in_tests(root):
    """Каталоги/файлы в 9-tests, которые не обязаны быть фронтовыми.

    Правило @AR: `9-tests` — только браузерные E2E. Каталог, где есть исполняемые файлы,
    но НЕТ Playwright (ни в package.json, ни в csproj) — посторонний.
    """
    base = os.path.join(root, "9-tests")
    out = []
    if not os.path.isdir(base):
        return out
    for top in sorted(os.listdir(base)):
        full = os.path.join(base, top)
        if not os.path.isdir(full):
            if top not in ALLOWED_FILES and top.endswith(EXEC_EXT):
                out.append(("9-tests/" + top, "исполняемый файл вне разрешённого набора"))
            continue
        if top not in ALLOWED_TOP:
            rel = "9-tests/" + top
            out.append((rel, "каталог не из разрешённого набора (%s)" % ", ".join(sorted(ALLOWED_TOP))))
            continue
        # Load: нагрузочные скрипты в 9-tests — расхождение с ADR-028 (место — 7-deploy/load)
        if top == "Load":
            ex = [f for dp, dn, fn in os.walk(full) for f in fn if f.endswith(EXEC_EXT)]
            if ex:
                out.append(("9-tests/Load", "нагрузочные скрипты (%d исполняемых) — место "
                                            "7-deploy/load по ADR-028" % len(ex)))
    return out


def selftest():
    import shutil
    ok = 0
    # tempfile под сэндбоксом: системный %TEMP% может быть недоступен на удаление,
    # поэтому рабочая папка — рядом с репозиторием, а уборка терпит ошибку прав.
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-front-e2e")
    cases = [
        # (файлы проекта, ожидание «всё хорошо»)
        ({"front-e2e.md": "x", "package.json": '{"devDependencies":{"@playwright/test":"1"}}'}, True),
        ({"front-e2e.md": "x", "package.json": '{"dependencies":{"left-pad":"1"}}'}, False),
        ({"front-e2e.md": "x", "t.csproj": "<Project><PackageReference Include=\"Microsoft.Playwright\" /></Project>"}, True),
        ({"t.csproj": "<Project />"}, False),
        ({"front-e2e.md": "x", "t.csproj": "<Project />"}, False),
    ]
    shutil.rmtree(base, ignore_errors=True)
    for files, expect_ok in cases:
        tmp = os.path.join(base, "case")
        shutil.rmtree(tmp, ignore_errors=True)
        proj = os.path.join(tmp, "9-tests", "probe")
        os.makedirs(proj)
        for name, content in files.items():
            open(os.path.join(proj, name), "w", encoding="utf-8").write(content)
        found = scan(tmp)
        good = bool(found) and all(m and b for _, m, b in found)
        if good == expect_ok:
            ok += 1
        else:
            print("  [FAIL] %s -> good=%s, ждали %s" % (sorted(files), good, expect_ok))
    # обратная проверка: Load с исполняемым скриптом и без браузера — посторонний
    shutil.rmtree(base, ignore_errors=True)
    load = os.path.join(base, "case", "9-tests", "Load")
    os.makedirs(load)
    open(os.path.join(load, "load.js"), "w", encoding="utf-8").write("// load\n")
    found = foreign_in_tests(os.path.join(base, "case"))
    if any(p == "9-tests/Load" for p, _ in found):
        ok += 1
    else:
        print("  [FAIL] Load не распознан как посторонний: %s" % found)
    cases.append(("foreign", None))

    shutil.rmtree(base, ignore_errors=True)
    print("check_front_e2e --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    found = scan(root)
    bad = [(rel, m, b) for rel, m, b in found if not (m and b)]
    for rel, m, b in bad:
        why = []
        if not m:
            why.append("нет `%s`" % MARKER)
        if not b:
            why.append("нет браузерной зависимости (Playwright)")
        print("::error::%s: %s — по ADR-028 тесты без фронта живут в `6-msa/**`"
              % (rel, ", ".join(why)))
    foreign = foreign_in_tests(root)
    for rel, why in foreign:
        print("::error::%s: %s — в `9-tests` допустимы только браузерные E2E (ADR-028)"
              % (rel, why))
    print("check_front_e2e: проектов в 9-tests %d, нарушений %d, посторонних %d"
          % (len(found), len(bad), len(foreign)))
    bad += foreign
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
