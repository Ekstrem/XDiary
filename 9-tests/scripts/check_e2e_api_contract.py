"""check_e2e_api_contract.py — эндпоинты, которые дёргают спеки, есть в `api-index.yaml`.

Зачем: спека может проверять путь, которого нет в контракте (`/api/v1/journal/records/{id}/complete`),
или ссылаться на удалённый маршрут. Тогда прогон либо падает 404 «по продукту», либо — хуже —
зеленеет на заглушке, и контракт живёт отдельно от тестов. Реестр операций (`api-index.yaml`,
80 операций) — единственный источник правды по путям.

Проверка: каждый `/api/v1/...` из `tests/*.spec.js` (без query) сопоставляется с путём реестра
с нормализацией: `{id}` ↔ `{...}` ↔ hex-uuid в спеке. Голый `/api/v1` (используется как префикс
в конкатенации) не считается путём.

Коды: `0` — все пути есть в реестре; `1` — есть неизвестные; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

SPEC_PATH = re.compile(r"""['"`](/api/v1/[^'"`?\s]*)""")
INDEX_PATH = re.compile(r'path:\s*"([^"]+)"')
UUIDISH = re.compile(r"/[0-9a-fA-F-]{6,}")


def normalize(path):
    p = path.rstrip("/")
    p = re.sub(r"\{[^}]+\}", "{id}", p)
    p = UUIDISH.sub("/{id}", p)
    return p


def evaluate(index_text, specs):
    """(issues, stats)."""
    contract = {normalize(p) for p in INDEX_PATH.findall(index_text)}
    used = {}
    for name, text in sorted(specs.items()):
        for m in SPEC_PATH.findall(text):
            path = m.rstrip("/")
            if path in ("/api/v1", "/api/v1/"):
                continue  # префикс конкатенации, а не операция
            used.setdefault(path, set()).add(name)
    issues = []
    for path, where in sorted(used.items()):
        if normalize(path) not in contract:
            issues.append(f"спека {'/'.join(sorted(where))} дёргает `{path}`, "
                          f"которого нет в `api-index.yaml`")
    stats = {"contract": len(contract), "used": len(used), "issues": len(issues)}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: операций в реестре {stats['contract']} | путей в спеках {stats['used']} | "
            f"неизвестных {stats['issues']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = pathlib.Path(a.root)
    index = root / "04-architecture" / "api-index.yaml"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not index.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_api_contract: нет api-index.yaml или спек — не измерено (rc=2)")
        return 2
    try:
        index_text = index.read_text(encoding="utf-8", errors="replace")
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_api_contract: не читается вход: {e} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(index_text, specs)
    for m in issues:
        print(f"::error::check_e2e_api_contract: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, index_text, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(index_text, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    idx = ('paths:\n  - path: "/api/v1/partners"\n  - path: "/api/v1/partners/{id}/remove"\n')
    case("путь есть в реестре", idx,
         {"a.spec.js": "fetch(base + '/api/v1/partners')"}, 0,
         ["путей в спеках 1", "неизвестных 0"])

    case("путь с плейсхолдером", idx,
         {"a.spec.js": "fetch(base + '/api/v1/partners/123456/remove')"}, 0, ["неизвестных 0"])

    case("путь вне реестра", idx,
         {"b.spec.js": "fetch(base + '/api/v1/journal/unknown')"}, 1,
         ["journal/unknown", "нет в `api-index.yaml`"])

    case("голый /api/v1 не считается операцией", idx,
         {"c.spec.js": "const base = '/api/v1' + '/partners'"}, 0, ["неизвестных 0"])

    total += 1
    line = receipt_line({"contract": 3, "used": 2, "issues": 0}, [])
    if "операций в реестре 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_api_contract --selftest: {ok}/{total} (путь вне api-index = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
