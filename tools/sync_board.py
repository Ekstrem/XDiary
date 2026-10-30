#!/usr/bin/env python3
"""Синхронизация 5-tech-backlog/*.md -> GitHub Issues + Projects-доска.

Два источника данных:
1. GitHub Issues (REST, GITHUB_TOKEN) — карточка-зеркало с лейблами
   роли (BE/DO/FE/TE/AR) и статуса (status:*).
2. GitHub Project (GraphQL, PROJECT_PAT) — поле Status для канбан-колонок.

Переменные окружения:
  GITHUB_TOKEN     — токен репозитория (issues: write), автомат. в Actions.
  GITHUB_REPOSITORY— owner/name (автомат. в Actions).
  PROJECT_PAT      — PAT с scope "project" (для ProjectsV2 GraphQL).
  PROJECT_OWNER    — владелец проекта (login), напр. Ekstrem.
  PROJECT_NUMBER   — номер проекта, напр. 2.

Если PROJECT_PAT не задан — синк только Issues (без колонок проекта).
"""
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

REPO = os.environ["GITHUB_REPOSITORY"]  # owner/name
# Issues/лейблы (REST): PAT с repo (PROJECT_PAT_ISSUE), иначе GITHUB_TOKEN.
TOKEN = os.environ.get("PROJECT_PAT_ISSUE", "").strip() or os.environ["GITHUB_TOKEN"]
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

# мой статус -> опция поля Status (дефолтный набор Project Board)
STATUS_TO_OPTION = {
    "открыта": "Todo",
    "в-работе": "In Progress",
    "на-проверке": "In Progress",
    "закрыта": "Done",
}


def rest(method, path, data=None, token=None):
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Authorization", "Bearer " + (token or TOKEN))
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


def graphql(query, variables, token):
    req = urllib.request.Request(API + "/graphql", method="POST")
    req.add_header("Authorization", "Bearer " + token)
    req.add_header("Content-Type", "application/json")
    body = json.dumps({"query": query, "variables": variables}).encode("utf-8")
    try:
        with urllib.request.urlopen(req, body) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        print("  ! GraphQL HTTP %s: %s" % (e.code, e.read().decode("utf-8", "ignore")[:300]),
              file=sys.stderr)
        return None


def ensure_label(name, color):
    if rest("GET", "/repos/%s/labels/%s" % (REPO, urllib.parse.quote(name))):
        return
    rest("POST", "/repos/%s/labels" % REPO, {"name": name, "color": color})


def parse_task(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    title = ""
    for line in text.splitlines():
        if line.startswith("# "):
            title = line[2:].strip()
            break
    status = "открыта"
    m = re.search(r"Статус:\*\*\s*([^)\n]+)", text)
    if m:
        s = m.group(1).strip().lower()
        if "закрыта" in s or "выполнена" in s:
            status = "закрыта"
        elif "на проверке" in s:
            status = "на-проверке"
        elif "в работе" in s:
            status = "в-работе"
    return title, status


def find_issue(task_id):
    q = urllib.parse.quote('repo:%s in:title "[%s]"' % (REPO, task_id))
    r = rest("GET", "/search/issues?q=" + q)
    if r and r.get("items"):
        return r["items"][0]
    return None


def resolve_project(owner, number, token):
    """Вернуть (project_id, field_id, {option_name: option_id}) для Status."""
    q = """
    query($login: String!, $number: Int!) {
      user(login: $login) {
        projectV2(number: $number) {
          id
          fields(first: 30) {
            nodes {
              ... on ProjectV2SingleSelectField {
                id name options { id name }
              }
            }
          }
        }
      }
    }"""
    r = graphql(q, {"login": owner, "number": number}, token)
    if not r:
        return None, None, None
    if r.get("errors"):
        for e in r["errors"]:
            print("  ! GraphQL: %s" % e.get("message", ""), file=sys.stderr)
        return None, None, None
    pv = ((r.get("data") or {}).get("user") or {}).get("projectV2")
    if not pv:
        print("  ! проект не найден (owner=%s number=%s)" % (owner, number), file=sys.stderr)
        return None, None, None
    project_id = pv.get("id")
    field_id = None
    opts = {}
    for field in (pv.get("fields") or {}).get("nodes", []):
        if not field:
            continue
        if (field.get("name") or "").lower() == "status":
            field_id = field.get("id")
            opts = {o.get("name"): o.get("id") for o in (field.get("options") or []) if o}
            break
    return project_id, field_id, opts


def sync_project(items, token, owner, number):
    """Добавить issue в проект и выставить Status."""
    project_id, field_id, opts = resolve_project(owner, number, token)
    if not project_id or not field_id:
        return 0
    done = 0
    for (issue_node_id, status_key) in items:
        if not issue_node_id:
            continue
        # добавить в проект (идемпотентно: уже добавленные вернутся)
        m = """
        mutation($projectId: ID!, $contentId: ID!) {
          addProjectV2ItemById(input: {projectId: $projectId, contentId: $contentId}) {
            item { id }
          }
        }"""
        r = graphql(m, {"projectId": project_id, "contentId": issue_node_id}, token)
        if not r or "errors" in r:
            continue
        item_id = r["data"]["addProjectV2ItemById"]["item"]["id"]
        opt_name = STATUS_TO_OPTION.get(status_key, "Todo")
        opt_id = opts.get(opt_name)
        if not opt_id:
            continue
        u = """
        mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $value: String!) {
          updateProjectV2ItemFieldValue(input: {
            projectId: $projectId, itemId: $itemId, fieldId: $fieldId,
            value: { singleSelectOptionId: $value }
          }) { clientMutationId }
        }"""
        graphql(u, {"projectId": project_id, "itemId": item_id,
                    "fieldId": field_id, "value": opt_id}, token)
        done += 1
    return done


def main():
    for name, color in {**ROLE_COLORS, **STATUS_LABELS}.items():
        ensure_label(name, color)

    created = updated = 0
    project_items = []  # (issue_node_id, status_key)
    for role_dir, role_label in ROLES.items():
        base = os.path.join("5-tech-backlog", role_dir)
        if not os.path.isdir(base):
            continue
        for fn in sorted(os.listdir(base)):
            if not fn.endswith(".md") or fn == "README.md":
                continue
            task_id = fn[:-3]
            path = os.path.join(base, fn).replace("\\", "/")
            title, status_key = parse_task(path)
            issue_title = "[%s] %s" % (task_id, title)
            labels = [role_label, "status:" + status_key]
            body = "Авто-синк из `%s`.\n\nЗадача-источник: `%s`" % (path, path)

            existing = find_issue(task_id)
            if existing:
                n = existing["number"]
                rest("PATCH", "/repos/%s/issues/%d" % (REPO, n),
                     {"title": issue_title, "labels": labels})
                updated += 1
                node_id = existing.get("node_id")
            else:
                r = rest("POST", "/repos/%s/issues" % REPO,
                         {"title": issue_title, "body": body, "labels": labels})
                if r:
                    created += 1
                    node_id = r.get("node_id")
                else:
                    node_id = None
            project_items.append((node_id, status_key))

    print("sync-board: created=%d updated=%d" % (created, updated))

    pat = os.environ.get("PROJECT_PAT", "").strip()
    owner = os.environ.get("PROJECT_OWNER", "").strip()
    number = os.environ.get("PROJECT_NUMBER", "").strip()
    if pat and owner and number:
        n = sync_project(project_items, pat, owner, int(number))
        print("sync-project: items=%d" % n)
    else:
        print("sync-project: пропущен (нет PROJECT_PAT/OWNER/NUMBER)")


if __name__ == "__main__":
    main()
