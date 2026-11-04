#!/usr/bin/env python
"""Снять OpenAPI с ЖИВЫХ сервисов и собрать `openapi/*.json` (DO-056 / BE-050).

Зачем этот скрипт, а не `curl` руками: у .NET-образов нет `curl`, swagger отдаётся по IP
контейнера, а живой документ **не содержит `operationId`** (Swashbuckle его не генерирует).
Поэтому:

  1. снимаем живой `swagger/v1/swagger.json` с контейнера (по IP из `podman inspect`);
  2. переносим `operationId` из `04-architecture/api-index.yaml` **по паре (method, path)**;
  3. пишем `openapi/<сервис>.json` + обновляем `openapi/_meta.json`
     (`rev` = sha256 файла, `paths`, `title`, `source`, `measured_at`).

Так реестр остаётся «снятым с кода» (пути и схемы — из живого сервиса) и одновременно
годится для импорта в APIM (там нужен `operationId`).

    python tools/snapshot_openapi.py --dry-run
    python tools/snapshot_openapi.py --write --host dev
"""
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys

INDEX = os.path.join("04-architecture", "api-index.yaml")
OPENAPI = "openapi"
SERVICES = ("client", "journal", "partners", "catalog", "privacy", "somatic-series",
            "daily-metrics", "portability", "analytics", "api-gateway",
            "apple-health-gateway", "garmin-gateway")


def index_ops(root):
    """{(method, path): operationId} из api-index.yaml (записи вида `- bc/method/path/operationId`)."""
    path = os.path.join(root, INDEX)
    out = {}
    if not os.path.exists(path):
        return out
    cur = {}
    for line in open(path, encoding="utf-8", errors="replace").read().splitlines():
        m = re.match(r'\s*-?\s*(bc|method|path|operationId):\s*(.+?)\s*$', line)
        if not m:
            continue
        key, val = m.group(1), m.group(2).strip().strip('"\'')
        if key == "bc":
            cur = {}
        else:
            cur[key] = val
        if key == "operationId" and cur.get("method") and cur.get("path"):
            out[(cur["method"].lower(), cur["path"])] = cur["operationId"]
            cur = {}
    return out


def fetch_live(host, svc, timeout=15):
    """Живой swagger с контейнера через SSH (у образа нет curl)."""
    cmd = ("sudo -n podman inspect xdiary-%s --format "
           "'{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' 2>/dev/null | head -1" % svc)
    ip = subprocess.run(["ssh", "-o", "BatchMode=yes", host, cmd],
                        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()
    if not ip:
        return None, "нет IP контейнера"
    out = subprocess.run(["ssh", "-o", "BatchMode=yes", host,
                          "curl -s -m %d http://%s:8080/swagger/v1/swagger.json" % (timeout, ip)],
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    body = (out.stdout or "").strip()
    if not body.startswith("{"):
        return None, "swagger не отдался (HTTP/сеть)"
    try:
        return json.loads(body), ""
    except ValueError:
        return None, "swagger не JSON"


def inject_operation_ids(doc, ops):
    """Проставить operationId по (method, path); вернуть (сколько проставлено, сколько без пары)."""
    set_n = miss = 0
    for path, item in (doc.get("paths") or {}).items():
        for method, op in (item or {}).items():
            if not isinstance(op, dict):
                continue
            if op.get("operationId"):
                continue
            oid = ops.get((method.lower(), path))
            if oid:
                op["operationId"] = oid
                set_n += 1
            else:
                miss += 1
    return set_n, miss


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--host", default="ubuntu@77.91.73.67")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    root = os.path.abspath(a.root)
    ops = index_ops(root)
    print("индекс: %d операций с (method, path)" % len(ops))

    meta_path = os.path.join(root, OPENAPI, "_meta.json")
    meta = json.load(open(meta_path, encoding="utf-8")) if os.path.exists(meta_path) else {}

    measured = []
    skipped = []
    for svc in SERVICES:
        doc, err = fetch_live(a.host, svc)
        if doc is None:
            skipped.append((svc, err))
            print("  %-22s ПРОПУСК: %s" % (svc, err))
            continue
        set_n, miss = inject_operation_ids(doc, ops)
        text = json.dumps(doc, ensure_ascii=False, indent=2, sort_keys=False)
        paths = len(doc.get("paths") or {})
        rev = hashlib.sha256(text.encode("utf-8")).hexdigest()
        title = ((doc.get("info") or {}).get("title") or svc)
        measured.append((svc, paths, set_n, miss, rev[:12]))
        print("  %-22s путей=%-3d operationId проставлено=%-3d без пары=%-2d rev=%s"
              % (svc, paths, set_n, miss, rev[:12]))
        if a.write:
            open(os.path.join(root, OPENAPI, svc + ".json"), "w",
                 encoding="utf-8", newline="\n").write(text + "\n")
            meta[svc] = {"rev": rev, "paths": paths, "title": title,
                         "operations": paths, "source": "live-swagger",
                         "measured_at": "2026-11-04", "derived_from": INDEX}
    if a.write:
        for svc, err in skipped:
            meta.setdefault(svc, {})["source"] = "derived (не измерено): %s" % err
        open(meta_path, "w", encoding="utf-8", newline="\n").write(
            json.dumps(meta, ensure_ascii=False, indent=2) + "\n")
        print("_meta.json обновлён")
    print("снято: %d, пропущено: %d" % (len(measured), len(skipped)))
    return 0 if not skipped else 3


if __name__ == "__main__":
    sys.exit(main())
