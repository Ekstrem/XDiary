#!/usr/bin/env python3
"""check_api_index.py — гейт «индекс API ↔ код» (DO-064, спека @AR 2026-11-11).

Что проверяет:
  1. Набор операций индекса (`04-architecture/api-index.yaml`) == набор маршрутов
     в контроллерах `6-msa/BC-*/**/*Controller.cs`. Расхождение в любую сторону → rc=1
     с конкретной парой (метод, путь).
  2. У каждой операции индекса заполнен `codes` (иначе rc=1).
  3. `auth: anonymous` ровно у 4 контроллерных маршрутов allow-list
     (register / verify / refresh / verify-token). `GET /health` исключён — не контроллер.

Зачем: индекс — это реестр контракта (ADR-025), и «индекс отстал от кода» иначе
обнаруживается глазами. Правило изменения индекса (правка + гейт в одном коммите)
записано в самом файле.

Почему без PyYAML: индекс — плоский список простых пар, а зависимость в CI дороже
20 строк разбора; формат жёстко задан владельцем и проверяется этим же гейтом.

    python tools/check_api_index.py            # зелёный/красный по репозиторию
    python tools/check_api_index.py --index f  # другой индекс (для негативного контроля)
    python tools/check_api_index.py --selftest
"""
import argparse
import os
import re
import sys

INDEX = "04-architecture/api-index.yaml"
ANON_ALLOW = {
    ("POST", "/api/v1/clients"),
    ("POST", "/api/v1/clients/refresh"),
    ("POST", "/api/v1/clients/{id}/verify"),
    ("POST", "/api/v1/privacy/verify-token"),
}
NORM = re.compile(r"\{(\w+)(?::[a-z]+)?\}")


def norm_path(p):
    p = p.strip().strip('"').strip("'")
    p = NORM.sub(lambda m: "{%s}" % m.group(1), p)
    if not p.startswith("/"):
        p = "/" + p
    return p.rstrip("/") or "/"


def parse_index(path):
    ops, cur = [], None
    for line in open(path, encoding="utf-8"):
        s = line.rstrip("\n")
        m = re.match(r"\s*-\s*bc:\s*(.+)$", s)
        if m:
            if cur:
                ops.append(cur)
            cur = {"bc": m.group(1).strip()}
            continue
        if cur is None:
            continue
        m = re.match(r"\s+(method|path|operationId|auth|codes|codes_status):\s*(.*)$", s)
        if m:
            cur[m.group(1)] = m.group(2).strip()
    if cur:
        ops.append(cur)
    return ops


def parse_code(root):
    """Маршруты из контроллеров: [(method, path, anonymous, file)]."""
    found = []
    for dirpath, _, names in os.walk(os.path.join(root, "6-msa")):
        for n in names:
            if not n.endswith("Controller.cs"):
                continue
            f = os.path.join(dirpath, n)
            try:
                text = open(f, encoding="utf-8-sig").read()
            except OSError:
                continue
            base = re.search(r'\[Route\("([^"]*)"\)\]', text)
            base_path = base.group(1) if base else ""
            for mm in re.finditer(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', text):
                verb = mm.group(1).upper()
                suffix = mm.group(2) or ""
                path = norm_path("/" + base_path.strip("/") + ("/" + suffix.strip("/") if suffix else ""))
                tail = text[mm.end():mm.end() + 300]
                anon = "[AllowAnonymous]" in tail.split("public ")[0] if "public " in tail else False
                found.append((verb, path, anon, os.path.relpath(f, root)))
    return found


def selftest():
    ok = 0
    cases = [
        ('{"path":"/api/v1/x/{id}"}', norm_path("/api/v1/x/{id:guid}"), "/api/v1/x/{id}"),
    ]
    for _, got, expect in cases:
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] norm_path: %s != %s" % (got, expect))
    if norm_path("/api/v1/journal/records/") == "/api/v1/journal/records":
        ok += 1
    else:
        print("  [FAIL] слэш в конце")
    if ("POST", "/api/v1/clients") in ANON_ALLOW and ("GET", "/api/v1/clients") not in ANON_ALLOW:
        ok += 1
    else:
        print("  [FAIL] allow-list")
    print("check_api_index --selftest: %d/3" % ok)
    return 0 if ok == 3 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--index", default=None)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()

    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    index_path = a.index or os.path.join(root, INDEX)
    if not os.path.exists(index_path):
        print("::error::нет %s — гейт не может быть зелёным без индекса" % INDEX)
        return 1

    ops = parse_index(index_path)
    idx = {(o.get("method", "").upper(), norm_path(o.get("path", ""))) for o in ops if o.get("method")}
    code = parse_code(root)
    code_set = {(v, p) for v, p, _, _ in code}
    bad = 0

    missing_in_index = sorted(code_set - idx)
    extra_in_index = sorted(idx - code_set)
    for v, p in missing_in_index[:20]:
        print("::error::в коде есть, в индексе нет: %s %s" % (v, p))
        bad += 1
    for v, p in extra_in_index[:20]:
        print("::error::в индексе есть, в коде нет: %s %s" % (v, p))
        bad += 1

    no_codes = [o for o in ops if not (o.get("codes") or "").strip()]
    for o in no_codes[:10]:
        print("::error::%s %s — не заполнен codes" % (o.get("method"), o.get("path")))
        bad += len(no_codes) and 1

    anon_idx = {(o.get("method", "").upper(), norm_path(o.get("path", "")))
                for o in ops if (o.get("auth") or "").strip() == "anonymous"}
    anon_code = {(v, p) for v, p, anon, _ in code if anon}
    if anon_idx != ANON_ALLOW:
        print("::error::анонимность в индексе != allow-list: индекс=%s" % sorted(anon_idx))
        bad += 1
    if anon_code != ANON_ALLOW:
        print("::error::анонимность в КОДЕ != allow-list: код=%s" % sorted(anon_code))
        bad += 1

    print("check_api_index: индекс %d операций, код %d маршрутов, расхождений %d"
          % (len(idx), len(code_set), bad))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
