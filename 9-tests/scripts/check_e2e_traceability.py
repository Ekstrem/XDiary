"""check_e2e_traceability.py — у каждой спеке `front-e2e` есть ссылка на карточку (правило `9-tests/AGENTS.md`).

Правило: «Тест без критерия приёмки из задачи — не тест: в шапке файла/коммита ссылаемся на `TE-xxx`».
Здесь проверяется машинно: в первых 12 строках спеки должен быть либо `TE-NNN`, либо явная метка
`LEGACY` с причиной (тогда спека исключена из гейта и не притворяется живой).

Коды: `0` — все спеки трассируются; `1` — есть спеки без ссылки и без `LEGACY`; `2` — не измерено
(нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

TE = re.compile(r"TE-\d{3}")
HEAD_LINES = 12


def evaluate(specs):
    """specs — {имя: первые N строк}. Возвращает (issues, traced, legacy)."""
    issues, traced, legacy = [], 0, 0
    for name, head in sorted(specs.items()):
        if TE.search(head):
            traced += 1
        elif re.search(r"LEGACY", head):
            legacy += 1
        else:
            issues.append(f"{name}: в шапке нет ни `TE-NNN`, ни метки `LEGACY` (правило 9-tests/AGENTS.md)")
    return issues, traced, legacy


def load(specs_dir: pathlib.Path):
    if not specs_dir.is_dir():
        return None, "нет каталога спек"
    out = {}
    for p in sorted(specs_dir.glob("*.spec.js")):
        head = "\n".join(p.read_text(encoding="utf-8", errors="replace").splitlines()[:HEAD_LINES])
        out[p.name] = head
    return out, None


def output_lines(issues, traced, legacy, total):
    lines = [f"::error::check_e2e_traceability: {m}" for m in issues]
    lines.append(f"квитанция: спек {total} | с TE-ссылкой: {traced} | LEGACY: {legacy} | "
                 f"без ссылки: {len(issues)}")
    return lines


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    specs, err = load(pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests")
    if err:
        print(f"::error::check_e2e_traceability: {err} — проверять нечего (rc=2)")
        return 2
    issues, traced, legacy = evaluate(specs)
    for line in output_lines(issues, traced, legacy, len(specs)):
        print(line)
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, traced, legacy = evaluate(specs)
        text = "\n".join(output_lines(issues, traced, legacy, len(specs)))
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}")

    case("норма: TE и LEGACY", {"a.spec.js": "// TE-050 критерий", "b.spec.js": "// LEGACY: снят"},
         0, ["с TE-ссылкой: 1", "LEGACY: 1", "без ссылки: 0"])
    case("спека без ссылки", {"a.spec.js": "// без ссылки"}, 1,
         ["a.spec.js", "ни `TE-NNN`, ни метки `LEGACY`"])
    case("TE во второй строке", {"a.spec.js": "// заголовок\n// TE-013\n"}, 0, ["с TE-ссылкой: 1"])

    total += 1
    line = "квитанция: спек 2 | с TE-ссылкой: 2 | LEGACY: 0 | без ссылки: 0"
    if "None" not in line and "без ссылки: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_e2e_traceability --selftest: {ok}/{total} (спека без TE/LEGACY = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
