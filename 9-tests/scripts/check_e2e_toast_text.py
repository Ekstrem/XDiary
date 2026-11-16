"""check_e2e_toast_text.py — если спека смотрит на `#toast`, она обязана утверждать ТЕКСТ, а не только видимость.

Почему: `TE-109` (находка @AR 2026-11-13 02:20) — тост с текстом причины не проверялся
ни одной спекой: упоминаний `#toast` десять, утверждений (`expect(...)`) — ноль. Видимость
без текста — утверждение, которое проходит и на пустом тосте, то есть «зелёное на пустоте».

Коды: `0` — нарушений нет; `1` — есть (перечисляются); `2` — не измерено (нет каталога спек).

Правила:
- комментарии не считаются (строка `// ...` — не проверка);
- `toHaveText` / `toContainText` / `toHaveValue` — утверждение текста (ок);
- `toBeVisible` / `toBeHidden` / `toHaveCount` без утверждения текста — дефект (видимость ≠ причина);
- `#toast` в коде без единого утверждения — тоже дефект (смотрим и ничего не утверждаем).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

TOAST = re.compile(r"#toast\b")
VISIBILITY = re.compile(r"toBeVisible|toBeHidden|toHaveCount|waitFor\s*\(")
TEXT = re.compile(r"toHaveText|toContainText|toHaveValue|hasText")

DEFAULT_DIR = "9-tests/RnD/front-e2e/tests"


def code_lines(text):
    """Строки кода без `//`-комментариев (кавычки учитываются, чтобы не резать URL)."""
    for raw in text.splitlines():
        out, quote = [], None
        i = 0
        while i < len(raw):
            ch = raw[i]
            if quote:
                out.append(ch)
                if ch == quote and (i == 0 or raw[i - 1] != "\\"):
                    quote = None
            elif ch in "\"'`":
                quote = ch
                out.append(ch)
            elif ch == "/" and raw[i:i + 2] == "//":
                break
            else:
                out.append(ch)
            i += 1
        yield "".join(out)


def evaluate(files):
    """files — {имя: текст}. Возвращает (issues, checked)."""
    issues, checked = [], 0
    for name, text in sorted(files.items()):
        code = "\n".join(code_lines(text))
        if not TOAST.search(code):
            continue
        checked += 1
        has_text = bool(TEXT.search(code))
        has_vis = bool(VISIBILITY.search(code))
        if not has_text and has_vis:
            issues.append(name + ": проверяет #toast БЕЗ текста — видимость проходит и на пустом тосте")
        elif not has_text and not has_vis:
            issues.append(name + ": упоминает #toast, но не утверждает ничего (ни видимость, ни текст)")
    return issues, checked


def selftest():
    ok = total = 0

    def case(name, files, want_issues):
        nonlocal ok, total
        total += 1
        issues, _ = evaluate(files)
        if len(issues) == want_issues:
            ok += 1
        else:
            print("  [FAIL] %s: ждали %d нарушений, получили %d (%s)"
                  % (name, want_issues, len(issues), issues[:2]))

    case("дефект: только видимость", {"a.spec.js": "await expect(page.locator('#toast')).toBeVisible();\n"}, 1)
    case("норма: видимость + текст",
         {"a.spec.js": "await expect(page.locator('#toast')).toBeVisible();\n"
                       "await expect(page.locator('#toast')).toHaveText(/Укажите имя/);\n"}, 0)
    case("норма: текст через hasText-фильтр",
         {"a.spec.js": "const s = page.locator('#toast, #view-wizard').filter({ hasText: /ошибк/i });\n"
                       "await expect(s.first()).toBeVisible();\n"}, 0)
    case("норма: только текст (видимость не нужна)",
         {"a.spec.js": "await expect(page.locator('#toast')).toContainText(/Ошибка/);\n"}, 0)
    case("норма: тост только в комментарии",
         {"a.spec.js": "// #toast смотрим глазами\nawait page.click('#save');\n"}, 0)
    case("дефект: упомянут без утверждений",
         {"a.spec.js": "const t = page.locator('#toast');\nconsole.log(await t.innerText());\n"}, 1)
    case("норма: спека без тоста", {"a.spec.js": "await page.click('#x');\n"}, 0)
    case("дефект: два файла — один без текста, второй норма",
         {"a.spec.js": "await expect(page.locator('#toast')).toBeVisible();\n",
          "b.spec.js": "await expect(page.locator('#toast')).toHaveText(/Укажите/);\n"}, 1)

    print("check_e2e_toast_text --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=DEFAULT_DIR, help="каталог спек front-e2e")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = pathlib.Path(a.dir)
    if not root.is_dir():
        print("::error::check_e2e_toast_text: нет каталога %s — не измерено (rc=2)" % a.dir)
        return 2

    files = {p.name: p.read_text(encoding="utf-8", errors="replace")
             for p in sorted(root.glob("*.spec.js"))}
    issues, checked = evaluate(files)
    total = len(files)
    for i in issues:
        print("::error::check_e2e_toast_text: " + i)
    print("квитанция: спек %d | смотрят на #toast %d | нарушений %d" % (total, checked, len(issues)))
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
