#!/usr/bin/env python3
"""DO-022 dry-run: отчёт по дублям Issues на задачи. Ничего не закрывает.

Собирает все Issues репозитория, группирует по ID задачи (XX-NNN) из заголовка
и выводит Markdown-отчёт: сколько Issues на задачу, какие номера, состояние.
Печатает в stdout — приложить к DO-022 и показать @TL до массового закрытия.

Запуск: python tools/cleanup_board.py
"""
import json
import os
import re
import sys
import urllib.request
from collections import Counter

REPO = "Ekstrem/XDiary"
API = "https://api.github.com"
TASK_ID = re.compile(r"\b([A-Z]{2,3}-\d{2,4})\b")


def api(path, token):
    req = urllib.request.Request(API + path)
    req.add_header("Authorization", "Bearer " + token)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "xdiary-cleanup")
    with urllib.request.urlopen(req, timeout=40) as r:
        return json.loads(r.read().decode()), r.headers


def main():
    token = None
    for line in open(os.path.expanduser("~/Documents/Obsidian/DSH/GHT.md"), encoding="utf-8"):
        t = line.strip()
        if t.startswith("github_pat_") or t.startswith("ghp_"):
            token = t
            break
    if not token:
        sys.exit("нет токена в GHT.md")

    issues = []
    page = 1
    while page <= 40:  # safety: до 4000 Issues
        data, _ = api(f"/repos/{REPO}/issues?state=all&per_page=100&page={page}", token)
        if not data:
            break
        issues.extend(data)
        if len(data) < 100:
            break
        page += 1

    # PR-ы не считаем
    issues = [i for i in issues if "pull_request" not in i]

    groups = {}
    for it in issues:
        m = TASK_ID.search(it.get("title", ""))
        key = m.group(1) if m else "(без ID)"
        groups.setdefault(key, []).append(it)

    total = len(issues)
    open_cnt = sum(1 for i in issues if i["state"] == "open")
    no_id = len(groups.get("(без ID)", []))
    dup_tasks = {k: v for k, v in groups.items() if k != "(без ID)" and len(v) > 1}
    single = sum(1 for k, v in groups.items() if k != "(без ID)" and len(v) == 1)

    print("# DO-022 dry-run: дубли Issues по задачам\n")
    print(f"- Всего Issues (без PR): **{total}** (open {open_cnt}, closed {total - open_cnt})")
    print(f"- Задач с Issues: **{len(dup_tasks) + single}**; без ID: **{no_id}**")
    print(f"- Задач с дублями (>1 Issue): **{len(dup_tasks)}**")
    print(f"- Задач с ровно одним Issue: **{single}**")
    if dup_tasks:
        print("\n## Худшие дубли (топ-20)\n")
        print("| Задача | Issues | Открытых | Номера (первые 12) |")
        print("|---|---|---|---|")
        for k, v in sorted(dup_tasks.items(), key=lambda kv: -len(kv[1]))[:20]:
            opens = sum(1 for i in v if i["state"] == "open")
            nums = ", ".join("#" + str(i["number"]) for i in v[:12])
            print(f"| {k} | {len(v)} | {opens} | {nums} |")
    labels = Counter()
    for i in issues:
        for l in i.get("labels", []):
            labels[l["name"]] += 1
    print("\n## Лейблы (топ-10)\n")
    for name, n in labels.most_common(10):
        print(f"- `{name}`: {n}")


if __name__ == "__main__":
    main()
