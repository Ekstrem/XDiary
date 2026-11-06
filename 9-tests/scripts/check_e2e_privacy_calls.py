"""check_e2e_privacy_calls.py — в спеках нет вызовов `/privacy/*` (ADR-021, критерий этапа 2).

Критерий @AR (`2026-11-16 02:12`): `git grep -e 'privacy/pin' -e 'privacy/token' -- 9-tests` не должен
находить ни одного `fetch`/`request` в `/privacy/*` — только `skip`-описания и комментарии-ссылки.

Почему это важно: после `FE-042` PIN живёт на устройстве (`window.Pin`), и фронт через серверный
`/privacy/*` не ходит. Спека, которая дёргает эти маршруты, проверяет контракт, которого в продукте
больше нет: зелёный результат такой спеки ничего не говорит о поведении, а красный — не дефект
продукта. Кроме того, состояние «на стенде есть PIN» после этапа 3 не существует как факт (PIN —
свойство устройства), поэтому и проба стенда через `/privacy/token` не нужна.

Гейт смотрит ТОЛЬКО исполняемый код спек (строки, начинающиеся с `//`, игнорируются) и только папку
`tests/`: двойник API (`mock-api.js`) и его контрактный смоук (`mock-smoke.js`) обязаны знать эти
маршруты — это их работа, а не проверка продукта.

Коды: `0` — вызовов нет; `1` — есть вызов `/privacy/*` в спеке; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

CALL = re.compile(r"(request\.(post|get|put|patch|delete)\s*\([^)]*?/privacy/|fetch\([^)]*?/privacy/)", re.I)


def evaluate(specs):
    """(issues, stats)."""
    issues = []
    allowed_mentions = 0
    for name, text in sorted(specs.items()):
        for i, line in enumerate(text.splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("/*"):
                if "/privacy/" in stripped:
                    allowed_mentions += 1
                continue
            if CALL.search(line):
                issues.append(f"{name}:{i}: вызов `/privacy/*` в исполняемом коде — по ADR-021 этап 2 "
                              f"в спеках его быть не должно: {stripped[:90]}")
    stats = {"specs": len(specs), "mentions": allowed_mentions}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | упоминаний в комментариях {stats['mentions']} | "
            f"вызовов `/privacy/*` в коде: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_privacy_calls: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.js"))}
    if not specs:
        print("::error::check_e2e_privacy_calls: нет файлов спек — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_privacy_calls: {m}")
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
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:180]}")

    case("комментарий-ссылка допустима",
         {"a.spec.js": "// было: request.post(API_BASE + '/api/v1/privacy/token', ...)\nexpect(x).toBe(1);"},
         0, ["упоминаний в комментариях 1", "в коде: 0"])

    case("request.post в коде",
         {"b.spec.js": "const r = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin: '' } });"},
         1, ["b.spec.js", "/privacy/*"])

    case("fetch в коде",
         {"c.spec.js": "const r = await fetch(base + '/api/v1/privacy/pin/has');"},
         1, ["c.spec.js"])

    case("обычные вызовы API не мешают",
         {"d.spec.js": "await fetch(base + '/api/v1/clients/me');"},
         0, ["в коде: 0"])

    total += 1
    line = receipt_line({"specs": 3, "mentions": 2}, [])
    if "спек 3" in line and "в коде: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_privacy_calls --selftest: {ok}/{total} "
          f"(вызов /privacy/* в коде спеки = rc=1, комментарий допустим)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
