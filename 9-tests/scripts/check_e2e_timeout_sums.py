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
# TE-062: спека может объявить СВОЙ бюджет теста (`test.setTimeout(140_000)`), если сумма её
# объявленных ожиданий больше общего лимита. Тогда лимит для неё — этот бюджет, а не общий.
SPEC_BUDGET = re.compile(r"test\.setTimeout\(\s*(\d[\d_]*)\s*\)")
UI_WAIT_REF = re.compile(r"timeout:\s*UI_WAIT\b")
UI_WAIT_DEFAULT = re.compile(r"XDIARY_E2E_UI_WAIT\s*\|\|\s*(\d+)")


def parse_limit(config_text):
    m = re.search(r"timeout:\s*(\d[\d_]*)", config_text)
    return (int(m.group(1).replace("_", "")) / 1000.0) if m else 90.0


def ui_wait_default(api_text, override=None):
    """Сколько «стоит» один `timeout: UI_WAIT` (мс)."""
    if override:
        return override / 1000.0
    m = UI_WAIT_DEFAULT.search(api_text)
    return (int(m.group(1)) / 1000.0) if m else 30.0


def split_tests(text):
    """Разбить файл на блоки по `test(` — сумма ожиданий считается НА ТЕСТ (иначе файл с тремя
    тестами выглядел как один и превышение получалось ложным)."""
    parts, cur = [], []
    for line in text.splitlines():
        if re.match(r"^\s*test\(", line) and cur:
            parts.append("\n".join(cur))
            cur = []
        cur.append(line)
    if cur:
        parts.append("\n".join(cur))
    return parts or [text]


def evaluate(specs, limit_sec, ui_wait=30.0):
    """(issues, stats). `ui_wait` — цена одного `timeout: UI_WAIT` в секундах."""
    per_spec = {}
    over = []
    for name, text in sorted(specs.items()):
        own = SPEC_BUDGET.search(text)
        spec_limit = (int(own.group(1).replace("_", "")) / 1000.0) if own else limit_sec
        worst = 0.0
        for block in split_tests(text):
            vals = [int(v.replace("_", "")) / 1000.0 for v in TIMEOUT.findall(block)]
            vals += [ui_wait] * len(UI_WAIT_REF.findall(block))
            block_total = round(sum(vals), 1)
            worst = max(worst, block_total)
            if block_total > spec_limit:
                over.append((name, block_total, round(block_total / spec_limit, 2)))
        per_spec[name] = worst
    issues = []
    for name, total, ratio in over:
        issues.append(f"{name}: объявленных ожиданий на {total:.0f} с при лимите теста "
                      f"{limit_sec:.0f} с (×{ratio}) — на медленном стенде тест упадёт по общему "
                      f"таймауту, хотя поведение может быть верным")
    stats = {"specs": len(specs), "limit": limit_sec, "over": len(over),
             "max": max(per_spec.values()) if per_spec else 0,
             "max_spec": max(per_spec, key=per_spec.get) if per_spec else "—"}
    return issues, stats


def plan_for_spec(text, limit_sec, target_ratio=0.8):
    """[(было_с, стало_с, сколько_раз)] — минимальный набор сокращений, чтобы уложиться в цель.

    Режем САМЫЕ БОЛЬШИЕ объявленные ожидания первыми и не ниже 10 с: меньше на локальных
    переходах рискованно (может добавить флейк), поэтому下限 — 10 с.
    """
    vals = sorted((int(v.replace("_", "")) / 1000.0 for v in TIMEOUT.findall(text)), reverse=True)
    target = limit_sec * target_ratio
    total = sum(vals)
    changes = []
    for value in vals:
        if total <= target or value <= 10:
            continue
        new_value = 10.0
        saved = value - new_value
        if total - saved < target:
            # частичное сокращение: ровно до цели, с шагом 5 с
            need = total - target
            new_value = max(10.0, value - (int(need) // 5 + 1) * 5)
            saved = value - new_value
        total -= saved
        if saved > 0:
            changes.append((value, new_value, 1))
    return changes, round(total, 1), round(target, 1)


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | лимит теста {stats['limit']:.0f} с | "
            f"превышений {stats['over']} | максимум {stats['max']:.0f} с "
            f"({stats['max_spec']}) | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--plan", action="store_true",
                    help="показать план сокращений ожиданий (ничего не меняет)")
    ap.add_argument("--ui-wait", type=int, default=None,
                    help="считать `timeout: UI_WAIT` как это число (мс), чтобы примерить настройку")
    ap.add_argument("--strict", action="store_true",
                    help="превышение — ошибка (rc=1); без флага — ::warning::")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    cfg, tests_dir, api = base / "playwright.config.js", base / "tests", base / "tests" / "api.js"
    if not cfg.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_timeout_sums: нет конфига или спек — не измерено (rc=2)")
        return 2
    try:
        limit = parse_limit(cfg.read_text(encoding="utf-8", errors="replace"))
        api_text = api.read_text(encoding="utf-8", errors="replace") if api.exists() else ""
        ui_wait = ui_wait_default(api_text, a.ui_wait)
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_timeout_sums: не читается вход: {e} — не измерено (rc=2)")
        return 2
    if a.plan:
        print(f"  лимит теста {limit:.0f} с, цель 80% = {limit * 0.8:.0f} с")
        for name, text in sorted(specs.items()):
            total = round(sum(int(v.replace("_", "")) / 1000.0 for v in TIMEOUT.findall(text))
                          + ui_wait * len(UI_WAIT_REF.findall(text)), 1)
            if total <= limit:
                continue
            changes, after, target = plan_for_spec(text, limit)
            note = "" if after <= target else "  ← ниже 10 с не режем, цель не достигается"
            print(f"  {name}: {total:.0f} с → {after:.0f} с (цель {target:.0f} с){note}")
            for was, now, times in changes:
                print(f"      {was:.0f} с → {now:.0f} с ×{times}")
        return 0

    issues, stats = evaluate(specs, limit, ui_wait)
    marker = "::error::" if a.strict else "::warning::"
    for m in issues:
        print(f"{marker}check_e2e_timeout_sums: {m}")
    print(receipt_line(stats, issues) + f" | UI_WAIT = {ui_wait:.0f} с")
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

    # план сокращений: три по 30 с → цель 72 с ⇒ режем до достижения цели
    total += 1
    changes, after, target = plan_for_spec("{ timeout: 30000 } { timeout: 30000 } { timeout: 30000 }", 90)
    if after <= target and changes and all(now >= 10 for _was, now, _n in changes):
        ok += 1
    else:
        print(f"  [FAIL] план: after={after}, target={target}, changes={changes}")

    # ожидания меньше 10 с не трогаем
    total += 1
    changes2, after2, _t2 = plan_for_spec("{ timeout: 5000 } { timeout: 5000 }", 10)
    if not changes2 and after2 == 10:
        ok += 1
    else:
        print(f"  [FAIL] мелкие ожидания не должны резаться: {changes2}, after={after2}")

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
