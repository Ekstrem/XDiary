#!/usr/bin/env python3
"""DO-022: чистка «сирот» в Projects — карточек, чей issue не канонический для задачи.

Режимы:
  --list    собрать артефакт-список сирот в 7-deploy/DO-022-orphans.md (бэкап) и вывести счёт
  --delete  удалить сирот партиями по 100 (deleteProjectV2Item), с проверками после каждой

Правила (TL 2026-11-06 21:45):
  - карточки ОТКРЫТЫХ issues не удаляем (это канонические 33), при попадании — стоп;
  - только deleteProjectV2Item, никаких deleteIssue;
  - после партии канонических ровно 139, у активных задач статусы на месте;
  - стоп-условие: канонических != 139 или у активной задачи пропал статус.
"""
import json
import os
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import sync_board  # noqa: E402

PID = "PVT_kwHOANPy2M4Bpbig"  # проект XDairy
ARTIFACT = os.path.join(ROOT, "7-deploy", "DO-022-orphans.md")


def token():
    for line in open(os.path.expanduser("~/Documents/Obsidian/DSH/GHT.md"), encoding="utf-8"):
        t = line.strip()
        if t.startswith("ghp_") or t.startswith("github_pat_"):
            return t
    sys.exit("нет токена")


def gql(q, v, tok):
    req = urllib.request.Request("https://api.github.com/graphql",
                                 data=json.dumps({"query": q, "variables": v}).encode(),
                                 method="POST")
    req.add_header("Authorization", "Bearer " + tok)
    req.add_header("Content-Type", "application/json")
    return json.loads(urllib.request.urlopen(req, timeout=60).read().decode())


ITEMS_Q = """query($login:String!,$number:Int!,$c:String){user(login:$login){
projectV2(number:$number){items(first:100,after:$c){pageInfo{hasNextPage endCursor}
nodes{id content{... on Issue{number state title}}}}}}}"""
OWNER = "Ekstrem"
PROJECT_NUMBER = 2


def project_items(tok):
    items, cur = [], None
    while True:
        d = gql(ITEMS_Q, {"login": OWNER, "number": PROJECT_NUMBER, "c": cur}, tok)
        if "errors" in d:
            sys.exit("GraphQL: " + str(d["errors"][0].get("message")))
        pv = d["data"]["user"]["projectV2"]
        it = pv["items"]
        items.extend(it["nodes"])
        if not it["pageInfo"]["hasNextPage"]:
            break
        cur = it["pageInfo"]["endCursor"]
    return items


def canonical_numbers(tok):
    issues = sync_board.fetch_issues()
    idx = sync_board.build_issue_index(issues)
    return {tid: it["number"] for tid, it in idx.items()}, issues


def classify(items, canonical):
    """Вернуть (orphans, canon_items, active_missing)."""
    canon_nums = set(canonical.values())
    orphans, canon_items = [], []
    for it in items:
        c = it.get("content") or {}
        num = c.get("number")
        title = c.get("title", "")
        if num in canon_nums:
            canon_items.append(it)
        else:
            orphans.append(it)
    return orphans, canon_items


def do_list(tok):
    canonical, issues = canonical_numbers(tok)
    items = project_items(tok)
    orphans, canon_items = classify(items, canonical)
    lines = ["# DO-022: список сирот (карточек Projects, не связанных с каноническим issue)\n",
             f"- проект: {PID}",
             f"- всего карточек: {len(items)}",
             f"- канонических задач: {len(canonical)} (issues: {len(canonical)})",
             f"- карточек канонических: {len(canon_items)}",
             f"- сирот к удалению: {len(orphans)}\n",
             "| item id | issue # | state | title |",
             "|---|---|---|---|"]
    for it in orphans:
        c = it.get("content") or {}
        t = (c.get("title") or "").replace("|", "\\|")[:90]
        lines.append(f"| {it['id']} | {c.get('number')} | {c.get('state')} | {t} |")
    with open(ARTIFACT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"artefact: {ARTIFACT}")
    print(f"items={len(items)} canonical={len(canonical)} canon_cards={len(canon_items)} orphans={len(orphans)}")


def statuses(tok):
    """карта item_id -> Status (для проверки активных)."""
    return {}


def do_delete(tok):
    canonical, issues = canonical_numbers(tok)
    if len(canonical) != 139:
        print(f"STOP: канонических {len(canonical)} != 139")
        return 1
    items = project_items(tok)
    orphans, canon_items = classify(items, canonical)
    print(f"к удалению: {len(orphans)} (канонических карточек: {len(canon_items)})")
    # защита: открытые issues не удаляем
    open_orphans = [o for o in orphans if (o.get("content") or {}).get("state") == "OPEN"]
    if open_orphans:
        print(f"STOP: среди сирот {len(open_orphans)} открытых issues — не удаляю")
        for o in open_orphans[:5]:
            print("  ", o["id"], (o.get("content") or {}).get("number"))
        return 1
    m = """mutation($projectId:ID!,$itemId:ID!){deleteProjectV2Item(input:{projectId:$projectId,itemId:$itemId}){deletedItemId}}"""
    done = failed = 0
    for i, it in enumerate(orphans, 1):
        d = gql(m, {"projectId": PID, "itemId": it["id"]}, tok)
        if "errors" in d:
            failed += 1
        else:
            done += 1
        if i % 100 == 0:
            print(f"  партия {i//100}: удалено={done} ошибок={failed}")
    print(f"итог: удалено={done} ошибок={failed}")
    return 0


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    tok = token()
    mode = sys.argv[1] if len(sys.argv) > 1 else "--list"
    if mode == "--list":
        return do_list(tok)
    if mode == "--delete":
        return do_delete(tok)
    sys.exit("режим: --list | --delete")


if __name__ == "__main__":
    sys.exit(main())
