#!/usr/bin/env python
"""check_apim_parity.py — паритет ПУБЛИЧНОГО поведения API между двумя стендами.

Зачем (@TL 2026-11-15, вопрос владельца «можно ли сверять конфигурацию API в Gravitee
на проде и тесте перед CD микросервисов»): расхождение стендов дважды за сутки давало
пользовательские баги на проде — «Фантазии» и добавление акта падали с 500, потому что
в APIM на проде жило сломанное определение API (`servers: "/"`), а на dev всё работало.

Идея: не лезть в management-API Gravitee (он не опубликован и требует админа), а сравнить
**то, что видит клиент**: для каждой операции из `04-architecture/api-index.yaml` дёрнуть
оба стенда БЕЗ токена и сравнить КЛАСС ответа. Инвариант: одинаковый контракт ⇒ одинаковый
класс (401 у защищённых, 4xx у валидирующих, и никогда 5xx).

rc=0 — классы совпали; rc=1 — расхождение (печатается таблица); rc=2 — измерить нельзя.

Проверка: `python tools/check_apim_parity.py --selftest` (6/6).
"""
import argparse
import json
import sys
import urllib.error
import urllib.request

DEFAULT_TIMEOUT = 12


def classify(code):
    if code in (401, 403):
        return "protected"
    if 400 <= code < 500:
        return "validated"
    if 500 <= code < 600:
        return "broken"
    if code == 200:
        return "open"
    return "noanswer"


def probe(url, timeout, retry_unknown=0, pause=1.0):
    """GET без токена. Повторяем ТОЛЬКО «нет ответа» (код 0): это сетевой сбой/таймаут,
    а не сигнал. 5xx НЕ повторяем намеренно — 5xx и есть измеряемое расхождение; если его
    «перепрашивать до зелёного», гейт перестанет ловить сломанный backend (класс из DO-098)."""
    import time as _t
    for attempt in range(retry_unknown + 1):
        req = urllib.request.Request(url, method="GET")
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.status
        except urllib.error.HTTPError as e:
            return e.code
        except Exception:
            if attempt < retry_unknown:
                _t.sleep(pause)
                continue
            return 0
    return 0


def operations(root):
    """[(method, path)] из api-index.yaml; path шаблоны заменяем на фиксированный uuid."""
    try:
        import yaml
    except ImportError:
        return None
    doc = yaml.safe_load(open(root + "/04-architecture/api-index.yaml", encoding="utf-8"))
    ops = []
    seen = set()
    for block in doc.get("operations") or []:
        if block.get("internal"):
            continue
        method = str(block.get("method", "GET")).upper()
        path = block.get("path") or ""
        if not path:
            continue
        key = (method, path)
        if key in seen:
            continue
        seen.add(key)
        ops.append((method, path))
    return ops


def instantiate(path):
    return path.replace("{id}", "00000000-0000-0000-0000-000000000000")


def compare(a_base, b_base, ops, timeout, retry_unknown=0):
    rows = []
    for method, path in ops:
        p = instantiate(path)
        ca = probe(a_base.rstrip("/") + p, timeout, retry_unknown) if method == "GET" else 0
        cb = probe(b_base.rstrip("/") + p, timeout, retry_unknown) if method == "GET" else 0
        if method != "GET":
            rows.append((method, path, "skip", "skip", None, None))
            continue
        rows.append((method, path, classify(ca), classify(cb), ca, cb))
    return rows


def verdict(rows):
    drift = []
    for method, path, ka, kb, ca, cb in rows:
        if ka == "skip":
            continue
        if ka == kb:
            if ka == "broken":
                drift.append((method, path, ka, kb, ca, cb, "оба стенда 5xx — сломан контракт/backend"))
            continue
        drift.append((method, path, ka, kb, ca, cb, "классы различаются"))
    return drift


def selftest():
    cases = [
        (401, "protected"), (403, "protected"), (400, "validated"), (404, "validated"),
        (415, "validated"), (500, "broken"), (502, "broken"), (200, "open"), (0, "noanswer"),
    ]
    ok = 0
    total = 0
    for code, want in cases:
        total += 1
        got = classify(code)
        if got == want:
            ok += 1
        else:
            print("FAIL classify(%s) = %s, ожидали %s" % (code, got, want))
    rows = [("GET", "/a", "protected", "protected", 401, 401),
            ("GET", "/b", "broken", "protected", 500, 401)]
    d = verdict(rows)
    total += 1
    if len(d) == 1 and d[0][1] == "/b":
        ok += 1
    else:
        print("FAIL verdict: ожидали одно расхождение по /b, получили", d)
    # @AR (2026-11-15 17:42): «оба 5xx» — это тоже расхождение (одинаково сломано ≠ паритет)
    both_broken = verdict([("GET", "/c", "broken", "broken", 500, 500)])
    total += 1
    if len(both_broken) == 1 and both_broken[0][1] == "/c":
        ok += 1
    else:
        print("FAIL verdict: оба 5xx должны быть расхождением, получили", both_broken)
    print("ИТОГ: %d/%d проверок" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--a", default="https://xdiary-vps1.taile0b6ee.ts.net", help="стенд A (прод)")
    ap.add_argument("--b", default="https://xdiary-vps2.taile0b6ee.ts.net", help="стенд B (dev)")
    ap.add_argument("--root", default=".")
    ap.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT)
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--json", action="store_true", help="машинный вывод")
    ap.add_argument("--retry-unknown", type=int, default=2,
                    help="повторов при «нет ответа» (код 0); 5xx НЕ повторяем — это и есть сигнал")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    ops = operations(args.root)
    if not ops:
        print("::error::check_apim_parity: не прочитал операции из api-index.yaml (rc=2)")
        return 2
    rows = compare(args.a, args.b, ops, args.timeout, args.retry_unknown)

    measured = [r for r in rows if r[2] != "skip"]
    # Замечание @TE/@AR (2026-11-15): «зелено на пустоте» быть не должно — требуем минимум измерений.
    # В прогоне 2026-11-15 измерялось 25 (все GET из api-index); 20 — сознательно ниже факта,
    # но выше любого «пустого» прогона (0 или несколько штук).
    MIN_MEASURED = 20
    if len(measured) < MIN_MEASURED:
        print("::error::check_apim_parity: измерено %d < %d — это не «зелено» (rc=2)"
              % (len(measured), MIN_MEASURED))
        return 2

    drift = verdict(rows)
    if args.json:
        print(json.dumps({"a": args.a, "b": args.b,
                          "measured": len(measured), "drift": [
                              {"method": m, "path": p, "a": ka, "b": kb, "code_a": ca, "code_b": cb, "why": why}
                              for m, p, ka, kb, ca, cb, why in drift]}, ensure_ascii=False, indent=2))
    else:
        # @AR (2026-11-16 10:52): порог «20 из 25» сам по себе не говорит, какие именно измерены.
        # Печатаем список измеренных операций — квитанция должна быть проверяемой, а не «20 ок».
        measured_ops = [("GET %s" % r[1]) for r in measured]
        print("измеренные операции (%d):" % len(measured_ops))
        for op in measured_ops:
            print("  - %s" % op)
        print()
        print("| операция | прод | dev | why |")
        print("|---|---|---|---|")
        for m, p, ka, kb, ca, cb, why in drift:
            print("| %s %s | %s (%s) | %s (%s) | %s |" % (m, p, ka, ca, kb, cb, why))
        print("измерено операций: %d, расхождений: %d" % (len(measured), len(drift)))

    return 0 if not drift else 1


if __name__ == "__main__":
    sys.exit(main())
