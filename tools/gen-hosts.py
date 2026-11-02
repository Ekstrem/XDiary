#!/usr/bin/env python3
"""Единый источник списков сервисов и `--add-host` (DO-034).

Проблема, которую это лечит: список сервисов был перечислен руками в ТРЁХ местах —
`update.sh` (`SVCS`), `deploy.sh` (`--add-host` для шлюза) и `heal-network.sh`
(`--add-host`). В `update.sh` забыли `clients` → образ клиента не тянулся 18 часов,
и `front-e2e` падал с 401 на `/clients/me` (BE-038 не доезжал).

Источник правды по СПИСКУ — `7-deploy/podman-compose.yml` (`services:`).
Источник правды по IP — карта `SERVICE_IPS` ниже (единственное место с адресами).

Запуск:
  python tools/gen-hosts.py --svcs       # "journal partners ... clients"
  python tools/gen-hosts.py --add-host   # "--add-host journal:10.89.9.20 ..."
  python tools/gen-hosts.py --check      # сверить, что все compose-сервисы имеют IP
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COMPOSE = os.path.join(ROOT, "7-deploy", "podman-compose.yml")

# ЕДИНСТВЕННОЕ место с IP-адресами. Скрипты берут их только отсюда.
SERVICE_IPS = {
    "journal": "10.89.9.20",
    "partners": "10.89.9.21",
    "catalog": "10.89.9.22",
    "somatic-series": "10.89.9.23",
    "daily-metrics": "10.89.9.24",
    "garmin-gateway": "10.89.9.25",
    "apple-health-gateway": "10.89.9.26",
    "analytics": "10.89.9.27",
    "privacy": "10.89.9.28",
    "portability": "10.89.9.29",
    "api-gateway": "10.89.9.30",
    "clients": "10.89.9.31",
}


def compose_services():
    """Список сервисов из compose. На сервере PyYAML может не быть — тогда парсим сами."""
    text = open(COMPOSE, encoding="utf-8").read()
    try:
        import yaml

        return list(yaml.safe_load(text).get("services", {}).keys())
    except ImportError:
        pass
    import re

    svcs, in_services = [], False
    for line in text.splitlines():
        if line.rstrip() == "services:":
            in_services = True
            continue
        if not in_services:
            continue
        if line and not line.startswith(" "):
            break
        m = re.match(r"^  ([a-z][a-z0-9-]*):\s*$", line)
        if m:
            svcs.append(m.group(1))
    return svcs


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    try:
        svcs = compose_services()
    except OSError as e:
        sys.exit(f"нет {COMPOSE}: {e}")

    missing = [s for s in svcs if s not in SERVICE_IPS]
    if missing:
        print(f"gen-hosts: нет IP для {missing} — добавь в SERVICE_IPS", file=sys.stderr)
        return 1

    if "--check" in args:
        extra = [s for s in SERVICE_IPS if s not in svcs]
        if extra:
            print(f"gen-hosts: в compose нет сервисов {extra} (есть в SERVICE_IPS)", file=sys.stderr)
            return 1
        print(f"gen-hosts: OK — {len(svcs)} сервисов, у всех есть IP")
        return 0

    if "--add-host" in args:
        # шлюзу не нужен --add-host на самого себя
        print(" ".join(f"--add-host {s}:{SERVICE_IPS[s]}" for s in svcs if s != "api-gateway"))
    elif "--svcs" in args:
        print(" ".join(svcs))
    else:
        print(" ".join(svcs))
    return 0


if __name__ == "__main__":
    sys.exit(main())
