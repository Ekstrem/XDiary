"""check_e2e_gate_hygiene.py — у каждого гейта должен быть `--selftest`, и гейт должен печатать квитанцию.

Зачем: гейт без самопроверки — это «зелено, потому что ничего не проверялось». Правило проекта: гейт
считается подключённым только после прогона на **дефекте** и на **норме**. Проверка на дефекте живёт
внутри гейта (`--selftest`), поэтому её наличие — минимальное требование, и оно должно быть машинным,
а не «я помню, что писал».

Что проверяется (статически, без запуска гейтов — быстро и без побочных эффектов):

1. в файле есть разбор `--selftest` (`"--selftest"`) и функция/ветка `selftest`;
2. в файле есть печать квитанции (слово `квитанция`) — иначе разбор прогона невозможен;
3. гейт перечислен в агрегаторе `check_e2e_all.py` (дублирует `_orphan_gates`, но здесь — чтобы
   сообщение было про гигиену, а не про сироту).

Коды: `0` — все гейты самопроверяемые и с квитанцией; `1` — есть нарушения; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SELF = "check_e2e_gate_hygiene.py"


def collect(scripts_dir):
    gates = {}
    for p in sorted(scripts_dir.glob("check_e2e_*.py")):
        if p.name == SELF:
            continue
        gates[p.name] = p.read_text(encoding="utf-8", errors="replace")
    agg_path = scripts_dir / "check_e2e_all.py"
    agg = agg_path.read_text(encoding="utf-8", errors="replace") if agg_path.exists() else ""
    return gates, agg


def evaluate(gates, agg):
    """(issues, stats)."""
    issues, with_selftest = [], 0
    for name, text in sorted(gates.items()):
        if '"--selftest"' not in text and "'--selftest'" not in text:
            issues.append(f"{name}: нет разбора `--selftest` — гейт нельзя проверить на дефекте")
        elif not re.search(r"def selftest\(", text):
            issues.append(f"{name}: `--selftest` есть, но функции `selftest()` нет")
        else:
            with_selftest += 1
        if "квитанция" not in text:
            issues.append(f"{name}: не печатает квитанцию — разбор прогона невозможен")
        if agg and name not in agg:
            issues.append(f"{name}: не подключён к `check_e2e_all.py`")
    stats = {"gates": len(gates), "with_selftest": with_selftest}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: гейтов {stats['gates']} | с самопроверкой {stats['with_selftest']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    scripts = pathlib.Path(a.root) / "9-tests" / "scripts"
    if not scripts.is_dir():
        print("::error::check_e2e_gate_hygiene: нет папки 9-tests/scripts — не измерено (rc=2)")
        return 2
    gates, agg = collect(scripts)
    if not gates:
        print("::error::check_e2e_gate_hygiene: не найдено ни одного гейта — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(gates, agg)
    for m in issues:
        print(f"::error::check_e2e_gate_hygiene: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, gates, agg, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(gates, agg)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    good = {"check_e2e_x.py": 'ap.add_argument("--selftest", action="store_true")\n'
                             'def selftest():\n    return 0\n'
                             'print("квитанция: ok")\n'}
    case("правильный гейт — зелёный", good, "check_e2e_x.py", 0,
         ["гейтов 1", "с самопроверкой 1", "нарушено: 0"])

    no_self = {"check_e2e_y.py": 'print("квитанция: nope")\n'}
    case("без --selftest — красный", no_self, "check_e2e_y.py", 1, ["нет разбора `--selftest`"])

    no_receipt = {"check_e2e_z.py": 'ap.add_argument("--selftest")\ndef selftest():\n    return 0\n'}
    case("без квитанции — красный", no_receipt, "check_e2e_z.py", 1, ["не печатает квитанцию"])

    orphan = dict(good)
    case("не подключён к агрегатору — красный", orphan, "check_e2e_all.py", 1, ["не подключён"])

    total += 1
    line = receipt_line({"gates": 4, "with_selftest": 3}, [])
    if "гейтов 4" in line and "с самопроверкой 3" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_gate_hygiene --selftest: {ok}/{total} "
          f"(гейт без `--selftest` или без квитанции = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
