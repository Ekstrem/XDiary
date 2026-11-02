#!/usr/bin/env python3
"""Метрики бюджета CI (DO-023) → 7-deploy/CI-BUDGET.md.

Считает p50/p95 длительности джобов `test` и `front-e2e` по последним
зелёным (success) прогонам workflow `Test (9-tests)` и пишет Markdown-таблицу.
Запуск локально:  python tools/ci-metrics.py
"""
import json
import os
import sys
import urllib.request
from datetime import datetime, timezone

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "7-deploy", "CI-BUDGET.md")
REPO = "Ekstrem/XDiary"
API = "https://api.github.com"


def _get(path, token):
    req = urllib.request.Request(API + path)
    req.add_header("Authorization", "Bearer " + token)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "xdiary-ci-metrics")
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def _dur_s(job):
    try:
        a = datetime.fromisoformat(job["started_at"].replace("Z", "+00:00"))
        b = datetime.fromisoformat(job["completed_at"].replace("Z", "+00:00"))
        return (b - a).total_seconds()
    except (KeyError, ValueError):
        return None


def _pct(times, p):
    if not times:
        return None
    s = sorted(times)
    i = max(0, min(len(s) - 1, int(round(p / 100.0 * len(s)) - 1)))
    return s[i]


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if len(sys.argv) > 1 and sys.argv[1] in ("--help", "-h"):
        print(__doc__)
        return

    token = open(os.path.expanduser("~/Documents/Obsidian/DSH/GHT.md")).read().strip()
    runs = _get(f"/repos/{REPO}/actions/workflows/ci-tests.yml/runs?per_page=30", token)
    test_times, e2e_times = [], []
    for run in runs.get("workflow_runs", []):
        # Смотрим по джобам, а не по выводу run'а (run редко success целиком — E2E часто красный/отменён).
        jobs = _get(f"/repos/{REPO}/actions/runs/{run['id']}/jobs?per_page=100", token)
        for j in jobs.get("jobs", []):
            if j.get("conclusion") != "success":
                continue
            d = _dur_s(j)
            if d is None:
                continue
            if j["name"] == "test":
                test_times.append(d)
            elif j["name"] == "front-e2e":
                e2e_times.append(d)

    def row(name, times):
        if not times:
            return f"| `{name}` | 0 | — | — |"
        return f"| `{name}` | {len(times)} | {_pct(times,50):.0f}s | {_pct(times,95):.0f}s |"

    lines = [
        "# Бюджет CI (p50/p95 по зелёным прогонам)",
        "",
        "> Авто-генерация `tools/ci-metrics.py` — не редактировать вручную.",
        f"> Обновлён: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        "",
        "| Джоб | Зелёных прогонов | p50 | p95 |",
        "|---|---|---|---|",
        row("test", test_times),
        row("front-e2e", e2e_times),
        "",
        "Бюджеты (DO-023): гарантированное окно 5 мин → `test`/`front-e2e` не отменяются вовсе (AGENTS §5.1).",
        "",
    ]
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("written %s (test=%d, e2e=%d)" % (os.path.relpath(OUT, ROOT), len(test_times), len(e2e_times)))


if __name__ == "__main__":
    main()
