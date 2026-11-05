"""check_e2e_diagnostics.py — каждая спека оставляет след для разбора упавшего прогона.

Зачем: когда тест падает в CI, у дежурного есть только лог и артефакты. Если спека ничего не
печатает (ни `reportStep(page, …)`, ни `console.log`, ни аннотации `test.info().annotations`),
разбор начинается с нуля: непонятно, на каком шаге и с каким ответом API всё сломалось.

Проверка: в каждой спеке должен быть хотя бы один диагностический вызов:
    reportStep(              — печатает тост/ошибку экрана (наша общая функция из `api.js`)
    console.log(             — обычный лог
    .annotations.push(       — аннотации Playwright (видны в отчёте)
    .attach(                 — вложение (скриншот/лог) в отчёт

LEGACY-спеки (`describe.skip`, помечены словом LEGACY в шапке) исключены: они не исполняются.

Коды: `0` — все спеки с диагностикой; `1` — есть «молчащие»; `2` — не измерено.
"""

import argparse
import pathlib
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

MARKERS = ("reportStep(", "console.log(", ".annotations.push(", ".attach(")


def has_diagnostics(text):
    return any(m in text for m in MARKERS)


def is_legacy(text):
    head = "\n".join(text.splitlines()[:12])
    return "LEGACY" in head or "describe.skip" in text


def evaluate(specs):
    """(issues, stats)."""
    issues, silent, legacy = [], [], 0
    for name, text in sorted(specs.items()):
        if is_legacy(text):
            legacy += 1
            continue
        if not has_diagnostics(text):
            silent.append(name)
            issues.append(f"{name}: нет ни `reportStep`, ни `console.log`, ни аннотаций — "
                          f"падение в CI будет не с чем разбирать")
    stats = {"specs": len(specs), "legacy": legacy, "silent": len(silent), "ok": len(specs) - legacy - len(silent)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | с диагностикой {stats['ok']} | "
            f"legacy (исключено) {stats['legacy']} | молчащих {stats['silent']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_diagnostics: нет папки спек — не измерено (rc=2)")
        return 2
    try:
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_diagnostics: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_diagnostics: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("спека с reportStep", {"a.spec.js": "await reportStep(page, 'a');"}, 0,
         ["с диагностикой 1", "нарушено: 0"])
    case("спека с console.log", {"a.spec.js": "console.log('x');"}, 0, ["нарушено: 0"])
    case("молчащая спека", {"b.spec.js": "test('x', async () => {});"}, 1,
         ["b.spec.js", "не с чем разбирать"])
    case("LEGACY исключена", {"c.spec.js": "// LEGACY: убранный контракт\ntest.describe.skip('x', () => {});"}, 0,
         ["legacy (исключено) 1"])
    case("аннотации считаются", {"d.spec.js": "test.info().annotations.push({});"}, 0, ["нарушено: 0"])

    total += 1
    line = receipt_line({"specs": 3, "legacy": 1, "silent": 0, "ok": 2}, [])
    if "спек 3" in line and "молчащих 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_diagnostics --selftest: {ok}/{total} "
          f"(молчащая спека = rc=1, LEGACY исключается)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
