"""check_e2e_timeout_sums.py — объявленные ожидания спеки помещаются в лимит теста.

Зачем: у теста есть общий лимит (`timeout: 90_000` в `playwright.config.js`). Если спека объявляет
ожидания, сумма которых больше лимита (`{ timeout: 30_000 }` × 5 и т.п.), то на медленном стенде она
упадёт **по общему таймауту**, хотя поведение было верным. Это ложный красный — тот же класс, что
«дефект продукта», и диагностируется он одинаково плохо.

Гейт считает сумму объявленных `timeout: N` по каждой спеке и сравнивает с лимитом теста:
  * `sum > limit` — предупреждение: тест может не уложиться даже при верном поведении;
  * `sum > 2 × limit` — тоже предупреждение, но с явным «перебор в N раз»;
под `--strict` любое превышение — ошибка.

Коды: `0` — превышений нет (или мягкий режим); `1` — есть под `--strict`; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

TIMEOUT = re.compile(r"timeout:\s*(\d[\d_]*)")


def parse_limit(config_text):
    m = re.search(r"timeout:\s*(\d[\d_]*)", config_text)
    return (int(m.group(1).replace("_", "")) / 1000.0) if m else 90.0


def evaluate(specs, limit_sec):
    """(issues, stats)."""
    per_spec = {}
    over = []
    for name, text in sorted(specs.items()):
        vals = [int(v.replace("_", "")) / 1000.0 for v in TIMEOUT.findall(text)]
        total = round(sum(vals), 1)
        per_spec[name] = total
        if total > limit_sec:
            over.append((name, total, round(total / limit_sec, 2)))
    issues = []
    for name, total, ratio in over:
        issues.append(f"{name}: объявленных ожиданий на {total:.0f} с при лимите теста "
                      f"{limit_sec:.0f} с (×{ratio}) — на медленном стенде тест упадёт по общему "
                      f"таймауту, хотя поведение может быть верным")
    stats = {"specs": len(specs), "limit": limit_sec, "over": len(over),
             "max": max(per_spec.values()) if per_spec else 0,
             "max_spec": max(per_spec, key=per_spec.get) if per_spec else "—"}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | лимит теста {stats['limit']:.0f} с | "
            f"превышений {stats['over']} | максимум {stats['max']:.0f} с "
            f"({stats['max_spec']}) | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict", action="store_true",
                    help="превышение — ошибка (rc=1); без флага — ::warning::")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    cfg, tests_dir = base / "playwright.config.js", base / "tests"
    if not cfg.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_timeout_sums: нет конфига или спек — не измерено (rc=2)")
        return 2
    try:
        limit = parse_limit(cfg.read_text(encoding="utf-8", errors="replace"))
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_timeout_sums: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs, limit)
    marker = "::error::" if a.strict else "::warning::"
    for m in issues:
        print(f"{marker}check_e2e_timeout_sums: {m}")
    print(receipt_line(stats, issues))
    return 1 if (issues and a.strict) else 0


def selftest():
    ok = total = 0

    def case(name, specs, limit, strict, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(specs, limit)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if (issues and strict) else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("всё в лимите", {"a.spec.js": "await expect(x).toBeVisible({ timeout: 20000 });"}, 90, True, 0,
         ["превышений 0", "нарушено: 0"])
    case("превышение в мягком режиме",
         {"b.spec.js": "{ timeout: 60000 } { timeout: 60000 }"}, 90, False, 0,
         ["превышений 1", "b.spec.js"])
    case("превышение под --strict",
         {"b.spec.js": "{ timeout: 60000 } { timeout: 60000 }"}, 90, True, 1,
         ["превышений 1"])
    case("граница ровно в лимит",
         {"c.spec.js": "{ timeout: 45000 } { timeout: 45000 }"}, 90, True, 0, ["превышений 0"])

    total += 1
    line = receipt_line({"specs": 2, "limit": 90.0, "over": 0, "max": 45.0, "max_spec": "a.spec.js"}, [])
    if "спек 2" in line and "максимум 45 с" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_timeout_sums --selftest: {ok}/{total} "
          f"(сумма ожиданий больше лимита теста = ::warning::, --strict = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
