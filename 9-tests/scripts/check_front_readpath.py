#!/usr/bin/env python3
"""XDiary: гейт read-path и индикатора занятости фронта (9-tests, зона @TE).

Проверяет **то, что можно проверить без браузера**, и не притворяется поведенческим тестом:
поведение (визард открылся при отказе чтения, баннер виден, ложной подстановки нет) доказывает
только `9-tests/RnD/front-e2e/tests/wizard-offline.spec.js` — он требует браузерного раннера.

Зачем: критерий «в `repository.js` нет молчаливых `catch`» — **вакуумный**, потому что в этом
файле нет ни одного `catch` (проверка не может провалиться). Здесь вместо него — предикаты,
которые ломаются, если фича удалена или переименована:

  1. `showReadError` + `hideReadError` + `reportReadFailure` + `openWizardDegraded` есть в `app.js`;
  2. баннер создаётся с `setAttribute("role", "alert")` (доступность, а не просто div);
  3. `busyRun(...)` применяется к трендам и к экспорту CSV (тяжёлые экраны, FE-040);
  4. `APP_VERSION` в `app.js` синхронизирован с `CACHE` в `sw.js` (иначе PWA отдаёт старый кэш).

Коды возврата: 0 — всё есть; 1 — чего-то нет (с именем предиката); 2 — нечего проверять
(файлы не найдены / фронт отсутствует).

Запуск: python 9-tests/scripts/check_front_readpath.py [--root <путь к 8-front>] [--selftest]
"""
from __future__ import annotations

import argparse
import re
import sys

try:                                     # UTF-8 вывод в cp1251-консоли
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:                        # noqa: BLE001
    pass

DEFAULT_ROOT = "8-front"


def read(path: str) -> str:
    with open(path, "r", encoding="utf-8") as fh:
        return fh.read()


def check_artifacts(text: str) -> list[str]:
    """(1) артефакты read-path из карточки FE-043 / списка @AR."""
    missing = [n for n in ("showReadError", "hideReadError", "reportReadFailure", "openWizardDegraded")
               if n not in text]
    return [f"FE-043: нет `{n}` в app.js" for n in missing]


def check_alert_role(text: str) -> list[str]:
    """(2) баннер обязан быть `role="alert"`, иначе скринридер молчит."""
    if re.search(r'setAttribute\(\s*["\']role["\']\s*,\s*["\']alert["\']\s*\)', text):
        return []
    if re.search(r'role\s*=\s*["\']alert["\']', text):
        return []
    return ["FE-043: баннер read-path создаётся без `role=\"alert\"`"]


def check_observable(text: str) -> list[str]:
    """(2b) признак ошибки обязан быть **наблюдаемым** (`read-paths.md` §5, уточнение 2026-11-16).

    Спека ищет текст в `#view-wizard`/`#toast`. Баннер, вставленный только в `document.body`
    или вытесняемый `innerHTML` шага, — «верный по смыслу, но невидимый для проверки» код.
    Требуем оба канала: `toast(...)` И предпочтение `#view-wizard` как host.
    """
    problems = []
    if "toast(" not in text:
        problems.append("FE-043: нет `toast(...)` — признак не попадёт в `#toast` (наблюдаемость)")
    if "getElementById(\"view-wizard\")" not in text and "getElementById('view-wizard')" not in text:
        problems.append("FE-043: баннер не предпочитает `#view-wizard` как host (спека ищет признак там)")
    return problems


def check_busy(text: str) -> list[str]:
    """(3) индикатор применяется там, где операции тяжёлые: тренды и экспорт CSV."""
    if "function busyRun" not in text and "busyRun =" not in text:
        return ["FE-040: нет `busyRun` — индикатор занятости отсутствует"]
    problems = []
    if not re.search(r'busyRun\(\s*["\'][^"\']*тренд', text, re.IGNORECASE):
        problems.append("FE-040: `busyRun` не применяется к трендам")
    if not re.search(r'busyRun\(\s*["\'][^"\']*CSV', text, re.IGNORECASE) and \
            not re.search(r'busyRun\([^)]*downloadCsv', text):
        problems.append("FE-040: `busyRun` не применяется к экспорту CSV")
    if "setTimeout" not in text:
        problems.append("FE-040: нет отложенного показа (порог 300 мс)")
    return problems


def check_version_sync(app_text: str, sw_text: str) -> list[str]:
    """(4) APP_VERSION ↔ CACHE: иначе PWA отдаёт старый кэш и правка «не доезжает»."""
    m1 = re.search(r'APP_VERSION\s*=\s*["\']([\d.]+)["\']', app_text)
    m2 = re.search(r'CACHE\s*=\s*["\']xdiary-v(\d+)["\']', sw_text)
    if not m1 or not m2:
        return ["версия: не найдено `APP_VERSION` в app.js или `CACHE` в sw.js"]
    ver, cache = m1.group(1), m2.group(1)
    # `0.93` -> `93`, `93` -> `93`: сравниваем нормализованные числа, а не строки
    norm = lambda v: str(int(re.sub(r"[^0-9]", "", v) or "0"))
    if norm(ver) != norm(cache):
        return [f"версия: APP_VERSION={ver} не совпадает с CACHE=xdiary-v{cache}"]
    return []


def run(root: str) -> int:
    import os
    app = os.path.join(root, "js", "app.js")
    sw = os.path.join(root, "sw.js")
    try:
        app_text = read(app)
        sw_text = read(sw) if os.path.exists(sw) else ""
    except OSError as e:
        print(f"::error::check_front_readpath: не читается {app} ({e}); проверять нечего (rc=2)")
        return 2
    problems = (check_artifacts(app_text) + check_alert_role(app_text)
                + check_observable(app_text) + check_busy(app_text)
                + check_version_sync(app_text, sw_text))
    for p in problems:
        print(f"::error::check_front_readpath: {p}")
    if problems:
        print(f"FAIL: {len(problems)} проблем(ы) в read-path/индикаторе ({root})")
        return 1
    print("PASS: артефакты read-path, role=alert, busyRun (тренды+CSV), версия в синхроне")
    return 0


def selftest() -> int:
    """Красный контроль: каждая проверка обязана падать на своём дефекте."""
    good_app = (
        'var APP_VERSION = "0.93";\n'
        'function showReadError(t){ var el=document.createElement("div"); '
        'el.setAttribute("role","alert"); toast(t, 6000); '
        'document.getElementById("view-wizard").appendChild(el); }\n'
        'function hideReadError(){}\nfunction reportReadFailure(d){}\nfunction openWizardDegraded(e){}\n'
        'function busyRun(l,t,r){ setTimeout(function(){},300); }\n'
        'busyRun("Считаем тренды", function(){ return 1; });\n'
        'busyRun("Готовим CSV", function(){ return 1; }, downloadCsv);\n'
    )
    good_sw = 'var CACHE = "xdiary-v93";\n'
    cases = [
        ("чистая норма", good_app, good_sw, 0),
        ("нет showReadError", good_app.replace("function showReadError(t)", "function showReadErr(t)"),
         good_sw, 1),
        ("баннер без role", good_app.replace('el.setAttribute("role","alert");', ""), good_sw, 1),
        ("баннер без toast/визарда", good_app.replace('toast(t, 6000); ', "").replace('document.getElementById("view-wizard").appendChild(el); ', ""), good_sw, 1),
        ("busyRun без трендов", good_app.replace('busyRun("Считаем тренды", function(){ return 1; });', ""),
         good_sw, 1),
        ("версия рассинхронена", good_app, 'var CACHE = "xdiary-v92";\n', 1),
    ]
    ok = 0
    for name, app, sw, want_fail in cases:
        problems = (check_artifacts(app) + check_alert_role(app) + check_observable(app)
                    + check_busy(app) + check_version_sync(app, sw))
        got = 1 if problems else 0
        if got == want_fail:
            ok += 1
        else:
            print(f"  [FAIL] {name}: получили {'проблемы' if problems else 'чисто'}, "
                  f"ждали {'проблему' if want_fail else 'чисто'}")
    total = len(cases)
    print(f"check_front_readpath --selftest: {ok}/{total} (красный контроль на каждом предикате)")
    return 0 if ok == total else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=DEFAULT_ROOT, help="папка фронта (по умолчанию 8-front)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    return run(args.root)


if __name__ == "__main__":
    raise SystemExit(main())
