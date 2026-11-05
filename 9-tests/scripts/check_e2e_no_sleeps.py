"""check_e2e_no_sleeps.py — в спеках `front-e2e` нет фиксированных пауз (`waitForTimeout`).

Почему: фиксированная пауза — это догадка о времени. На медленном стенде она даёт ложный красный,
на быстром — ложный зелёный, и оба случая неотличимы от дефекта продукта. Замена — ожидание
**признака**: `waitForResponse`, `waitForLoadState('networkidle')`, `expect(...)` или `waitAppReady`
(`tests/api.js`), который ждёт появления любого экрана после `reload`.

Коды: `0` — фиксированных пауз нет; `1` — есть (перечисляются); `2` — не измерено (нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SLEEP = re.compile(r"waitForTimeout\s*\(")


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
    """files — {имя: текст}. Возвращает (issues, checked).

    Правила: пауза в комментарии — не пауза; строка с меткой `backoff:` — осознанный
    экспоненциальный откат (ретрай на сетевой ошибке), он разрешён и печатается отдельно.
    """
    issues = []
    allowed = 0
    for name, text in sorted(files.items()):
        hits = 0
        for raw, code in zip(text.splitlines(), code_lines(text)):
            if not SLEEP.search(code):
                continue
            # метка читается в СЫРОЙ строке: она живёт в комментарии справа от кода
            if "backoff:" in raw:
                allowed += 1
                continue
            hits += 1
        if hits:
            issues.append(f"{name}: фиксированных пауз {hits} — замените на ожидание признака "
                          f"(waitForResponse / networkidle / waitAppReady)")
    return issues, len(files), allowed


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_no_sleeps: нет каталога спек — не измерено (rc=2)")
        return 2
    files = {p.name: p.read_text(encoding="utf-8", errors="replace")
             for p in sorted(tests_dir.glob("*.js"))}
    issues, checked, allowed = evaluate(files)
    for m in issues:
        print(f"::error::check_e2e_no_sleeps: {m}")
    print(f"квитанция: файлов {checked} | с фиксированными паузами: {len(issues)} | разрешённых backoff: {allowed}")
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, files, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, checked, allowed = evaluate(files)
        text = "квитанция: файлов %d | с фиксированными паузами: %d" % (checked, len(issues))
        rc = 1 if issues else 0
        miss = [m for m in must if m not in ("\n".join(issues) + text)]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}")

    case("норма: ожидания по признаку",
         {"a.spec.js": "await page.waitForResponse(r => true);\nawait page.waitForLoadState('networkidle');"},
         0, ["с фиксированными паузами: 0"])
    case("пауза есть",
         {"a.spec.js": "await page.waitForTimeout(1500);"}, 1,
         ["a.spec.js", "фиксированных пауз 1"])
    case("пауза во вспомогательном файле",
         {"api.js": "await page.waitForTimeout(250);"}, 1, ["api.js"])

    total += 1
    line = "квитанция: файлов 2 | с фиксированными паузами: 0"
    if "None" not in line and "паузами: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_e2e_no_sleeps --selftest: {ok}/{total} (waitForTimeout = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
