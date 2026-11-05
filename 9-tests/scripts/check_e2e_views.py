"""check_e2e_views.py — экраны, которых ждут спеки, фронт умеет показывать.

Зачем: спека проверяет «после действия виден `#view-x`». Если фронт такой экран никогда не показывает
(`showView('x')` вызовов нет), ожидание гарантированно упадёт по таймауту — и это будет выглядеть как
дефект продукта, хотя ожидание мертво с самого начала. Обратная сторона: `showView('y')` в коде, для
которого нет разметки `id="view-y"`, — экран-призрак (код переключает в пустое место).

Проверки:
1. каждый литерал `showView('<x>')` имеет разметку `id="view-x"`;
2. каждый `#view-*`, которого ждёт спека, **есть среди целей `showView`** (иначе ожидание мертво);
   исключения — экраны, которые показываются не через `showView` (объявляются явно, с причиной).

Коды: `0` — расхождений нет; `1` — есть; `2` — не измерено (нет фронта или спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SHOW = re.compile(r"""showView\(\s*["']([\w-]+)["']""")
VIEW_ID = re.compile(r'id="(view-[\w-]+)"')
SPEC_VIEW = re.compile(r"#(view-[\w-]+)")

# экраны, которые показываются НЕ через `showView` (причина обязательна)
NOT_VIA_SHOWVIEW = {
    "view-splash": "стартовый экран разметки: виден до инициализации, скрывается напрямую",
}


def evaluate(html, js, specs):
    """(issues, stats)."""
    ids = set(VIEW_ID.findall(html)) | set(VIEW_ID.findall(js))
    shown = {"view-" + name for name in SHOW.findall(js)}
    issues = []

    for target in sorted(shown - ids):
        issues.append(f"фронт переключает на `{target}`, а разметки с таким id нет (экран-призрак)")

    waited = set()
    for name, text in sorted(specs.items()):
        for view in set(SPEC_VIEW.findall(text)):
            waited.add(view)
            if view in NOT_VIA_SHOWVIEW:
                continue
            if view not in shown:
                where = "разметке" if view in ids else "нигде"
                issues.append(f"{name}: ждёт `#{view}`, но фронт не делает showView — "
                              f"экран есть только в {where} (ожидание мертво)")

    stats = {"specs": len(specs), "views_waited": len(waited), "shown": len(shown),
             "ids": len(ids), "exceptions": len(NOT_VIA_SHOWVIEW)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | ждут экранов {stats['views_waited']} | "
            f"showView-целей {stats['shown']} | разметки {stats['ids']} | "
            f"исключений {stats['exceptions']} | нарушено: {len(issues)}")


def collect(root: pathlib.Path):
    index = root / "8-front" / "index.html"
    js_dir = root / "8-front" / "js"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not tests_dir.is_dir():
        return None, "нет 8-front/index.html или каталога спек"
    html = index.read_text(encoding="utf-8", errors="replace")
    js = ""
    if js_dir.is_dir():
        for p in sorted(js_dir.glob("*.js")):
            js += "\n" + p.read_text(encoding="utf-8", errors="replace")
    specs = {}
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            specs[p.name] = p.read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        return None, f"не читается {getattr(e, 'filename', tests_dir)}: {e}"
    return (html, js, specs), None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    data, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_views: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(*data)
    for m in issues:
        print(f"::error::check_e2e_views: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, html, js, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(html, js, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    html = '<div id="view-splash"></div><div id="view-journal"></div><div id="view-wizard"></div>'
    js = 'showView("journal"); showView("wizard");'

    case("норма",
         html, js, {"a.spec.js": "await expect(page.locator('#view-journal')).toBeVisible();"}, 0,
         ["нарушено: 0", "ждут экранов 1"])

    case("ожидание экрана, который фронт не показывает",
         html, js, {"b.spec.js": "await expect(page.locator('#view-trends')).toBeVisible();"}, 1,
         ["#view-trends", "ожидание мертво"])

    case("showView на экран без разметки",
         html, js + ' showView("ghost");', {"a.spec.js": "page.locator('#view-journal')"}, 1,
         ["view-ghost", "экран-призрак"])

    case("исключение splash не нарушает",
         html, js, {"c.spec.js": "await expect(page.locator('#view-splash')).toBeHidden();"}, 0,
         ["нарушено: 0"])

    total += 1
    line = receipt_line({"specs": 2, "views_waited": 3, "shown": 4, "ids": 5, "exceptions": 1}, [])
    if "ждут экранов 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_views --selftest: {ok}/{total} (мёртвое ожидание / экран-призрак = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
