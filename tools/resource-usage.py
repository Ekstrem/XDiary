#!/usr/bin/env python3
"""Сводка расхода ресурсов по средам (prod/dev) → 7-deploy/RESOURCE-USAGE.md.

Собирает с VPS-1 (prod) и VPS-2 (dev): CPU/RAM/диск/load + `podman stats`
по каждому контейнеру и пишет Markdown-снимок. Запускать локально:
    python tools/resource-usage.py
Требует paramiko и SSH-ключи из ~/.ssh (см. TARGETS ниже).
"""
import os
import sys
from datetime import datetime, timezone

import paramiko

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "7-deploy", "RESOURCE-USAGE.md")

# (label, host, user, keyfile, sudo_prefix)
TARGETS = [
    ("PROD (VPS-1)", "31.192.111.168", "deploy",
     os.path.expanduser("~/.ssh/id_ed25519_xdiary"), ""),
    ("DEV (VPS-2)", "77.91.73.67", "ubuntu",
     os.path.expanduser("~/.ssh/id_ed25519"), "sudo "),
]


def run(c, cmd, t=90):
    _, o, e = c.exec_command(cmd, timeout=t)
    return o.read().decode("utf-8", "ignore").strip(), e.read().decode("utf-8", "ignore").strip()


def parse_mb(s):
    try:
        s = s.lower()
        if s.endswith("gb"):
            return float(s[:-2]) * 1024
        if s.endswith("mb"):
            return float(s[:-2])
        if s.endswith("kb"):
            return float(s[:-2]) / 1024
        return 0.0
    except (ValueError, TypeError):
        return 0.0


def gather(host, user, keyfile, sudo):
    key = paramiko.Ed25519Key.from_private_key_file(keyfile)
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(host, username=user, pkey=key, timeout=25,
              allow_agent=False, look_for_keys=False)
    cpu = run(c, "nproc")[0]
    mem = run(c, "free -h | awk '/Mem:/{print $2\" total / \"$3\" used / \"$7\" avail\"}'")[0]
    disk = run(c, "df -h / | awk 'NR==2{print $3\" used / \"$2\" total\"}'")[0]
    load = run(c, "uptime | awk -F'load average:' '{print $2}'")[0].strip()
    o, _ = run(c, f"{sudo}podman stats --no-stream 2>&1")
    rows = []
    for ln in o.splitlines():
        p = ln.split()
        if len(p) >= 7 and p[1].startswith("xdiary-"):
            rows.append((p[1], p[2], p[3], p[5], p[6], parse_mb(p[3])))
    c.close()
    return cpu, mem, disk, load, rows


def render_block(label, host, cpu, mem, disk, load, rows):
    rows = sorted(rows, key=lambda r: -r[5])
    total = sum(r[5] for r in rows)
    lines = [f"## {label}", ""]
    lines.append(f"- **Хост:** `{host}`")
    lines.append(f"- **CPU:** {cpu} vCPU")
    lines.append(f"- **RAM:** {mem}")
    lines.append(f"- **Диск `/`:** {disk}")
    lines.append(f"- **Load:** {load}")
    lines.append(f"- **Суммарно контейнеры:** ~{total:.0f} MB")
    lines.append("")
    lines.append("| Контейнер | CPU% | MEM | Limit% |")
    lines.append("|---|---|---|---|")
    for name, cpup, mu, _ml, mp, _mb in rows:
        mark = " 🗄️" if name == "xdiary-pg" else ""
        lines.append(f"| `{name}`{mark} | {cpup} | {mu} | {mp} |")
    return "\n".join(lines)


def main():
    blocks = []
    for label, host, user, keyfile, sudo in TARGETS:
        cpu, mem, disk, load, rows = gather(host, user, keyfile, sudo)
        blocks.append(render_block(label, host, cpu, mem, disk, load, rows))

    ts = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    content = (
        "# Расход ресурсов (prod/dev)\n\n"
        "> Авто-снимок `tools/resource-usage.py` — **не редактировать вручную**.\n"
        f"> Обновлён: {ts}\n\n"
        + "\n\n".join(blocks)
        + "\n"
    )
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(content)
    print("written %s" % os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
