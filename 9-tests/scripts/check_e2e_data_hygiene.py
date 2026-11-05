"""check_e2e_data_hygiene.py — спека, создающая данные, убирает за собой и делает это уникально.

Зачем (реальный случай): на стенде накопилось 2639 партнёров и 1751 клиент, `#onboarding-list` дорос
до 2607 узлов (588 КБ), `.first()` выбирал произвольную старую карточку — и прогон флапал
«Список партнёров пуст». Прогон, который мусорит, ломает **следующие** прогоны — то есть зелёный
сегодня делает красным завтра, и это неотличимо от дефекта продукта.

Проверки (для спек, которые создают данные):
1. **уборка** — есть `removeByName`/`remove(`/`cleanup`/`finally`, то есть данные удаляются или
   попытка удаления гарантирована;
2. **уникальность** — имя/телефон содержит `Date.now()`, `uniquePhone`, `uniqueName` или
   `Math.random`, иначе на «грязном» стенде коллизия и ложное падение;
3. исключения — только с причиной (ниже), и они печатаются в квитанции.

Коды: `0` — гигиена в порядке; `1` — есть нарушения; `2` — не измерено (нет каталога спек).

Исключения (осознанные, с причиной):
    onboarding.spec.js  — создаёт КЛИЕНТА самим сценарием регистрации; удаления клиента в API нет
                          (`DELETE /clients/{id}` отсутствует), поэтому уборка невозможна by design;
    garmin-somatic.spec.js — создаёт точки соматических серий (POST /somatic-series/points);
                          они привязаны к времени и не влияют на списки UI, удаления нет;
    no-pin.spec.js      — legacy-сценарий под `describe.skip`: тел не выполняется, данные не создаются.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# действия, которыми спека ДОБАВЛЯЕТ сущность (партнёр/запись/позиция каталога)
# `#btn-new` — это «открыть мастер», а не создание: она есть и в спеке, которая ничего не сохраняет
# (офлайн-сценарий). Поэтому в признак создания она не входит — иначе гейт даёт ложный красный.
CREATE = re.compile(r"#btn-(?:add|save|cat|create|onb-profile|onb-phone|onb-otp)"
                    r"|removeByName\(|/partners',\s*'POST'|POST\s+/partners"
                    r"|/journal/records',\s*'POST'|POST\s+/journal/records"
                    r"|/catalog',\s*'POST'")
CLEANUP = re.compile(r"removeByName\(|\.remove\(|cleanup|finally\s*\{|delete\s*\(|/remove'")
UNIQUE = re.compile(r"Date\.now\(\)|uniquePhone|uniqueName|Math\.random")

EXCEPTIONS = {
    "onboarding.spec.js": "создаёт клиента сценарием регистрации; удаления клиента в API нет",
    "garmin-somatic.spec.js": "создаёт точки соматических серий (привязаны ко времени, удаления нет)",
    "no-pin.spec.js": "legacy под describe.skip — тел не выполняется",
    "pin-first-login.spec.js": "создаёт клиента сценарием первого входа; удаления клиента в API нет",
}


def evaluate(specs):
    """(issues, stats). specs — {имя: текст}."""
    issues = []
    stats = {"specs": len(specs), "creating": 0, "cleaning": 0, "exceptions": 0, "violations": 0}
    for name, text in sorted(specs.items()):
        creates = bool(CREATE.search(text))
        cleans = bool(CLEANUP.search(text))
        unique = bool(UNIQUE.search(text))
        if creates:
            stats["creating"] += 1
        if cleans:
            stats["cleaning"] += 1
        if name in EXCEPTIONS:
            if creates:
                stats["exceptions"] += 1
            continue
        if not creates:
            continue
        if not cleans:
            issues.append(f"{name}: создаёт данные, но уборки нет "
                          f"(removeByName/remove(/cleanup/finally) — стенд накапливает мусор")
        if not unique:
            issues.append(f"{name}: создаёт данные без уникального имени "
                          f"(Date.now()/uniquePhone) — на грязном стенде коллизия")
    stats["violations"] = len(issues)
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | создают данные {stats['creating']} | "
            f"с уборкой {stats['cleaning']} | исключений с причиной {stats['exceptions']} | "
            f"нарушено: {len(issues)}")


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
        print("::error::check_e2e_data_hygiene: нет каталога спек — не измерено (rc=2)")
        return 2
    specs, err = read_specs(tests_dir)
    if err:
        print(f"::error::check_e2e_data_hygiene: {err} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_data_hygiene: {m}")
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

    clean = {'ok.spec.js': (
        "const n = 'TE-' + Date.now();\n"
        "await page.click('#btn-add');\n"
        "await removeByName(page, 'partner', n);\n")}
    case("норма: создаёт, убирает, уникально", clean, 0, ["нарушено: 0", "создают данные 1"])

    case("нет уборки",
         {'dirty.spec.js': "const n = 'TE-' + Date.now();\nawait page.click('#btn-save');\n"},
         1, ["dirty.spec.js", "уборки нет"])

    case("нет уникальности",
         {'same.spec.js': "const n = 'TE';\nawait page.click('#btn-save');\nawait removeByName(page,'partner',n);\n"},
         1, ["без уникального имени"])

    case("исключение с причиной не нарушает",
         {'onboarding.spec.js': "await page.click('#btn-onb-phone');\n"}, 0,
         ["исключений с причиной 1", "нарушено: 0"])

    case("спека без создания данных не проверяется",
         {'passive.spec.js': "await expect(page.locator('#x')).toBeVisible();\n"}, 0,
         ["создают данные 0"])

    total += 1
    line = receipt_line({"specs": 3, "creating": 2, "cleaning": 2, "exceptions": 1}, [])
    if "спек 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_data_hygiene --selftest: {ok}/{total} "
          f"(создаёт без уборки / без уникальности = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
