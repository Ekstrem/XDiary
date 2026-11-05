"""check_e2e_ui_coverage.py — какие элементы управления фронта E2E вообще не трогает.

Зачем: покрытие экранов (`check_e2e_coverage`) отвечает «экран открыт», но не «действие проверено».
Кнопка с обработчиком, которую не нажимает ни одна спека, — это непроверенный путь: она может быть
сломана, и ни один прогон об этом не скажет. Гейт строит карту «обработчик во фронте ⇄ упоминание
в спеке» и печатает непокрытые.

Важность: у критичных для MVP элементов (`MVP_CRITICAL`) непокрытие — **предупреждение** по умолчанию
и **ошибка** под `--strict`. Остальные печатаются только списком: это карта, а не запрет.

Коды: `0` — критичные покрыты (или режим предупреждения); `1` — под `--strict` есть непокрытые
критичные; `2` — не измерено (нет фронта или спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

BIND = re.compile(r'\$\("([\w-]+)"\)\.(?:addEventListener|onclick)')
BIND_HTML = re.compile(r'id="([\w-]+)"[^>]*onclick=')
TOUCH = re.compile(r"\.(?:click|press|fill|type|selectOption|check|uncheck)\(\s*'#([\w-]+)")
TOUCH_LOC = re.compile(r"locator\(\s*'#([\w-]+)")

# элементы, без которых MVP-сценарий неполон (негативные пути, удаление, выбор пола и т.п.)
MVP_CRITICAL = {
    "confirm-no": "отмена подтверждения удаления",
    "btn-partner-delete": "удаление партнёра",
    "btn-catalog-delete": "удаление позиции каталога",
    "btn-import": "импорт CSV",
    "onb-gender-female": "второй вариант пола в онбординге",
    "btn-wizard-prev": "возврат на шаг назад в мастере",
}


def collect(root: pathlib.Path):
    js_dir = root / "8-front" / "js"
    index = root / "8-front" / "index.html"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not tests_dir.is_dir():
        return None, "нет 8-front/index.html или каталога спек"
    js = ""
    if js_dir.is_dir():
        for p in sorted(js_dir.glob("*.js")):
            js += "\n" + p.read_text(encoding="utf-8", errors="replace")
    html = index.read_text(encoding="utf-8", errors="replace")
    bound = set(BIND.findall(js)) | set(BIND_HTML.findall(html))
    touched = set()
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            t = p.read_text(encoding="utf-8", errors="replace")
            touched |= set(TOUCH.findall(t)) | set(TOUCH_LOC.findall(t))
    except OSError as e:
        return None, f"не читается спека: {e}"
    return {"bound": bound, "touched": touched}, None


def evaluate(data, strict):
    """(issues, stats)."""
    bound, touched = data["bound"], data["touched"]
    uncovered = sorted(bound - touched)
    critical_gaps = [i for i in uncovered if i in MVP_CRITICAL]
    total = len(bound)
    covered = len(bound & touched)
    stats = {"bound": total, "covered": covered, "uncovered": len(uncovered),
             "critical": len(critical_gaps), "list": uncovered}
    issues = []
    for ident in critical_gaps:
        msg = (f"критичный для MVP элемент `#{ident}` ({MVP_CRITICAL[ident]}) "
               f"не трогает ни одна спека")
        issues.append(msg)
    return issues, stats, critical_gaps


def receipt_line(stats, issues):
    return (f"квитанция: элементов с обработчиком {stats['bound']} | трогают спеки {stats['covered']} | "
            f"не покрыто {stats['uncovered']} | критичных без покрытия {stats['critical']} | "
            f"нарушено: {0 if not issues else len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict", action="store_true",
                    help="непокрытый критичный элемент — ошибка (rc=1), иначе ::warning::")
    ap.add_argument("--list", action="store_true", help="напечатать все непокрытые id")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    data, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_ui_coverage: {err} — не измерено (rc=2)")
        return 2
    issues, stats, critical_gaps = evaluate(data, a.strict)
    marker = "::error::" if a.strict else "::warning::"
    for m in issues:
        print(f"{marker}check_e2e_ui_coverage: {m}")
    if a.list:
        for ident in stats["list"]:
            mark = " (MVP)" if ident in MVP_CRITICAL else ""
            print(f"  не покрыто: #{ident}{mark}")
    print(receipt_line(stats, issues))
    if a.strict and critical_gaps:
        return 1
    return 0


def selftest():
    ok = total = 0

    def case(name, data, strict, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats, _gap = evaluate(data, strict)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if (strict and any("критичный" in i for i in issues)) else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    full = {"bound": {"btn-save", "confirm-no"}, "touched": {"btn-save", "confirm-no"}}
    case("всё покрыто", full, True, 0, ["не покрыто 0", "нарушено: 0"])

    gap = {"bound": {"btn-save", "confirm-no"}, "touched": {"btn-save"}}
    case("критичный не покрыт (мягкий режим)", gap, False, 0, ["критичный", "confirm-no"])
    case("критичный не покрыт (строгий режим)", gap, True, 1, ["критичный"])

    other = {"bound": {"btn-save", "btn-unknown"}, "touched": {"btn-save"}}
    case("некритичный не покрыт — не ошибка", other, True, 0, ["не покрыто 1", "нарушено: 0"])

    total += 1
    line = receipt_line({"bound": 10, "covered": 7, "uncovered": 3, "critical": 0, "list": []}, [])
    if "элементов с обработчиком 10" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_ui_coverage --selftest: {ok}/{total} "
          f"(непокрытый критичный элемент = ::warning::, --strict = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
