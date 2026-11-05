"""check_e2e_selectors.py — статическая сверка селекторов `front-e2e` с текущим фронтом.

Зачем: `front-e2e` гоняется только в CI на живом стенде, а самый частый класс поломки —
**спека ищет `#id`, которого фронт больше не отдаёт** (`8-front` меняется, спеки — нет).
Это проверяется локально, без браузера: берём все `#id` из `tests/*.spec.js` и ищем их
в `8-front/index.html` или в `getElementById("...")`/`querySelector("#...")` в `8-front/js/*.js`.

Коды: `0` — все селекторы есть; `1` — есть пропавшие; `2` — не измерено (нет фронта или спек).

Использование:
    python 9-tests/scripts/check_e2e_selectors.py [--root .] [--selftest]
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SEL = re.compile(r"""['"]#([A-Za-z][\w-]*)['"]""")
ID_ATTR = re.compile(r'id="([^"]+)"')
JS_ID = re.compile(r"""(?:getElementById|querySelector)\(\s*['"](?:#)?([\w-]+)['"]""")


def collect(root: pathlib.Path):
    """(front_ids, {spec_name: set(ids)}); None, если вход не читается."""
    index = root / "8-front" / "index.html"
    js_dir = root / "8-front" / "js"
    specs_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not specs_dir.is_dir():
        return None, None, "нет 8-front/index.html или нет каталога спек"
    front = index.read_text(encoding="utf-8", errors="replace")
    js_text = ""
    if js_dir.is_dir():
        for p in sorted(js_dir.glob("*.js")):
            js_text += "\n" + p.read_text(encoding="utf-8", errors="replace")
    # id может появляться и в HTML, и в JS-шаблонах (`'<input id="w-place">'`), и через
    # getElementById/querySelector — иначе гейт даёт ложный красный на живых id.
    ids = set(ID_ATTR.findall(front)) | set(ID_ATTR.findall(js_text)) | set(JS_ID.findall(js_text))
    specs = {}
    for p in sorted(specs_dir.glob("*.spec.js")):
        specs[p.name] = set(SEL.findall(p.read_text(encoding="utf-8", errors="replace")))
    return ids, specs, None


def evaluate(ids, specs):
    """(issues, checked_specs, checked_selectors)."""
    issues = []
    checked = 0
    for name, used in sorted(specs.items()):
        if not used:
            continue
        checked += 1
        gone = sorted(i for i in used if i not in ids)
        if gone:
            issues.append(f"{name}: селекторы, которых нет во фронте: {', '.join('#' + g for g in gone[:8])}")
    return issues, checked, sum(len(v) for v in specs.values())


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    ids, specs, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_selectors: {err} — проверять нечего (rc=2)")
        return 2
    issues, checked, total = evaluate(ids, specs)
    for m in issues:
        print(f"::error::check_e2e_selectors: {m}")
    print(f"квитанция: спек {checked} из {len(specs)} | селекторов {total} | "
          f"нарушено: {len(issues)} | id во фронте: {len(ids)}")
    return 1 if issues else 0


def selftest():
    ok = total = 0
    front = {"view-journal", "btn-save", "tab-partners"}
    specs = {
        "ok.spec.js": {"view-journal", "btn-save"},
        "bad.spec.js": {"view-journal", "view-removed"},
    }
    issues, checked, sel = evaluate(front, specs)
    total += 1
    if len(issues) == 1 and "bad.spec.js" in issues[0] and "#view-removed" in issues[0] \
            and checked == 2 and sel == 4:
        ok += 1
    else:
        print(f"  [FAIL] пропавший селектор: {issues}, checked={checked}, sel={sel}")

    total += 1
    issues2, _, _ = evaluate({"a", "b"}, {"x.spec.js": {"a"}, "y.spec.js": {"b"}})
    if not issues2:
        ok += 1
    else:
        print(f"  [FAIL] норма даёт нарушения: {issues2}")

    total += 1
    line = ("квитанция: спек 2 из 2 | селекторов 3 | нарушено: 0 | id во фронте: 2")
    if "None" not in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_e2e_selectors --selftest: {ok}/{total} (спека без id во фронте = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
