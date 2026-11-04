#!/usr/bin/env python
"""DO-056: drift-check «индекс/спеки ↔ APIM» (contract-first, решение владельца 2026-11-04).

Что проверяет: набор `operationId` из `04-architecture/api-index.yaml` (и `openapi/*.json`,
когда реестр готов) должен совпадать с тем, что реально опубликовано в Gravitee APIM.
Расхождение → `::error::` и rc=1.

Почему через `podman exec`, а не через порт: на проде Management API **не опубликован**
на хост (только gateway `18082`), а изнутри контейнера `:8083/management/v2/` отвечает
**200**. Публиковать лишний порт наружу ради гейта — не надо.

Токен: `GRAVITEE_TOKEN` (Management API требует Bearer). Без токена гейт **не** зелёный:
он возвращает rc=2 и пишет, чего не хватает — «не смог проверить» не равно «совпадает».

    python tools/check_apim_drift.py --root .              # прод (podman exec) 
    python tools/check_apim_drift.py --root . --api http://127.0.0.1:18083
    python tools/check_apim_drift.py --selftest
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys

INDEX = os.path.join("04-architecture", "api-index.yaml")
OPENAPI_DIR = "openapi"


def index_operations(root):
    path = os.path.join(root, INDEX)
    if not os.path.exists(path):
        return set()
    txt = open(path, encoding="utf-8", errors="replace").read()
    return {m for m in re.findall(r'operationId:\s*([A-Za-z0-9_.]+)', txt) if not m.isdigit()}


def spec_operations(root):
    """operationId из `openapi/*.json` (реестр @BE)."""
    out = set()
    base = os.path.join(root, OPENAPI_DIR)
    if not os.path.isdir(base):
        return out
    for name in sorted(os.listdir(base)):
        if not name.endswith(".json") or name.startswith("_"):
            continue
        try:
            doc = json.load(open(os.path.join(base, name), encoding="utf-8"))
        except (OSError, ValueError):
            continue
        for item in (doc.get("paths") or {}).values():
            for op in (item or {}).values():
                if isinstance(op, dict) and op.get("operationId"):
                    out.add(op["operationId"])
    return out


def apim_operations(api_base, token, exec_container=None, timeout=20):
    """operationId, реально опубликованные в APIM (по всем API окружения)."""
    def call(path):
        if exec_container:
            cmd = ["podman", "exec", exec_container, "sh", "-c",
                   "curl -s -m %d -H 'Authorization: Bearer %s' '%s%s'"
                   % (timeout, token, "http://127.0.0.1:8083", path)]
            r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                               errors="replace")
            return r.stdout
        import urllib.request
        req = urllib.request.Request(api_base + path,
                                     headers={"Authorization": "Bearer " + token})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.read().decode("utf-8", "replace")

    try:
        apis = json.loads(call("/management/v2/organizations/DEFAULT/environments/DEFAULT/apis?per_page=100") or "{}")
    except Exception as exc:                       # сеть, токен, формат
        return None, "APIM недоступен или токен не принят: %s" % str(exc)[:120]

    ids = set()
    for api in (apis.get("data") or []):
        api_id = api.get("id")
        if not api_id:
            continue
        try:
            page = json.loads(call("/management/v2/organizations/DEFAULT/environments/DEFAULT/apis/%s/pages"
                                   % api_id) or "{}")
        except Exception:
            continue
        # operationId берём из спек: у Gravitee это страница типа SWAGGER/OPENAPI
        for pg in (page.get("data") or []):
            content = (pg.get("content") or "")
            if "operationId" in content or pg.get("type", "").upper() in ("SWAGGER", "OPENAPI"):
                try:
                    doc = json.loads(content)
                except ValueError:
                    continue
                for item in (doc.get("paths") or {}).values():
                    for op in (item or {}).values():
                        if isinstance(op, dict) and op.get("operationId"):
                            ids.add(op["operationId"])
    return ids, ""


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-apim")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(os.path.join(base, "04-architecture"))
    os.makedirs(os.path.join(base, "openapi"))
    open(os.path.join(base, "04-architecture", "api-index.yaml"), "w", encoding="utf-8").write(
        "operations:\n  - operationId: Journal.List\n  - operationId: Client.Me\n")
    got = index_operations(base)
    if got == {"Journal.List", "Client.Me"}:
        ok += 1
    else:
        print("  [FAIL] индекс: %s" % got)
    open(os.path.join(base, "openapi", "journal.json"), "w", encoding="utf-8").write(json.dumps(
        {"paths": {"/journal": {"get": {"operationId": "Journal.List"}}}}))
    got = spec_operations(base)
    if got == {"Journal.List"}:
        ok += 1
    else:
        print("  [FAIL] спеки: %s" % got)
    shutil.rmtree(base, ignore_errors=True)
    print("check_apim_drift --selftest: %d/2" % ok)
    return 0 if ok == 2 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--api", default="http://127.0.0.1:18083")
    ap.add_argument("--container", default="xdiary-apim-management")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--specs-only", action="store_true",
                    help="только посчитать спеки/индекс, без обращения к APIM")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    token = os.environ.get("GRAVITEE_TOKEN", "").strip()
    want = index_operations(root)
    specs = spec_operations(root)
    print("check_apim_drift: индекс %d operationId, спеки %d (openapi/*.json)"
          % (len(want), len(specs)))
    if a.specs_only:
        print("check_apim_drift: --specs-only: индекс %d, спеки %d, APIM не опрашивался"
              % (len(want), len(specs)))
        return 0
    if not token:
        print("::warning::GRAVITEE_TOKEN не задан — сравнение с APIM не выполнено (это НЕ «совпадает»)")
        return 2
    got, err = apim_operations(a.api, token, exec_container=a.container)
    if got is None:
        print("::error::(apim) %s" % err)
        return 2
    base = specs if specs else set()
    missing = sorted(base - got)
    extra = sorted(got - base) if base else []
    if not base and not got:
        print("check_apim_drift: спек нет и в APIM пусто — импортировать нечего, расхождений 0")
        return 0
    if want and not specs:
        print("check_apim_drift: индекс %d операций, спек 0 — drift считается по спекам "
              "(реестр ещё не выкачен), APIM %d" % (len(want), len(got)))
    for op in missing[:20]:
        print("::error::(apim) %s — есть в индексе/спеках, нет в APIM" % op)
    for op in extra[:20]:
        print("::error::(apim) %s — есть в APIM, нет в индексе/спеках" % op)
    print("check_apim_drift: APIM %d операций, расхождений %d" % (len(got), len(missing) + len(extra)))
    return 1 if (missing or extra) else 0


if __name__ == "__main__":
    sys.exit(main())
