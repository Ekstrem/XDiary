"""check_e2e_mock_fields.py — двойник API отдаёт те поля, которые спеки проверяют утверждением.

Зачем: локальный прогон против `mock-api.js` падает не на дефекте продукта, а на **неполном двойнике**:
спека делает `expect(edited.rec.durationMin).toBe(45)`, а мок это поле не возвращает — и красный
приходит из ниоткуда. Ровно так было с `durationMin`/`place` (`edit-delete-record`) и `notes`/`gender`
(`partner-edit`).

Проверка узкая и точная: берём только поля, которые спека **утверждает** (`expect(<obj>.<field>…)`
для `rec`/`partner`/`p`/`me`/`profile`), и требуем, чтобы поле присутствовало в `mock-api.js`.
Случайные обращения (Playwright: `url`, `json`, `body`) не учитываются — они не про ответ API.

Коды: `0` — все утверждаемые поля двойник отдаёт; `1` — есть пропуски; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# объекты ответов API, на которых спеки делают утверждения
OBJ = r"(?:rec|record|partner|p|me|profile)"
ASSERT_FIELD = re.compile(r"expect\(\s*" + OBJ + r"\.(\w+)")
ASSERT_FIELD2 = re.compile(r"expect\(\s*\w+\.rec\.(\w+)")

# поля, которые к ответу API не относятся (это объекты Playwright/JS: ответ, запрос, body, json…).
# Без этого фильтра гейт краснел бы на `expect(p.body)`, хотя `p` — локальная переменная спеки.
NON_API = {
    "body", "data", "json", "url", "method", "status", "headers", "postData", "request",
    "text", "ok", "result", "onerror", "onsuccess", "calls", "length", "then", "catch",
}


def evaluate(specs, mock_text):
    """(issues, stats)."""
    fields = {}
    for name, text in sorted(specs.items()):
        for field in ASSERT_FIELD.findall(text) + ASSERT_FIELD2.findall(text):
            fields.setdefault(field, set()).add(name)
    issues = []
    fields = {f: w for f, w in fields.items() if f not in NON_API}
    for field, where in sorted(fields.items()):
        # поле «отдаётся», если оно есть в моке как ключ (field:) или как значение в объекте
        if not re.search(r"\b" + re.escape(field) + r"\s*:", mock_text):
            issues.append(f"спека утверждает `{field}` (например, {sorted(where)[0]}), "
                          f"но двойник API это поле не отдаёт")
    stats = {"specs": len(specs), "fields": len(fields)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: спек {stats['specs']} | утверждаемых полей API {stats['fields']} | "
            f"не отдаёт двойник: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    base = pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e"
    mock = base / "mock-api.js"
    tests_dir = base / "tests"
    if not mock.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_mock_fields: нет mock-api.js или каталога спек — "
              "не измерено (rc=2)")
        return 2
    specs = {}
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            specs[p.name] = p.read_text(encoding="utf-8", errors="replace")
        mock_text = mock.read_text(encoding="utf-8", errors="replace")
    except OSError as e:
        print(f"::error::check_e2e_mock_fields: не читается файл: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(specs, mock_text)
    for m in issues:
        print(f"::error::check_e2e_mock_fields: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, specs, mock, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(specs, mock)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    mock_ok = "function recordView(r) { return { id: r.id, durationMin: r.durationMin, place: r.place }; }"
    case("поле отдаётся",
         {"a.spec.js": "expect(edited.rec.durationMin).toBe(45);"}, mock_ok, 0,
         ["утверждаемых полей API 1", "не отдаёт двойник: 0"])

    case("поля нет в двойнике",
         {"b.spec.js": "expect(edited.rec.notInMock).toBe(1);"}, mock_ok, 1,
         ["notInMock", "не отдаёт"])

    case("Playwright-поля не считаются",
         {"c.spec.js": "const r = await rec.json(); expect(r.url).toBe('x');"}, mock_ok, 0,
         ["не отдаёт двойник: 0"])

    total += 1
    line = receipt_line({"specs": 2, "fields": 3}, [])
    if "утверждаемых полей API 3" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_mock_fields --selftest: {ok}/{total} (поле, которого нет в двойнике = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
