"""check_e2e_negative_pairs.py — негативный код без парной позитивной проверки не приёмка (ADR-026).

Смысл правила: «401 без 200» проверяет только, что запрос не пустили, но ничего не говорит о том,
что тот же путь с валидным токеном работает. Красная половина без зелёной легко «зеленеет» и на
сломанном эндпоинте, который отдаёт 401 всем подряд.

Гейт смотрит **утверждаемые** статусы (комментарии вырезаются, иначе упоминание `401` в пояснении
считается проверкой — так и было в `trends.spec.js`). Если в спеке есть утверждение негативного
кода (`401/403/404/405` рядом с `expect`/`toBe`/`status`), то в этой же спеке должно быть утверждение
позитивного (`200/201/204`). Исключения — только с записанной причиной.

Коды: `0` — пары на месте; `1` — негатив без позитива; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

NEGATIVE = ("401", "403", "404", "405")
POSITIVE = ("200", "201", "204")
ASSERT_HINT = re.compile(r"expect\(|\.toBe\(|\.toEqual\(|assert|status|includes\(", re.I)
STATUS_NUM = re.compile(r"\b(200|201|204|401|403|404|405)\b")

# Спеки, которые по своей природе проверяют только отказ, с причиной.
EXEMPT = {
    "no-pin.spec.js": "LEGACY describe.skip: контракт «PIN выдаёт токен» отменён (ADR-021)",
}


def strip_comments(text):
    out = []
    for line in text.splitlines():
        # строчный комментарий (не трогаем `//` внутри строк — для наших спеков достаточно)
        line = re.sub(r"(?<!:)\s//.*$", "", line)
        out.append(line)
    return "\n".join(out)


def evaluate(specs):
    """(issues, stats)."""
    issues, with_neg, with_pair, exempt = [], 0, 0, 0
    for name, text in sorted(specs.items()):
        if name in EXEMPT:
            exempt += 1
            continue
        code = strip_comments(text)
        asserted = [l for l in code.splitlines() if ASSERT_HINT.search(l)]
        neg = {n for l in asserted for n in NEGATIVE if re.search(r"\b%s\b" % n, l)}
        pos = {p for l in asserted for p in POSITIVE if re.search(r"\b%s\b" % p, l)}
        if not neg:
            continue
        with_neg += 1
        if pos:
            with_pair += 1
        else:
            issues.append(f"{name}: утверждает только {sorted(neg)} без парной позитивной проверки "
                          f"({'/'.join(POSITIVE)}) — по ADR-026 это не приёмка")
    stats = {"specs": len(specs), "with_neg": with_neg, "with_pair": with_pair, "exempt": exempt}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | с негативным утверждением {stats['with_neg']} | "
            f"с парной позитивной {stats['with_pair']} | исключений {stats['exempt']} | "
            f"нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    tests = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not tests.is_dir():
        print("::error::check_e2e_negative_pairs: нет папки спек — не измерено (rc=2)")
        return 2
    specs = {p.name: p.read_text(encoding="utf-8", errors="replace") for p in sorted(tests.glob("*.spec.js"))}
    issues, stats = evaluate(specs)
    for m in issues:
        print(f"::error::check_e2e_negative_pairs: {m}")
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

    case("пара есть",
         {"a.spec.js": "expect(r.status).toBe(401);\nexpect(ok.status).toBe(200);"},
         0, ["нарушено: 0", "с парной позитивной 1"])

    case("только негатив",
         {"b.spec.js": "expect(r.status).toBe(403);"},
         1, ["без парной позитивной", "b.spec.js"])

    case("негатив в комментарии не считается",
         {"c.spec.js": "// раньше тут был 401\nexpect(x).toBeVisible();"},
         0, ["с негативным утверждением 0"])

    case("исключение с причиной",
         {"no-pin.spec.js": "expect(r.status).toBe(401);"},
         0, ["исключений 1"])

    total += 1
    line = receipt_line({"specs": 4, "with_neg": 2, "with_pair": 1, "exempt": 1}, [])
    if "с негативным утверждением 2" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_negative_pairs --selftest: {ok}/{total} "
          f"(негатив без парного позитива = rc=1; комментарий не считается)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
