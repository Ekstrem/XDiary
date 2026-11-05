"""check_e2e_interactions.py — у каждого `click('#id')`/`fill('#id')` в спеке есть адресат во фронте.

Зачем: «элемент есть» и «элемент что-то делает» — разные вещи. Селектор может найтись (гейт
`check_e2e_selectors` зелёный), а клик уходит в **пустоту**: обработчик отвязали при рефакторинге,
кнопка осталась. Спека тогда падает по таймауту ожидания результата — и это выглядит как дефект
продукта, хотя сломан тест/связка.

Проверки для `page.click('#x')` / `page.fill('#x', …)`:
1. **id существует** во фронте (HTML или JS-шаблоны);
2. **клик** — у id есть обработчик: `$("x").addEventListener`, `$("x").onclick`, `id="x" … onclick=`
   в разметке, либо `onclick=`-атрибут в шаблоне;
3. **fill** — id объявлен как `<input>`/`<textarea>`/`<select>` (в HTML или JS-шаблоне) либо имеет
   `contenteditable`; иначе «поле» окажется, например, `<div>`, и `fill` упадёт.

Коды: `0` — все адресаты на месте; `1` — есть нарушения; `2` — не измерено (нет фронта/спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# клики и ввод в спеках
CLICK = re.compile(r"\.(?:click|check|uncheck|hover|selectOption)\(\s*'#([\w-]+)'")
FILL = re.compile(r"\.(?:fill|type|press)\(\s*'#([\w-]+)'")
# обработчики во фронте
BIND_ADD = re.compile(r"""\$\("([\w-]+)"\)\.addEventListener""")
BIND_ONCLICK = re.compile(r"""\$\("([\w-]+)"\)\.onclick\s*=""")
BIND_QSA = re.compile(r"""querySelector\("#([\w-]+)"\)\.addEventListener""")
HTML_ONCLICK = re.compile(r"""id="([\w-]+)"[^>]*?\bonclick=""")
# поля ввода: ищем открывающий тег с нужным id
FIELD = re.compile(r"""<(?:input|textarea|select)[^>]*\bid="([\w-]+)\"""")
EDITABLE = re.compile(r"""id="([\w-]+)"[^>]*contenteditable""")
IDS = re.compile(r'id="([^"]+)"')


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
    bound = set(BIND_ADD.findall(js)) | set(BIND_ONCLICK.findall(js)) | set(BIND_QSA.findall(js))
    bound |= set(HTML_ONCLICK.findall(html))
    fields = set(FIELD.findall(html)) | set(FIELD.findall(js)) | set(EDITABLE.findall(html))
    front = {
        "ids": set(IDS.findall(html)) | set(IDS.findall(js)),
        "bound": bound,
        "fields": fields,
    }
    specs = {}
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            specs[p.name] = p.read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        return None, f"не читается {getattr(e, 'filename', tests_dir)}: {e}"
    return (front, specs), None


def evaluate(front, specs):
    """(issues, stats)."""
    issues = []
    clicks = fills = 0
    for name, text in sorted(specs.items()):
        for target in sorted(set(CLICK.findall(text))):
            clicks += 1
            if target not in front["ids"]:
                issues.append(f"{name}: клик по `#{target}` — такого id нет во фронте")
            elif target not in front["bound"]:
                issues.append(f"{name}: клик по `#{target}` — id есть, но обработчика нет "
                              f"(проверьте привязку во фронте)")
        for target in sorted(set(FILL.findall(text))):
            fills += 1
            if target not in front["ids"]:
                issues.append(f"{name}: ввод в `#{target}` — такого id нет во фронте")
            elif target not in front["fields"]:
                issues.append(f"{name}: ввод в `#{target}` — это не поле ввода "
                              f"(input/textarea/select/contenteditable)")
    stats = {"specs": len(specs), "clicks": clicks, "fills": fills,
             "bound": len(front["bound"]), "fields": len(front["fields"])}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | кликов {stats['clicks']}, вводов {stats['fills']} | "
            f"адресатов во фронте {stats['bound']} (полей {stats['fields']}) | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    data, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_interactions: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(*data)
    for m in issues:
        print(f"::error::check_e2e_interactions: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, front, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(front, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    front = {"ids": {"btn-save", "in-name", "btn-dead", "div-not-field"},
             "bound": {"btn-save"}, "fields": {"in-name"}}

    case("норма: клик по привязанному, ввод в поле",
         front, {"a.spec.js": "await page.click('#btn-save'); await page.fill('#in-name', 'x');"},
         0, ["нарушено: 0", "кликов 1, вводов 1"])

    case("клик по id без обработчика",
         front, {"b.spec.js": "await page.click('#btn-dead');"}, 1,
         ["btn-dead", "обработчика нет"])

    case("клик по несуществующему id",
         front, {"c.spec.js": "await page.click('#btn-nope');"}, 1, ["такого id нет"])

    case("ввод в не-поле",
         front, {"d.spec.js": "await page.fill('#div-not-field', 'x');"}, 1,
         ["не поле ввода"])

    total += 1
    line = receipt_line({"specs": 2, "clicks": 3, "fills": 1, "bound": 5, "fields": 2}, [])
    if "кликов 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_interactions --selftest: {ok}/{total} "
          f"(клик без обработчика / ввод не в поле = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
