#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Гейт: спека не должна навигировать ДО установки E2E-режима (иначе тест идёт в ПРОД).

Причина (@DO 2026-11-19 08:05): во фронте `baseUrl()` отдаёт `custom` только при
`window.__XDIARY_E2E__ === true`; иначе он **чистит** `xdiary.apiBase` и возвращает
`DEFAULT_BASE = https://xdiary-vps1…` (прод). Значит спека, которая делает `goto('/')`
раньше, чем `installBase(page)` (или `loginWithPin`, который вызывает его первым шагом),
гоняет сценарий **против прода** — вплоть до `POST`/`delete`, то есть меняет прод-данные.

Правило: в каждом `*.spec.js` первый вызов `goto(`/`page.goto` должен идти ПОСЛЕ
`installBase(` или `loginWithPin(` (оба ставят E2E-режим до загрузки приложения).

Коды: 0 — нарушений нет; 1 — есть спека с навигацией до E2E-режима; 2 — не найдена папка.
`--selftest` проверяет гейт на двух фикстурах (правильная и нарушающая).
"""
import os
import sys

ROOT_DEFAULT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUARDS = ("installBase(", "loginWithPin(")


def spec_files(root):
    tests = os.path.join(root, "9-tests", "RnD", "front-e2e", "tests")
    if not os.path.isdir(tests):
        return None
    return [os.path.join(tests, n) for n in sorted(os.listdir(tests)) if n.endswith(".spec.js")]


def check_file(path):
    """Возвращает (has_goto, line_of_first_goto, guarded_before) — только для исполняемых строк."""
    code = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        for i, line in enumerate(fh, 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("/*") or stripped.startswith("*"):
                continue
            code.append((i, line))
    first_goto = None
    for i, line in code:
        if "goto(" in line:
            first_goto = i
            break
    if first_goto is None:
        return False, None, True
    guarded = any(any(g in line for g in GUARDS) and i < first_goto for i, line in code)
    return True, first_goto, guarded


def scan(root):
    files = spec_files(root)
    if files is None:
        return None
    bad, total = [], 0
    for path in files:
        has_goto, line, guarded = check_file(path)
        if not has_goto:
            continue
        total += 1
        if not guarded:
            bad.append((os.path.basename(path), line))
    return total, bad


def selftest():
    import tempfile
    cases = []
    with tempfile.TemporaryDirectory() as tmp:
        tests = os.path.join(tmp, "9-tests", "RnD", "front-e2e", "tests")
        os.makedirs(tests, exist_ok=True)
        good = os.path.join(tests, "good.spec.js")
        with open(good, "w", encoding="utf-8") as fh:
            fh.write("const { installBase } = require('./api');\n"
                     "test('x', async ({ page }) => {\n"
                     "  await installBase(page);\n"
                     "  await page.goto('/');\n"
                     "});\n")
        bad = os.path.join(tests, "bad.spec.js")
        with open(bad, "w", encoding="utf-8") as fh:
            fh.write("const { installBase } = require('./api');\n"
                     "test('y', async ({ page }) => {\n"
                     "  await page.goto('/');\n"
                     "  await installBase(page);\n"
                     "});\n")
        no_goto = os.path.join(tests, "nogoto.spec.js")
        with open(no_goto, "w", encoding="utf-8") as fh:
            fh.write("test('z', async () => { /* без навигации */ });\n")

        total, found = scan(tmp)
        names = [n for n, _ in found]
        cases.append(("спек с goto: 2 (третья без навигации не считается)", total == 2, str(total)))
        cases.append(("good не в нарушениях", "good.spec.js" not in names, str(names)))
        cases.append(("bad в нарушениях", "bad.spec.js" in names, str(names)))
        cases.append(("nogoto не в нарушениях", "nogoto.spec.js" not in names, str(names)))

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
        print("ОШИБКА: нет папки 9-tests/RnD/front-e2e/tests в %s" % root)
        return 2
    total, bad = res
    print("квитанция: спек с навигацией %d | goto до E2E-режима %d | нарушений %d"
          % (total, len(bad), len(bad)))
    for name, line in bad:
        print("  ! %s — первый goto на L%d до installBase/loginWithPin ⇒ тест может уйти в ПРОД" % (name, line))
    if bad:
        print("ИТОГ: rc=1 — есть навигация до E2E-режима (класс «тест пишет в прод»)")
        return 1
    print("ИТОГ: rc=0 — все навигации идут после E2E-режима")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
