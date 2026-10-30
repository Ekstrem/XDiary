#!/usr/bin/env python3
"""Синхронизация 5-tech-backlog/*.md -> GitHub Issues (для Projects-доски).

Каждая задача (.md) превращается в Issue:
  заголовок: "[BE-002] Реализовать journal ..."
  лейблы: роль (BE/DO/FE/TE/AR) + status:<открыта|в-работе|на-проверке|закрыта>

Источник правды — .md-файлы; Issues — автоматическое зеркало.
Запускается GitHub Action'ом (push в main). Только stdlib.
"""
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

REPO = os.environ["GITHUB_REPOSITORY"]  # owner/name
TOKEN = os.environ["GITHUB_TOKEN"]
API = "https://api.github.com"

ROLES = {
    "back-end": "BE",
    "devops": "DO",
    "front-end": "FE",
    "tester": "TE",
    "arch": "AR",
}
STATUS_LABELS = {
    "status:открыта": "1d76db",
    "status:в-работе": "fbca04",
    "status:на-проверке": "d93f0b",
    "status:закрыта": "0e8a16",
}
ROLE_COLORS = {
    "BE": "0e8a16",
    "DO": "1d76db",
    "FE": "fbca04",
    "TE": "d93f0b",
    "AR": "5319e7",
}


def api(method, path, data=None):
    """Вызов GitHub REST API v3."""
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Authorization", "Bearer " + TOKEN)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    body = None
    if data is not None:
        body = json.dumps(data).encode("utf-8")
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, body) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        print("  ! HTTP %s: %s" % (e.code, e.read().decode("utf-8", "ignore")[:200]),
              file=sys.stderr)
        return None


def ensure_label(name, color):
    """Создать лейбл, если его ещё нет."""
    if api("GET", "/repos/%s/labels/%s" % (REPO, urllib.parse.quote(name))):
        return
    api("POST", "/repos/%s/labels" % REPO, {"name": name, "color": color})


def parse_task(path):
    """Вернуть (title, status_label) из .md."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    title = ""
    for line in text.splitlines():
        if line.startswith("# "):
            title = line[2:].strip()
            break
    status = "status:открыта"
    m = re.search(r"Статус:\*\*\s*([^)\n]+)", text)
    if m:
        s = m.group(1).strip().lower()
        if "закрыта" in s or "выполнена" in s:
            status = "status:закрыта"
        elif "на проверке" in s:
            status = "status:на-проверке"
        elif "в работе" in s:
            status = "status:в-работе"
    return title, status


def find_issue(task_id):
    """Найти существующий Issue по [ID] в заголовке."""
    q = urllib.parse.quote('repo:%s in:title "[%s]"' % (REPO, task_id))
    r = api("GET", "/search/issues?q=" + q)
    if r and r.get("items"):
        return r["items"][0]
    return None


def main():
    for name, color in {**ROLE_COLORS, **STATUS_LABELS}.items():
        ensure_label(name, color)

    created = updated = 0
    for role_dir, role_label in ROLES.items():
        base = os.path.join("5-tech-backlog", role_dir)
        if not os.path.isdir(base):
            continue
        for fn in sorted(os.listdir(base)):
            if not fn.endswith(".md") or fn == "README.md":
                continue
            task_id = fn[:-3]  # BE-001
            path = os.path.join(base, fn).replace("\\", "/")
            title, status = parse_task(path)
            issue_title = "[%s] %s" % (task_id, title)
            labels = [role_label, status]
            body = "Авто-синк из `%s`.\n\nЗадача-источник: `%s`" % (path, path)

            existing = find_issue(task_id)
            if existing:
                n = existing["number"]
                api("PATCH", "/repos/%s/issues/%d" % (REPO, n),
                    {"title": issue_title, "labels": labels})
                updated += 1
            else:
                r = api("POST", "/repos/%s/issues" % REPO,
                        {"title": issue_title, "body": body, "labels": labels})
                if r:
                    created += 1

    print("sync-board: created=%d updated=%d" % (created, updated))


if __name__ == "__main__":
    main()
