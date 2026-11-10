"""Диагностика канона: длительность и код КАЖДОЙ пробы (зона @DO, tools/).

Зачем: @TL 2026-11-10 15:30 — «7 неизвестных по 200–500 мс и по 30 с — два разных диагноза,
и по имени их не различить». Значит нужна таблица «операция → код → длительность», а не
агрегат. Скрипт использует функции `check_apim_auth.py` (импорт, не копия), поэтому канон
не меняется: те же классы, тот же знаменатель.

Пример:
    python tools/canon_per_probe.py --base https://xdiary-vps1.taile0b6ee.ts.net --passes 3
"""
from __future__ import annotations

import argparse
import importlib.util
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def load_canon():
    path = ROOT / "9-tests" / "scripts" / "check_apim_auth.py"
    spec = importlib.util.spec_from_file_location("canon", path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["canon"] = mod
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--passes", type=int, default=1)
    ap.add_argument("--pause", type=int, default=0)
    ap.add_argument("--retry-unknown", type=int, default=6)
    a = ap.parse_args()

    canon = load_canon()
    ops = [o for o in canon.load_ops() if canon.requires_auth(o) and not o.get("internal")]
    print("проб всего: %d | база %s | проходов %d" % (len(ops), a.base, a.passes))

    all_rows = []
    for p in range(1, a.passes + 1):
        print("\n=== проход %d ===" % p)
        t_pass = time.time()
        rows = []
        for op in ops:
            name = op.get("operationId") or op.get("path") or "?"
            t0 = time.time()
            code, where = canon.probe_op(a.base, op, a.retry_unknown)
            dt = time.time() - t0
            cls = canon.classify_code(code, True) if code is not None else "unknown"
            rows.append((name, code, dt, cls))
            if cls != "protected":
                print("  %-7s %-42s %6.2f с" % (cls, str(name)[:42], dt), flush=True)
        dur = time.time() - t_pass
        times = [r[2] for r in rows]
        by_cls = {}
        for r in rows:
            by_cls[r[3]] = by_cls.get(r[3], 0) + 1
        print("  проход %d: %.1f с | классы: %s | мин %.2f / медиана %.2f / макс %.2f с"
              % (p, dur, by_cls, min(times), statistics.median(times), max(times)))
        slow = sorted(rows, key=lambda r: -r[2])[:5]
        print("  самые медленные: " + ", ".join("%s %.1f с" % (str(r[0])[:24], r[2]) for r in slow))
        all_rows.extend(rows)
        if p < a.passes and a.pause:
            print("  пауза %d с" % a.pause, flush=True)
            time.sleep(a.pause)

    print("\n=== сводка по всем проходам ===")
    unk = [r for r in all_rows if r[3] == "unknown"]
    print("  проб всего: %d | unknown: %d" % (len(all_rows), len(unk)))
    if unk:
        print("  unknown по длительности:")
        for name, code, dt, _ in sorted(unk, key=lambda r: -r[2]):
            print("    %-40s %6.2f с" % (str(name)[:40], dt))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
