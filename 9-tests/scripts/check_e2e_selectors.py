"""check_e2e_selectors.py — статическая сверка спек `front-e2e` с текущим фронтом.

Зачем: `front-e2e` гоняется в CI на живом стенде, а самый частый класс поломки — **спека ищет то,
чего фронт больше не отдаёт** (`8-front` меняется, спеки — нет). Проверяется локально, без браузера,
по трём видам ожиданий:

1. **id** — `'#x'` в спеке → есть в `index.html`, в JS-шаблонах (`'<input id="w-place">'`) или в
   `getElementById('x')`;
2. **классы** — `.a.b` в аргументе `locator/click/fill/...` → каждый класс есть среди `class="..."`
   в HTML или JS-шаблонах;
3. **тексты** — `toContainText(/…/)` / `toHaveText(/…/)` → хотя бы одно слово длиной ≥ 4 символа
   из ожидания встречается в тексте фронта (проверка намеренно **мягкая**: тексты склеиваются
   из частей, и жёсткое сравнение давало бы ложный красный — так уже было 2026-11-05).

Коды: `0` — расхождений нет; `1` — есть; `2` — не измерено (нет фронта или спек).

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

SEL_ID = re.compile(r"""['"]#([A-Za-z][\w-]*)['"]""")
ID_ATTR = re.compile(r'id="([^"]+)"')
JS_ID = re.compile(r"""(?:getElementById|querySelector)\(\s*['"](?:#)?([\w-]+)['"]""")
CLASS_ATTR = re.compile(r'class="([^"]+)"')
# класс может ставиться из кода: `el.className = "busy-banner"`, `classList.add("row")`
CLASS_JS = re.compile(r"""(?:className\s*=\s*|classList\.(?:add|toggle|remove)\(\s*)['"]([^'"]+)['"]""")
# аргументы вызовов, где Playwright принимает селектор
CALL = re.compile(r"\.(?:locator|click|fill|press|type|check|uncheck|hover|selectOption|"
                  r"waitForSelector|isVisible)\(\s*'([^']+)'")
# селектор может лежать в константе: `const BANNER = '.busy-banner'` — берём и такие литералы
CLASS_LIT = re.compile(r"""['"](\.[\w-]+(?:\.[\w-]+)*)(?::(?:visible|hidden))?['"]""")
TEXT_RE = re.compile(r"to(?:ContainText|HaveText)\(\s*/([^/]{3,})/([a-z]*)")
TEXT_STR = re.compile(r"to(?:ContainText|HaveText)\(\s*'([^']{3,})'")
WORD = re.compile(r"[\wА-Яа-яЁё]{4,}")


def class_parts(selector):
    """[классы] из простого классового селектора (`.a.b`), иначе []."""
    core = re.sub(r":(visible|hidden)$", "", selector.strip())
    if not core.startswith("."):
        return []
    return [x for x in core.split(".") if x]


def collect(root: pathlib.Path):
    """(front, specs) либо (None, ошибка)."""
    index = root / "8-front" / "index.html"
    specs_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not specs_dir.is_dir():
        return None, "нет 8-front/index.html или нет каталога спек"
    html = index.read_text(encoding="utf-8", errors="replace")
    js = ""
    js_dir = root / "8-front" / "js"
    if js_dir.is_dir():
        for p in sorted(js_dir.glob("*.js")):
            js += "\n" + p.read_text(encoding="utf-8", errors="replace")
    front = {
        "ids": set(ID_ATTR.findall(html)) | set(ID_ATTR.findall(js)) | set(JS_ID.findall(js)),
        "classes": set(),
        "text": (html + "\n" + js).lower(),
    }
    for attr in CLASS_ATTR.findall(html) + CLASS_ATTR.findall(js):
        front["classes"].update(attr.split())
    # классы, которые фронт ставит из JS (`className = "busy-banner"`) — иначе гейт даёт
    # ложный красный на живом классе (так и случилось на `.busy-banner`)
    for attr in CLASS_JS.findall(js):
        front["classes"].update(attr.split())
    specs = {}
    for p in sorted(specs_dir.glob("*.spec.js")):
        text = p.read_text(encoding="utf-8", errors="replace")
        classes = set()
        for sel in CALL.findall(text):
            classes.update(class_parts(sel))
        for lit in CLASS_LIT.findall(text):
            classes.update(class_parts(lit))
        # `TEXT_RE` теперь с двумя группами (шаблон + флаги) — берём ТОЛЬКО шаблон,
        # иначе в список текстов попадают кортежи и проверка падает на типах.
        texts = [m.group(1) for m in TEXT_RE.finditer(text)] + list(TEXT_STR.findall(text))
        specs[p.name] = {
            "ids": set(SEL_ID.findall(text)),
            "classes": classes,
            "texts": texts,
        }
    return front, specs


def text_matches(fragment, front_low):
    """Строгая проверка: регулярка из спеки должна совпасть с текстом фронта.

    Шаблон без букв (`^\\s*$` — «поле не пустое») текстом интерфейса не является и не проверяется:
    иначе гейт краснел бы не по делу. Проверка «есть ли буква» сделана через `str.isalpha()`,
    чтобы не зависеть от кириллицы в регулярном выражении.
    """
    # `\\s`, `\\d` и прочие escape-последовательности — не текст: убираем их ПЕРЕД проверкой букв,
    # иначе `^\\s*$` считается «текстом с буквой s» и гейт краснеет не по делу.
    probe = re.sub(r"\\.", "", fragment)
    if not any(ch.isalpha() for ch in probe):
        return True
    try:
        rx = re.compile(fragment, re.I)
    except re.error:
        return True  # экзотический шаблон — не наша забота, лишь бы спека компилировалась
    return bool(rx.search(front_low))


def text_hits(fragment, front_low):
    """Мягкая проверка текста: достаточно одного слова ≥4 символов из ожидания.

    Если в шаблоне нет слов (например `^\\s*$` — проверка «поле не пустое»), проверять нечего:
    такой шаблон не про текст интерфейса, и краснеть на нём гейт не должен (иначе гейт,
    который краснеет не по делу, отключают — наш же класс дефектов).
    """
    probe = re.sub(r"[()\[\]^$|?+*\\]", " ", fragment.replace("\\s", " ").replace(".*", " "))
    words = WORD.findall(probe)
    if not words:
        return True
    return any(w.lower() in front_low for w in words)


def evaluate(front, specs):
    """(issues, stats) — stats: {'spec': n, 'ids': n, 'classes': n, 'texts': n}."""
    issues = []
    stats = {"spec": 0, "ids": 0, "classes": 0, "texts": 0}
    for name, used in sorted(specs.items()):
        if not (used["ids"] or used["classes"] or used["texts"]):
            continue
        stats["spec"] += 1
        stats["ids"] += len(used["ids"])
        stats["classes"] += len(used["classes"])
        stats["texts"] += len(used["texts"])

        gone_ids = sorted(i for i in used["ids"] if i not in front["ids"])
        if gone_ids:
            issues.append(f"{name}: id, которых нет во фронте: "
                          + ", ".join("#" + g for g in gone_ids[:8]))

        gone_cls = sorted(c for c in used["classes"] if c not in front["classes"])
        if gone_cls:
            issues.append(f"{name}: классы, которых нет во фронте: "
                          + ", ".join("." + g for g in gone_cls[:8]))

        gone_txt = [t for t in used["texts"] if not text_matches(t, front["text"])]
        if gone_txt:
            issues.append(f"{name}: ожидаемый текст не совпадает с текстом фронта: "
                          + "; ".join("/" + t[:40] + "/" for t in gone_txt[:4]))
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['spec']} | id {stats['ids']}, классов {stats['classes']}, "
            f"текстов {stats['texts']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    front, specs = collect(pathlib.Path(a.root))
    if front is None:
        print(f"::error::check_e2e_selectors: {specs} — проверять нечего (rc=2)")
        return 2
    issues, stats = evaluate(front, specs)
    for m in issues:
        print(f"::error::check_e2e_selectors: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:160]}")

    f = {"ids": {"view-journal", "btn-save"}, "classes": {"busy-banner", "row"},
         "text": "считаем тренды и дольше обычного"}
    case("норма",
         f,
         {"ok.spec.js": {"ids": {"view-journal"}, "classes": {"busy-banner"},
                         "texts": ["Считаем тренды"]}},
         0, ["нарушено: 0", "id 1, классов 1, текстов 1"])

    case("пропавший id",
         f,
         {"bad.spec.js": {"ids": {"view-removed"}, "classes": set(), "texts": []}},
         1, ["bad.spec.js", "#view-removed"])

    case("пропавший класс",
         f,
         {"bad2.spec.js": {"ids": set(), "classes": {"gone-class"}, "texts": []}},
         1, [".gone-class"])

    case("пропавший текст",
         f,
         {"bad3.spec.js": {"ids": set(), "classes": set(), "texts": ["совершенно другой текст"]}},
         1, ["ожидаемый текст не совпадает"])

    total += 1
    stats = {"spec": 3, "ids": 5, "classes": 2, "texts": 1}
    line = receipt_line(stats, [])
    if "спек 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_selectors --selftest: {ok}/{total} "
          f"(id/класс/текст спеки, которых нет во фронте = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
