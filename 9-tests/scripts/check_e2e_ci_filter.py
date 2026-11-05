"""check_e2e_ci_filter.py — в CI не «теряются» спеки из-за фильтра названий.

Зачем (реальный случай): в `package.json` стояло
`"test": "playwright test --grep-invert \"смена PIN\""`, и CI гонял `npm test`. То есть
`change-pin.spec.js` **не выполнялся никогда**, а в квитанции прогона этого не было видно —
ни `skipped`, ни слова. Это тот же класс, что «зелёно, потому что перестало смотреть».

Правило: `npm test` не должен содержать `--grep`/`--grep-invert`; исключение сценария делается
**явным `test.skip(условие, причина)`** в самой спеке (тогда оно видно числом `skipped`
в квитанции `e2e_receipt.py`), а включается флагом окружения.

Проверки:
1. в `package.json` скрипт `test` — без `--grep`/`--grep-invert`/`--project`;
2. если спека содержит флаг `XDIARY_*`-skip, этот флаг упомянут в `front-e2e.md` (чтобы был
   виден в таблице «флаг → что включает»).

Коды: `0` — фильтров нет; `1` — есть; `2` — не измерено (нет package.json/маркера).
"""

import argparse
import json
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

FORBIDDEN = ("--grep-invert", "--grep", "--shard", "--project")
FLAG = re.compile(r"XDIARY_[A-Z0-9_]+")


def evaluate(scripts, specs, marker_text, config_text="", guard_text=""):
    """(issues, stats)."""
    issues = []
    test_cmd = scripts.get("test", "")
    hits = [f for f in FORBIDDEN if f in test_cmd]
    if hits:
        issues.append("в `npm test` есть фильтр " + ", ".join(hits) +
                      " — часть спек не выполняется и это не видно в квитанции; "
                      "исключение делайте явным `test.skip(…, причина)`")
    flags = set()
    for name, text in sorted(specs.items()):
        for line in text.splitlines():
            if "test.skip" in line:
                flags.update(FLAG.findall(line))
    undocumented = sorted(f for f in flags if f not in marker_text)
    for f in undocumented:
        issues.append(f"флаг `{f}` управляет пропуском спек, но не описан в `front-e2e.md` "
                      f"(таблица «флаг → что включает»)")
    # Если ВСЕ тесты уходят в skip, Playwright завершится кодом 0 («0 passed, N skipped»).
    # Набор в этом случае не проверил НИЧЕГО, значит прогон не зелёный — за это отвечает
    # репортёр-страж `reporters/executed-guard.js`, и он обязан быть подключён в конфиге.
    # Артефакт `test-results/` уезжает из CI, а лог джобы — нет. Страж обязан писать рядом
    # машиночитаемую квитанцию (`receipt.txt`), иначе числа прогона приходится копировать руками
    # и `e2e_receipt.py` нечем накормить.
    guard = None
    guard_ok = "executed-guard" in config_text
    if config_text and not guard_ok:
        issues.append("в `playwright.config.js` не подключён страж `reporters/executed-guard.js`   "
                      "прогон, где все тесты пропущены, выглядел бы зелёным")
    # Артефакт `test-results/` уезжает из CI, а лог джобы — нет: страж обязан писать рядом
    # машиночитаемую квитанцию, иначе числа прогона приходится копировать руками.
    if guard_text and "receipt.txt" not in guard_text:
        issues.append("страж `reporters/executed-guard.js` не пишет `test-results/receipt.txt` — "
                      "в артефакте не будет машиночитаемых чисел прогона")
    stats = {"scripts": len(scripts), "specs": len(specs), "flags": len(flags), "grep": len(hits), "undocumented": len(undocumented), "guard": 1 if guard_ok else 0}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: скриптов npm {stats['scripts']} | спек {stats['specs']} | "
            f"флагов пропуска {stats['flags']} | фильтров в test: {stats['grep']} | "
            f"не описано в маркере: {stats['undocumented']} | страж исполнения: "
            f"{'есть' if stats.get('guard') else 'НЕТ'} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    pkg, marker, tests_dir = base / "package.json", base / "front-e2e.md", base / "tests"
    if not pkg.exists() or not marker.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_ci_filter: нет package.json, маркера или спек — "
              "не измерено (rc=2)")
        return 2
    try:
        scripts = json.loads(pkg.read_text(encoding="utf-8")).get("scripts", {})
        marker_text = marker.read_text(encoding="utf-8", errors="replace")
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except (OSError, ValueError) as e:
        print(f"::error::check_e2e_ci_filter: не читается вход: {e} — не измерено (rc=2)")
        return 2
    cfg_path = base / "playwright.config.js"
    cfg_text = cfg_path.read_text(encoding="utf-8", errors="replace") if cfg_path.exists() else ""
    guard_path = base / "reporters" / "executed-guard.js"
    guard_text = guard_path.read_text(encoding="utf-8", errors="replace") if guard_path.exists() else ""
    issues, stats = evaluate(scripts, specs, marker_text, cfg_text, guard_text)
    for m in issues:
        print(f"::error::check_e2e_ci_filter: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, scripts, specs, marker, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(scripts, specs, marker)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("чистый test без фильтров",
         {"test": "playwright test"},
         {"a.spec.js": "test.skip(!E, 'нужен XDIARY_BUSY=1');"},
         "| `XDIARY_BUSY=1` | индикатор |", 0, ["фильтров в test: 0", "нарушено: 0"])

    case("grep-фильтр в test",
         {"test": 'playwright test --grep-invert "смена PIN"'},
         {"a.spec.js": "test('x')"}, "маркер", 1,
         ["--grep-invert", "не видно в квитанции"])

    case("флаг не описан в маркере",
         {"test": "playwright test"},
         {"b.spec.js": "test.skip(!E, 'нужен XDIARY_NEWFLAG=1');"},
         "маркер без флага", 1, ["XDIARY_NEWFLAG", "не описан"])

    total += 1
    line = receipt_line({"scripts": 2, "specs": 3, "flags": 1, "grep": 0, "undocumented": 0,
                              "guard": 1}, [])
    if "спек 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")


    # Страж «ноль исполненных = не зелёный»: подключён -> 0, не подключён -> 1.
    for cfg_text, want in (
        ("reporter: [['list'], ['./reporters/executed-guard.js']]", 0),
        ("reporter: [['list']]", 1),
    ):
        total += 1
        issues2, stats2 = evaluate({"test": "playwright test"},
                                   {"a.spec.js": "test('x')"},
                                   "marker", cfg_text)
        rc2 = 1 if issues2 else 0
        guard_expected = 1 if want == 0 else 0
        if rc2 == want and stats2["guard"] == guard_expected:
            ok += 1
        else:
            print(f"  [FAIL] страж исполнения: rc={rc2} (ждали {want}), guard={stats2['guard']}")


    # Страж обязан писать `test-results/receipt.txt` (числа едут в артефакте, а не в логе).
    for guard_text, want in (("const x = 'receipt.txt';", 0), ("const x = '';", 1)):
        total += 1
        issues3, _ = evaluate(
            {"test": "playwright test"},
            {"a.spec.js": "test('x')"},
            "marker",
            "reporter: [['list'], ['./reporters/executed-guard.js']]",
            guard_text,
        )
        rc3 = 1 if issues3 else 0
        if rc3 == want:
            ok += 1
        else:
            print(f"  [FAIL] receipt.txt в страже: rc={rc3} (ждали {want})")

    print(f"check_e2e_ci_filter --selftest: {ok}/{total} (grep в test / неописанный флаг = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
