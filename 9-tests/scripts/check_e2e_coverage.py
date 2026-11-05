"""check_e2e_coverage.py — покрытие экранов фронта спеками `front-e2e` и правдивость маркера.

Зачем: `9-tests/RnD/front-e2e/front-e2e.md` — единственное место, где объявлено «какой экран
какой спекой покрыт» (по нему `tools/check_front_e2e.py` признаёт папку фронтовым E2E). Документ
стареет молча: спек переименовали — маркер врёт; экран добавили — покрытия нет, и никто не заметил.

Проверки:
1. **Маркер ⇄ диск:** каждая спека из таблицы существует; каждая спека на диске упомянута.
2. **Экран ⇄ маркер:** каждый `#view-*`/`#tab-*` из `8-front/index.html` либо объявлен в маркере,
   либо перечислен в исключениях с причиной (ниже); объявленные в маркере экраны существуют.
3. **Экран ⇄ спека:** каждый экран (кроме исключений) реально упоминается хотя бы в одной спеке.

Коды: `0` — расхождений нет; `1` — есть; `2` — не измерено (нет маркера, фронта или спек).

Исключения (осознанные, с причиной):
    view-splash — переходный экран загрузки; проверяется его ИСЧЕЗНОВЕНИЕ в `onboarding.spec.js`,
    а ожидание появления было бы догадкой о времени и флапало бы на быстром стенде.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SCREEN = re.compile(r'id="((?:view|tab)-[\w-]+)"')
ROW = re.compile(r"^\|\s*(.+?)\s*\|\s*(.+?)\s*\|")
BACKTICK = re.compile(r"`([^`]+)`")
SPEC = re.compile(r"([\w-]+\.spec\.js)")

# экран → причина исключения (проверяется иначе, чем «упомянут в спеке»)
EXCEPTIONS = {
    "view-splash": "переходный экран загрузки: проверяется исчезновение (onboarding.spec.js)",
}


def parse_marker(text):
    """({screens}, {specs}) объявленные в маркере.

    Формат таблицы в маркере — «без внешних пайпов»: строка вида
    `` `#view-x` | `x.spec.js` | что проверяется ``, поэтому парсим по внутренним `|`,
    а не по `startswith("|")` (на этом гейт сначала дал 22 ложных «спека не объявлена»).
    """
    screens, specs = set(), set()
    for line in text.splitlines():
        if "|" not in line or ".spec.js" not in line:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2:
            continue
        left, right = cells[0], cells[1]
        if "Экран" in left:
            continue
        for tick in BACKTICK.findall(left):
            if tick.startswith("#"):
                screens.add(tick.lstrip("#"))
        specs.update(SPEC.findall(right))
    return screens, specs


def collect(root: pathlib.Path):
    index = root / "8-front" / "index.html"
    marker = root / "9-tests" / "RnD" / "front-e2e" / "front-e2e.md"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not (index.exists() and marker.exists() and tests_dir.is_dir()):
        return None, "нет index.html, маркера или каталога спек"
    html = index.read_text(encoding="utf-8", errors="replace")
    return {
        "screens": set(SCREEN.findall(html)),
        "marker_screens": None,  # заполняется ниже
        "specs_on_disk": {p.name for p in tests_dir.glob("*.spec.js")},
        "spec_texts": {p.name: p.read_text(encoding="utf-8", errors="replace")
                       for p in sorted(tests_dir.glob("*.spec.js"))},
        "marker_text": marker.read_text(encoding="utf-8", errors="replace"),
    }, None


def evaluate(data):
    """(issues, stats)."""
    issues = []
    marker_screens, marker_specs = parse_marker(data["marker_text"])
    disk = data["specs_on_disk"]

    for s in sorted(marker_specs - disk):
        issues.append(f"маркер ссылается на отсутствующую спеку: {s}")
    for s in sorted(disk - marker_specs):
        issues.append(f"спека не объявлена в маркере front-e2e.md: {s}")

    for scr in sorted(marker_screens - data["screens"]):
        issues.append(f"маркер объявляет несуществующий экран: #{scr}")

    covered = uncovered = 0
    for scr in sorted(data["screens"]):
        hit = any(re.search(r"#?" + re.escape(scr) + r"\b", t) for t in data["spec_texts"].values())
        if scr in EXCEPTIONS:
            covered += 1
            continue
        if hit:
            covered += 1
        else:
            if scr not in marker_screens:
                issues.append(f"экран без спеки и без объявления в маркере: #{scr}")
            else:
                issues.append(f"маркер объявляет покрытие, но ни одна спека не упоминает: #{scr}")
            uncovered += 1

    stats = {"screens": len(data["screens"]), "covered": covered, "uncovered": uncovered,
             "specs": len(disk), "marker_specs": len(marker_specs),
             "exceptions": len(EXCEPTIONS)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: экранов {stats['screens']} | покрыто {stats['covered']} "
            f"(исключений {stats['exceptions']}) | без покрытия {stats['uncovered']} | "
            f"спек {stats['specs']} (в маркере {stats['marker_specs']}) | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    data, err = collect(pathlib.Path(a.root))
    if err:
        print(f"::error::check_e2e_coverage: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(data)
    for m in issues:
        print(f"::error::check_e2e_coverage: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    marker_ok = (
        "| Экран (`8-front/index.html`) | Спека | Что проверяется |\n"
        "|---|---|---|\n"
        "| `#view-splash`, `#view-onboarding` | `onboarding.spec.js` | вход |\n"
        "| `#view-journal` | `wizard-record.spec.js` | записи |\n"
    )

    def case(name, data, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(data)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    base = {
        "screens": {"view-splash", "view-onboarding", "view-journal"},
        "specs_on_disk": {"onboarding.spec.js", "wizard-record.spec.js"},
        "spec_texts": {
            "onboarding.spec.js": "page.locator('#view-onboarding'); page.locator('#view-splash')",
            "wizard-record.spec.js": "page.locator('#view-journal')",
        },
        "marker_text": marker_ok,
    }
    case("норма", base, 0, ["без покрытия 0", "нарушено: 0"])

    no_journal = dict(base)
    no_journal["spec_texts"] = {"onboarding.spec.js": "page.locator('#view-onboarding')"}
    case("экран объявлен, но не упомянут",
         dict(no_journal, screens={"view-splash", "view-onboarding", "view-journal"}),
         1, ["#view-journal"])

    stale_marker = dict(base)
    stale_marker["marker_text"] = marker_ok + "| `#view-removed` | `gone.spec.js` | старьё |\n"
    case("маркер ссылается на отсутствующую спеку и экран",
         stale_marker, 1, ["gone.spec.js", "#view-removed"])

    unannounced = dict(base)
    unannounced["screens"] = {"view-splash", "view-onboarding", "view-journal", "view-new"}
    case("новый экран без спеки и без объявления",
         unannounced, 1, ["#view-new", "без спеки и без объявления"])

    total += 1
    line = receipt_line({"screens": 3, "covered": 3, "uncovered": 0, "specs": 2,
                         "marker_specs": 2, "exceptions": 1}, [])
    if "экранов 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_coverage --selftest: {ok}/{total} "
          f"(непокрытый экран / старый маркер = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
