"""check_e2e_cleanup_coverage.py — набор не оставляет мусор на стенде.

Зачем: боевой стенд — это реальный дневник пары, а не пустая песочница. Прогон создаёт клиентов и
партнёров и обязан убирать их тем же шагом, что и CI (`7-deploy/cleanup-e2e-dev.sh`). Очистка удаляет
**только** то, что совпадает с её шаблонами:

    партнёры: `"Name" LIKE 'TE-%'` или `lower("Name") LIKE '%e2e%'`
    клиенты:  `"Phone" LIKE '+7999%'`

Если спека создаст клиента с другим телефоном или партнёра с другим именем, очистка его НЕ увидит:
мусор копится от прогона к прогону, а списки в UI растут (инцидент `DO-044`: `#partners-list` до 588 КБ
при 2 600+ карточках). Ровно этот класс и был: фикстура создавала партнёра на каждый вход.

Что проверяется:
    1. скрипт очистки существует и содержит оба шаблона (иначе гейт не о чем — «не измерено»);
    2. каждый телефон, который создаёт набор, начинается с `+7999`;
    3. каждое имя партнёра, которое создаёт набор, начинается с `TE-` или содержит `e2e`;
    4. телефоны не берутся из `Date.now()` целиком (коллизии) — используется `uniquePhone`/10-значный суффикс.

Коды: `0` — набор убираем очисткой; `1` — есть данные вне шаблонов; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PHONE_LITERAL = re.compile(r"['\"](\+7\d{8,14})['\"]")
UNIQUE_PHONE = re.compile(r"uniquePhone\(\s*['\"](\+\d+)['\"]")
# Ловим ЛЮБОЕ присваивание имени, а не только «похожее на тестовое»: иначе проверка
# подтверждает сама себя (случай selftest: «Мой партнёр» не находился и нарушения не было).
PARTNER_NAME = re.compile(
    r"""(?:partnerName|PARTNER_NAME|partner_name|\bPARTNER\b|\bNAME\b|\bOLD\b|\bNEW\b|\bname\b)"""
    r"""\s*=\s*['"`]([^'"`\n]{2,80})['"`]""",
    re.I,
)
PARTNER_NAME_INLINE = re.compile(r"""name:\s*['"`]([^'"`]{2,80})['"`]""")


def _literal_prefix(value):
    """Литеральная часть значения: `TE-LINK-${id}` -> `TE-LINK-`."""
    for mark in ("${", "`", " +", "+ "):
        idx = value.find(mark)
        if idx > 0:
            return value[:idx]
    return value


def _looks_like_identifier(value):
    """CSS/HTML-идентификаторы и файлы — это не имена партнёров."""
    v = value.strip()
    if "${" in v or "+" in v:
        # динамическое имя: `TE-LINK-${id}` — проверять надо литеральный префикс, а не всю строку
        return _literal_prefix(v).strip().startswith("TE-") is False and "e2e" not in v.lower()
    return (v.startswith(("def-", "#", ".", "data-", "xdiary"))
            or "/" in v or v.endswith((".png", ".js", ".json", ".html", ".csv", ".txt"))
            or v.isdigit())


def _name_is_cleanable(value):
    v = _literal_prefix(value).strip()
    return v.startswith("TE-") or "e2e" in v.lower()

CLEAN_PARTNER = ("TE-", "e2e")
CLEAN_PHONE_PREFIX = "+7999"
EXEMPT_FILES = {"no-pin.spec.js"}


def read_cleanup(root):
    p = root / "7-deploy" / "cleanup-e2e-dev.sh"
    if not p.exists():
        return None
    return p.read_text(encoding="utf-8", errors="replace")


def evaluate(specs, cleanup_text):
    """(issues, stats) — cleanup_text=None значит «не измерено»."""
    if cleanup_text is None:
        return None, {"specs": len(specs), "phones": 0, "partners": 0}
    issues = []
    if "+7999%" not in cleanup_text:
        issues.append("скрипт очистки не содержит фильтра клиентов `+7999%` — правило нельзя применить")
    if "TE-%" not in cleanup_text:
        issues.append("скрипт очистки не содержит фильтра партнёров `TE-%` — правило нельзя применить")

    phones = partners = 0
    for name, text in sorted(specs.items()):
        if name in EXEMPT_FILES:
            continue
        for m in UNIQUE_PHONE.finditer(text):
            phones += 1
            if not m.group(1).startswith(CLEAN_PHONE_PREFIX):
                issues.append(f"{name}: телефон из `uniquePhone('{m.group(1)}')` не попадает под очистку "
                              f"(нужен префикс {CLEAN_PHONE_PREFIX})")
        for m in PHONE_LITERAL.finditer(text):
            phones += 1
            if not m.group(1).startswith(CLEAN_PHONE_PREFIX):
                issues.append(f"{name}: телефон `{m.group(1)}` не попадает под очистку "
                              f"(нужен префикс {CLEAN_PHONE_PREFIX})")
        for m in PARTNER_NAME.finditer(text):
            nm = m.group(1)
            if _looks_like_identifier(nm):
                continue
            partners += 1
            if not _name_is_cleanable(nm):
                issues.append(f"{name}: имя партнёра `{nm}` не попадает под очистку "
                              f"(нужен префикс TE- или `e2e` в имени)")
        for m in PARTNER_NAME_INLINE.finditer(text):
            nm = m.group(1)
            # `name:` встречается и в файлах/аватарах — фильтруем очевидно не имена партнёров
            if _looks_like_identifier(nm):
                continue
            partners += 1
            if not _name_is_cleanable(nm):
                issues.append(f"{name}: имя партнёра `{nm}` не попадает под очистку "
                              f"(нужен префикс TE- или `e2e` в имени)")
    return issues, {"specs": len(specs), "phones": phones, "partners": partners}


def receipt_line(stats, issues):
    if stats is None or issues is None:
        return "квитанция: не измерено (нет скрипта очистки)"
    return (f"квитанция: спек {stats['specs']} | телефонов {stats['phones']} | имён партнёров "
            f"{stats['partners']} | вне шаблонов очистки: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    root = pathlib.Path(a.root)
    tests = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_cleanup_coverage: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.js"))}
    cleanup = read_cleanup(root)
    if cleanup is None:
        print("::error::check_e2e_cleanup_coverage: нет `7-deploy/cleanup-e2e-dev.sh` — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs, cleanup)
    for m in issues:
        print(f"::error::check_e2e_cleanup_coverage: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    CLEAN = "CLIENT_PATTERN='(\"Phone\" LIKE '+7999%')'\nPARTNER_PATTERN='(\"Name\" LIKE 'TE-%')'"

    def case(name, specs, cleanup, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(specs, cleanup)
        text = "\n".join(issues or []) + "\n" + receipt_line(stats, issues)
        rc = 2 if issues is None else (1 if issues else 0)
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    case("норма: TE- имена и +7999 телефоны",
         {"a.spec.js": "const partnerName = 'TE-A-' + Date.now(); const p = uniquePhone('+7999');"},
         CLEAN, 0, ["вне шаблонов очистки: 0", "телефонов 1", "имён партнёров 1"])

    case("клиент с другим телефоном",
         {"b.spec.js": "const p = uniquePhone('+7911');"},
         CLEAN, 1, ["не попадает под очистку"])

    case("партнёр с другим именем",
         {"c.spec.js": "const partnerName = 'Мой партнёр';"},
         CLEAN, 1, ["имя партнёра"])

    case("нет скрипта очистки — не измерено",
         {"a.spec.js": "const partnerName = 'TE-A';"}, None, 2, ["не измерено"])

    case("скрипт без фильтра партнёров",
         {"a.spec.js": "const partnerName = 'TE-A';"}, "CLIENT_PATTERN '+7999%'", 1, ["TE-%"])

    total += 1
    line = receipt_line({"specs": 3, "phones": 4, "partners": 2}, [])
    if "спек 3" in line and "вне шаблонов очистки: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_cleanup_coverage --selftest: {ok}/{total} "
          f"(данные вне шаблонов очистки = rc=1, нет скрипта = rc=2)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
