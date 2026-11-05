"""check_e2e_skips.py — каждый `skip` объяснён, и видно, что НЕ выполняется в обычном прогоне.

Зачем: `test.skip(...)` — единственный способ, которым спека может «не выполниться, но прогон
зелёный». Два класса дефекта:
  1. **skip без причины** — «тихий зелёный», по квитанции не видно, что проверка не шла;
  2. **skip по флагу окружения** — легально, но если флаг не выставлен в CI, спек не выполняется
     никогда, и покрытие в документе врёт.

Гейт проверяет: у каждого `test.skip`/`describe.skip` есть строка-причина; печатает список
пропусков и флагов, которыми они управляются, чтобы «зелёно» нельзя было прочитать как «всё
проверено».

Коды: `0` — все skip объяснены; `1` — есть skip без причины; `2` — не измерено (нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SKIP = re.compile(r"(?:test|describe)\.skip\s*\(")
REASON = re.compile(r"""['"`][^'"`]{8,}['"`]""")  # строка-причина длиной от 8 символов
ENV_FLAG = re.compile(r"XDIARY_[A-Z0-9_]+")


def evaluate(specs):
    """(issues, stats). specs — {имя: текст}."""
    issues = []
    flags = set()
    skips = []
    for name, text in sorted(specs.items()):
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if not SKIP.search(line):
                continue
            # причина может быть на этой же строке или на следующих двух (многострочный вызов)
            window = "\n".join(lines[i:i + 3])
            flags.update(ENV_FLAG.findall(window))
            if not REASON.search(window):
                issues.append(f"{name}:{i + 1}: skip без причины "
                              f"(нужна строка вида 'pending:TE-xxx — …')")
            else:
                skips.append(f"{name}:{i + 1}")
    stats = {"specs": len(specs), "skips": len(skips), "flags": sorted(flags)}
    return issues, stats


def receipt_line(stats, issues):
    tail = (" | флаги: " + ", ".join(stats["flags"])) if stats["flags"] else ""
    return (f"квитанция: спек {stats['specs']} | skip-маркеров {stats['skips']} | "
            f"без причины: {len(issues)}{tail}")


def read_specs(tests_dir):
    """({имя: текст}, None) либо (None, причина). Ошибка чтения — «не измерено», а не traceback."""
    specs = {}
    try:
        files = sorted(tests_dir.glob("*.spec.js"))
        for p in files:
            specs[p.name] = p.read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        return None, f"не читается {getattr(e, 'filename', tests_dir)}: {e}"
    if not specs:
        return None, "каталог спек пуст"
    return specs, None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests_dir.is_dir():
        print("::error::check_e2e_skips: нет каталога спек — не измерено (rc=2)")
        return 2
    specs, err = read_specs(tests_dir)
    if err:
        print(f"::error::check_e2e_skips: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_skips: {m}")
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

    case("skip с причиной",
         {"a.spec.js": "test.skip(!E, 'pending:TE-059 — включается XDIARY_BUSY=1');"}, 0,
         ["skip-маркеров 1", "флаги: XDIARY_BUSY", "без причины: 0"])

    case("skip без причины",
         {"b.spec.js": "test.skip(!E);"}, 1, ["skip без причины"])

    case("многострочный skip с причиной",
         {"c.spec.js": "test.skip(\n  !E,\n  'pending:FE-READPATH — ждём двух состояний'\n);"}, 0,
         ["без причины: 0"])

    case("describe.skip с причиной",
         {"d.spec.js": "test.describe.skip('legacy: PIN больше не выдаёт токен', () => {});"}, 0,
         ["skip-маркеров 1"])

    case("нет skip — чисто",
         {"e.spec.js": "test('x', async () => {});"}, 0, ["skip-маркеров 0"])

    total += 1
    line = receipt_line({"specs": 2, "skips": 1, "flags": ["XDIARY_BUSY"]}, [])
    if "спек 2" in line and "без причины: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_skips --selftest: {ok}/{total} (skip без причины = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
