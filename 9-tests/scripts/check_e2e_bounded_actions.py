"""check_e2e_bounded_actions.py — «действующие» клики обязаны иметь явный таймаут.

Почему: три падения в прогонах #766/#774/#775 заняли почти весь бюджет теста на **одном клике** —
`#btn-catalog-delete` (80 и 110 с), `#btn-wizard-save` (80 с), `#btn-import-pick` (90 с). Playwright по
умолчанию ждёт элемент до общего лимита теста, поэтому вместо «кнопка недоступна, вот диагностика» тест
молчал и падал «по времени», а стоящая рядом диагностика (тосты, состояние экранов) не запускалась.

Правило: клик по кнопке, которая меняет данные или запускает сетевую операцию
(`*-save`, `*-apply`, `*-delete`, `*-submit`, `*-create`, `*-register`, `*-finish`), обязан содержать
`timeout:` в том же вызове, и значение не больше `MAX_MS` (по умолчанию 25000). Клики навигации
(`btn-wizard-next`/`prev`, табы) под правило не попадают — они не ходят в API.

Коды: `0` — все действующие клики ограничены; `1` — есть неограниченные; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

ACTION_SUFFIX = r"(save|apply|delete|submit|create|register|finish)"
CLICK = re.compile(r"page\.click\(\s*['\"`]#(btn-[\w-]*" + ACTION_SUFFIX + r")['\"`]")
MAX_MS = 25000


def _call_text(lines, start):
    """Текст вызова начиная со строки start до `);` включительно."""
    chunk = []
    for i in range(start, min(start + 6, len(lines))):
        chunk.append(lines[i])
        if ")" in lines[i]:
            break
    return " ".join(chunk)


def evaluate(specs, max_ms=MAX_MS):
    issues, checked = [], 0
    for name, text in sorted(specs.items()):
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if line.strip().startswith("//"):
                continue
            m = CLICK.search(line)
            if not m:
                continue
            checked += 1
            call = _call_text(lines, i)
            tm = re.search(r"timeout:\s*(\d+)", call)
            if not tm:
                issues.append(f"{name}:{i + 1}: `page.click('#{m.group(1)}')` без `timeout:` — "
                              f"ожидание пойдёт до лимита теста, диагностика не успеет")
            elif int(tm.group(1)) > max_ms:
                issues.append(f"{name}:{i + 1}: `#{m.group(1)}` timeout {tm.group(1)} мс > {max_ms} мс — "
                              f"слишком долго, это снова «молчание до лимита»")
    stats = {"specs": len(specs), "actions": checked}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | действующих кликов {stats['actions']} | "
            f"без ограничения: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_bounded_actions: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_bounded_actions: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    case("клик сохранения с таймаутом — зелёный",
         {"a.spec.js": "await page.click('#btn-wizard-save', { timeout: 20000 });"},
         0, ["действующих кликов 1", "без ограничения: 0"])

    case("клик сохранения без таймаута — красный",
         {"b.spec.js": "await page.click('#btn-partner-save');"},
         1, ["b.spec.js:1", "без `timeout:`"])

    case("многострочный вызов с таймаутом — зелёный",
         {"c.spec.js": "await page.click('#btn-catalog-delete', {\n  timeout: 15000,\n});"},
         0, ["без ограничения: 0"])

    case("слишком большой таймаут — красный",
         {"d.spec.js": "await page.click('#btn-import-apply', { timeout: 90000 });"},
         1, ["90000 мс > 25000 мс"])

    case("навигация не считается",
         {"e.spec.js": "await page.click('#btn-wizard-next');"},
         0, ["действующих кликов 0"])

    total += 1
    line = receipt_line({"specs": 2, "actions": 3}, [])
    if "действующих кликов 3" in line and "без ограничения: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_bounded_actions --selftest: {ok}/{total} "
          f"(мутирующий клик без `timeout:` = rc=1, потому что иначе тест молчит до лимита)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
