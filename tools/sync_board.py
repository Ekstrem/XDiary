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
# Issues/лейблы (REST): GITHUB_TOKEN (workflow permissions = read+write).
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
    "status:идея": "ededed",
    "status:на-проработке": "c5def5",
    "status:todo": "1d76db",
    "status:в-работе": "fbca04",
    "status:в-паузе": "d4c5f9",
    "status:на-проверке": "d93f0b",
    "status:закрыта": "0e8a16",
    "status:отклонена": "6e6e6e",
}
ROLE_COLORS = {
    "BE": "0e8a16",
    "DO": "1d76db",
    "FE": "fbca04",
    "TE": "d93f0b",
    "AR": "5319e7",
}

# статус задачи -> опция поля Status в Projects (опции доски: Todo / In Progress / Review / Paused / Done)
STATUS_TO_OPTION = {
    "идея": "Todo",
    "на-проработке": "Todo",
    "todo": "Todo",
    "в-работе": "In Progress",
    "в-паузе": "Paused",
    "на-проверке": "Review",
    "закрыта": "Done",
    "отклонена": "Done",
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


def rest_status(method, path, data=None, token=None):
    """Как rest(), но возвращает HTTP-код: DELETE отдаёт 204 и пустое тело."""
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
            resp.read()
            return resp.status
    except urllib.error.HTTPError as e:
        e.read()
        return e.code


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
    status = "todo"
    m = re.search(r"Статус:\*\*\s*([^)\n]+)", text)
    if m:
        s = m.group(1).strip().lower()
        # порядок важен: «на проработке» / «в паузе» не должны попасть в другие ветки
        if "отклон" in s:
            status = "отклонена"
        elif "закрыт" in s or "выполн" in s or "готов" in s or "done" in s:
            status = "закрыта"
        elif "пауз" in s:
            status = "в-паузе"
        elif "проработ" in s:
            status = "на-проработке"
        elif "проверк" in s or "ревью" in s:
            status = "на-проверке"
        elif "иде" in s:
            status = "идея"
        elif "работе" in s:
            status = "в-работе"
        elif "todo" in s or "очеред" in s or "открыт" in s:
            status = "todo"
    return title, status


ISSUE_INDEX = {}  # task_id -> канонический issue (минимальный номер)
ISSUES_ALL = []


def task_id_of(issue):
    m = re.match(r"^\[([A-Z]+-\d+)\]", issue.get("title", ""))
    return m.group(1) if m else None


def fetch_issues():
    """Все issues (открытые+закрытые), пагинация по 100. Детерминированно."""
    out, page = [], 1
    while True:
        r = rest("GET", "/repos/%s/issues?state=all&per_page=100&page=%d" % (REPO, page))
        if not r:
            break
        out += r
        if len(r) < 100:
            break
        page += 1
    return out


def build_issue_index(issues):
    """task_id -> канонический issue (с минимальным номером = самый старый)."""
    idx = {}
    for i in issues:
        if i.get("pull_request"):
            continue
        tid = task_id_of(i)
        if not tid:
            continue
        cur = idx.get(tid)
        if cur is None or i["number"] < cur["number"]:
            idx[tid] = i
    return idx


def find_issue(task_id):
    return ISSUE_INDEX.get(task_id)


def close_duplicates(issues):
    """Убрать лишние issue с тем же task_id.

    DO-022 (@TL 2026-11-13 09:10): раньше дубликаты только **закрывались** и оставались
    на доске навсегда — накопилось 1614 лишних issues (1851 при 237 карточках). Теперь:
    дубликаты **удаляются**, если у токена есть право (`DELETE` → 204); если права нет
    (GitHub отдаёт 404) — **закрываются** и пишется предупреждение один раз, чтобы гейт
    не падал, но и «тихо зелёным» не был. Канон — открытый с минимальным номером,
    иначе минимальный номер.
    """
    removed = closed = denied = 0
    warned = False
    for i in issues:
        if i.get("pull_request"):
            continue
        tid = task_id_of(i)
        if not tid:
            continue
        canon = ISSUE_INDEX.get(tid)
        if not canon or i["number"] == canon["number"]:
            continue
        if i.get("state") == "open" and canon.get("state") != "open":
            continue  # открытую не трогаем, если канон закрыт
        st = rest_status("DELETE", "/repos/%s/issues/%d" % (REPO, i["number"]))
        if st in (204, 200):
            removed += 1
            continue
        if st == 404 and not warned:
            print("  ! DELETE issue недоступен (404): у PAT нет Issues: write — "
                  "дубликаты закрываю, а не удаляю (DO-022)")
            warned = True
        if st == 404:
            denied += 1
        rest("PATCH", "/repos/%s/issues/%d" % (REPO, i["number"]),
             {"state": "closed", "labels": ["duplicate"]})
        closed += 1
    if removed or closed:
        print("  дубликаты: удалено %d, закрыто %d%s"
              % (removed, closed, " (нет права на удаление)" if denied else ""))
    return closed


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

    # диагностика: чей токен и может ли он читать
    me = rest("GET", "/user")
    print("token-user: %s" % (me.get("login") if me else "ОШИБКА/невалиден"))

    # ДЕТЕРМИНИРОВАННЫЙ индекс issues вместо Search API.
    # Search API даёт лаг индексации и rate-limit → только что созданные issue
    # не находились, и скрипт создавал дубли (было 500 issue на 40 задач).
    global ISSUES_ALL, ISSUE_INDEX
    ISSUES_ALL = fetch_issues()
    ISSUE_INDEX = build_issue_index(ISSUES_ALL)
    print("issues: %d, уникальных задач: %d" % (len(ISSUES_ALL), len(ISSUE_INDEX)))
    dups = close_duplicates(ISSUES_ALL)
    if dups:
        print("dedup: закрыто %d дублей" % dups)

    created = updated = failed = 0
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
            # убрать ведущий "BE-001: " из заголовка .md (иначе дубль в Issue)
            clean_title = re.sub(r"^" + re.escape(task_id) + r"\s*[:—–-]\s*", "", title)
            issue_title = "[%s] %s" % (task_id, clean_title)
            labels = [role_label, "status:" + status_key]
            body = "Авто-синк из `%s`.\n\nЗадача-источник: `%s`" % (path, path)

            existing = find_issue(task_id)
            if existing:
                n = existing["number"]
                rest("PATCH", "/repos/%s/issues/%d" % (REPO, n),
                     {"title": issue_title, "labels": labels,
                      "state": "closed" if status_key == "закрыта" else "open"})
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
                    failed += 1
            # DO-022 (@TL 2026-11-13 10:10, п.3): в Project добавляем ТОЛЬКО открытые
            # карточки. Закрытые на доске не нужны: именно они раздували её до 1800
            # элементов при 237 карточках.
            if status_key != "закрыта":
                project_items.append((node_id, status_key))

    print("sync-board: created=%d updated=%d failed=%d" % (created, updated, failed))
    if failed:
        print("  ! не удалось создать %d Issue — см. строки '! HTTP' выше" % failed,
              file=sys.stderr)
        sys.exit(1)

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
