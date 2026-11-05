"""check_e2e_error_paths.py — негативные пути (элементы ошибок) не остаются непроверенными.

Зачем: «счастливый путь» покрыт легко, а сообщения об ошибках (`#onb-phone-error`, `#pin-error`,
`#import-result`…) — нет. Непроверенная ошибка означает: пользователь может не увидеть причину
отказа (или увидит её на чужом экране), и ни один прогон об этом не скажет.

Гейт находит во фронте элементы с `-error`/`-err`/`-fail`/`-warn` в id и требует, чтобы каждый
либо **упоминался в спеке** (то есть утверждался), либо был в списке исключений с причиной.

Коды: `0` — все негативные элементы проверяются; `1` — есть непроверенные (под `--strict`);
`2` — не измерено (нет фронта или спек).

Исключения (осознанные):
    onb-link-error — сообщение LEGACY-пути привязки (FE-007), на 0.90+ не показывается:
                     шаг привязки удалён (ADR-021), проверять нечего.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

ERROR_ID = re.compile(r'id="([\w-]*(?:-error|-err|-fail|-warn)[\w-]*)"')

EXCEPTIONS = {
    "onb-link-error": "сообщение LEGACY-пути привязки (FE-007); на 0.90+ шаг удалён (ADR-021)",
}


def evaluate(html, js, specs):
    """(issues, stats)."""
    ids = set(ERROR_ID.findall(html)) | set(ERROR_ID.findall(js))
    spec_text = "\n".join(specs.values())
    checked, uncovered = 0, []
    for ident in sorted(ids):
        if ident in EXCEPTIONS:
            checked += 1
            continue
        if ("#" + ident) in spec_text:
            checked += 1
        else:
            uncovered.append(ident)
    issues = [f"элемент ошибки `#{i}` не утверждается ни одной спекой — "
              f"пользователь может не увидеть причину отказа" for i in uncovered]
    stats = {"errors": len(ids), "checked": checked, "uncovered": len(uncovered),
             "exceptions": len(EXCEPTIONS), "list": uncovered}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: элементов ошибок {stats['errors']} | проверяются {stats['checked']} | "
            f"исключений {stats['exceptions']} | не проверяются {stats['uncovered']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict", action="store_true",
                    help="непроверенный элемент ошибки — ошибка (rc=1), иначе ::warning::")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = pathlib.Path(a.root)
    index = root / "8-front" / "index.html"
    js_dir = root / "8-front" / "js"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_error_paths: нет фронта или спек — не измерено (rc=2)")
        return 2
    try:
        html = index.read_text(encoding="utf-8", errors="replace")
        js = ""
        if js_dir.is_dir():
            for p in sorted(js_dir.glob("*.js")):
                js += "\n" + p.read_text(encoding="utf-8", errors="replace")
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_error_paths: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(html, js, specs)
    marker = "::error::" if a.strict else "::warning::"
    for m in issues:
        print(f"{marker}check_e2e_error_paths: {m}")
    print(receipt_line(stats, issues))
    return 1 if (issues and a.strict) else 0


def selftest():
    ok = total = 0

    def case(name, html, js, specs, strict, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(html, js, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if (issues and strict) else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    html = '<p id="pin-error"></p><p id="onb-link-error"></p>'
    case("ошибка утверждается",
         html, "", {"a.spec.js": "await expect(page.locator('#pin-error')).toBeVisible();"},
         True, 0, ["проверяются 2", "нарушено: 0"])

    case("ошибка не утверждается (мягкий режим)",
         '<p id="pin-error"></p><p id="import-error"></p>', "",
         {"a.spec.js": "await expect(page.locator('#pin-error')).toBeVisible();"},
         False, 0, ["#import-error", "нарушено: 1"])

    case("ошибка не утверждается (строгий режим)",
         '<p id="pin-error"></p><p id="import-error"></p>', "",
         {"a.spec.js": "await expect(page.locator('#pin-error')).toBeVisible();"},
         True, 1, ["#import-error"])

    total += 1
    line = receipt_line({"errors": 3, "checked": 3, "uncovered": 0, "exceptions": 1, "list": []}, [])
    if "элементов ошибок 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_error_paths --selftest: {ok}/{total} "
          f"(непроверенный элемент ошибки = ::warning::, --strict = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
