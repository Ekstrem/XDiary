"""check_e2e_fixture_contract.py — контракт тестовой обвязки `front-e2e`.

Зачем: спека импортирует из `tests/api.js` по именам (`const { X } = require('./api')`). Если имя
переименовали или забыли добавить в `module.exports`, спека падает **в браузере** на `X is not a
function` — и это выглядит как дефект продукта, хотя сломан тест. Ровно так я сам чуть не отправил
`waitAppReady` без импорта (спасла ручная сверка). Здесь это проверяется машинно, без браузера.

Проверки:
1. каждое имя из `require('./api')` в спеке есть в `module.exports` файла `tests/api.js`;
2. спека, вызывающая `test(`, импортирует `test` из `@playwright/test` (и `expect`, если вызывает `expect(`);
3. информационно: экспорты, которые не использует **ни одна** спека (кандидаты в мёртвый код).

Коды: `0` — контракт в порядке; `1` — есть нарушения; `2` — не измерено (нет `api.js` или спек).
"""

import argparse
import json
import re
import pathlib
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

REQ_API = re.compile(r"const\s*\{([^}]*)\}\s*=\s*require\('\./api'\)")
REQ_PW = re.compile(r"const\s*\{([^}]*)\}\s*=\s*require\('@playwright/test'\)")
EXPORTS = re.compile(r"module\.exports\s*=\s*\{([^}]*)\}", re.S)


def names_of(group):
    return [x.strip() for x in group.replace("\n", " ").split(",") if x.strip()]


def evaluate(api_text, specs):
    """(issues, stats). specs — {имя: текст}."""
    m = EXPORTS.search(api_text)
    exports = set(names_of(m.group(1))) if m else set()
    issues = []
    stats = {"specs": len(specs), "exports": len(exports), "imports": 0, "unused_exports": 0}
    referenced = set()

    for name, text in sorted(specs.items()):
        for ident in re.findall(r"\b(\w+)\s*\(", text):
            referenced.add(ident)
        for ident in re.findall(r"\b([A-Z][A-Z_]+)\b", text):
            referenced.add(ident)

        api_import = REQ_API.search(text)
        if api_import:
            imported = names_of(api_import.group(1))
            stats["imports"] += 1
            missing = [n for n in imported if n not in exports]
            if missing:
                issues.append(f"{name}: импортирует из api.js то, чего нет в module.exports: "
                              + ", ".join(missing))

        pw = REQ_PW.search(text)
        pw_names = set(names_of(pw.group(1))) if pw else set()
        if re.search(r"^\s*test\(", text, re.M) and "test" not in pw_names:
            issues.append(f"{name}: вызывает test(), но не импортирует `test` из @playwright/test")
        if "expect(" in text and "expect" not in pw_names:
            issues.append(f"{name}: вызывает expect(), но не импортирует `expect` из @playwright/test")

    unused = sorted(x for x in exports if x not in referenced)
    stats["unused_exports"] = len(unused)
    stats["unused_names"] = unused
    return issues, stats


def receipt_line(stats, issues):
    tail = ""
    if stats["unused_names"]:
        tail = " | не используют спеки: " + ", ".join(stats["unused_names"][:6])
    return (f"квитанция: спек {stats['specs']} | импортов из api.js {stats['imports']} | "
            f"экспортов {stats['exports']} | нарушено: {len(issues)}{tail}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--json", action="store_true", help="машинный вывод")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    tests_dir = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    api = tests_dir / "api.js"
    if not api.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_fixture_contract: нет tests/api.js или каталога спек — "
              "не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
             for p in sorted(tests_dir.glob("*.spec.js"))}
    issues, stats = evaluate(api.read_text(encoding="utf-8", errors="replace"), specs)
    if a.json:
        print(json.dumps({"issues": issues, "stats": {k: v for k, v in stats.items()
                                                      if k != "unused_names"}},
                         ensure_ascii=False))
    else:
        for m in issues:
            print(f"::error::check_e2e_fixture_contract: {m}")
        print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    api = "function waitAppReady() {}\nmodule.exports = { waitAppReady, PIN, API_BASE };"

    def case(name, api_text, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(api_text, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:160]}")

    good = ("const { test, expect } = require('@playwright/test');\n"
            "const { waitAppReady, PIN } = require('./api');\n"
            "test('x', async () => { await waitAppReady(); expect(PIN).toBe('1'); });\n")
    case("норма", api, {"a.spec.js": good}, 0, ["нарушено: 0", "импортов из api.js 1"])

    bad_import = good.replace("waitAppReady, PIN", "waitAppReady, PIN, нетТакого")
    case("импорт несуществующего", api, {"b.spec.js": bad_import}, 1, ["нетТакого"])

    no_test = ("const { expect } = require('@playwright/test');\n"
               "const { PIN } = require('./api');\n"
               "test('x', async () => { expect(PIN).toBe('1'); });\n")
    case("test() без импорта", api, {"c.spec.js": no_test}, 1, ["не импортирует `test`"])

    no_expect = ("const { test } = require('@playwright/test');\n"
                 "test('x', async () => { expect(1).toBe(1); });\n")
    case("expect() без импорта", api, {"d.spec.js": no_expect}, 1, ["не импортирует `expect`"])

    total += 1
    stats = {"specs": 2, "exports": 3, "imports": 2, "unused_exports": 1, "unused_names": ["PIN"]}
    line = receipt_line(stats, [])
    if "спек 2" in line and "нарушено: 0" in line and "не используют спеки: PIN" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_fixture_contract --selftest: {ok}/{total} "
          f"(импорт несуществующего / test без импорта = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
