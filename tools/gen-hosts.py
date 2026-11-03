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
  python tools/gen-hosts.py --images     # образы ИЗ COMPOSE (clients -> xdiary-client)
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

# ЕДИНСТВЕННОЕ место с псевдонимами имён (DO-051, @TL 11:45).
# Сервис в compose называется `clients`, контейнер — `xdiary-client`, код шлюза ходит по
# `clients`. Пока это знание жило в двух скриптах, пересоздание шлюза «по именам
# контейнеров» теряло `clients` → `/api/v1/clients` = 502 (прод, 2026-11-10).
ALIASES = {
    "clients": ("client",),   # clients -> дополнительно client
}


def hosts_for(services):
    """[(имя, IP)] для --add-host: сервисы + псевдонимы, без api-gateway."""
    pairs = []
    for svc in services:
        if svc == "api-gateway" or svc not in SERVICE_IPS:
            continue
        pairs.append((svc, SERVICE_IPS[svc]))
        for alias in ALIASES.get(svc, ()):
            pairs.append((alias, SERVICE_IPS[svc]))
    return pairs


def compose_images():
    """Карта service -> image из compose. Причина (P0 03.11): имя образа НЕ всегда
    равно `xdiary-<service>` — сервис `clients` использует образ `xdiary-client`.
    `update.sh` собирал имя из сервиса и получал 404 на пулле (ошибка глоталась
    `|| true`), поэтому контейнер клиента 33 часа жил на старом образе, и правки
    @BE не доезжали до прода."""
    text = open(COMPOSE, encoding="utf-8").read()
    try:
        import yaml

        svcs = yaml.safe_load(text).get("services", {}) or {}
        return {k: (v or {}).get("image", "") for k, v in svcs.items() if (v or {}).get("image")}
    except ImportError:
        pass
    import re

    images, cur, in_services = {}, None, False
    for line in text.splitlines():
        if line.rstrip() == "services:":
            in_services = True
            continue
        if not in_services:
            continue
        m = re.match(r"^  ([a-z0-9-]+):\s*$", line)
        if m:
            cur = m.group(1)
            continue
        m = re.match(r"^\s+image:\s*(\S+)", line)
        if m and cur:
            images[cur] = m.group(1).strip('"\'')
    return images


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
            # Не ошибка: IP есть в SERVICE_IPS, --add-host их выдаст. Это дрейф compose
            # (DO-051) — сообщаем громко, но не блокируем пересоздание шлюза.
            print(f"::warning::gen-hosts: в compose нет сервисов {extra} (есть в SERVICE_IPS)")
        pairs = hosts_for(sorted(set(svcs) | set(SERVICE_IPS)))
        if "clients" not in [n for n, _ in pairs]:
            print("gen-hosts: ОШИБКА — в списке нет псевдонима `clients`", file=sys.stderr)
            return 1
        print(f"gen-hosts: OK — {len(svcs)} сервисов, --add-host {len(pairs)} записей, `clients` есть")
        return 0

    if "--add-host" in args:
        # Объединение: compose может НЕ содержать `clients` (прод, дрейф DO-051), но IP
        # для него есть в SERVICE_IPS — иначе шлюз теряет псевдоним и вход ломается.
        unknown = [s for s in svcs if s not in SERVICE_IPS]
        if unknown:
            print(f"gen-hosts: нет IP для {unknown} — добавь в SERVICE_IPS", file=sys.stderr)
            return 1
        pairs = hosts_for(sorted(set(svcs) | set(SERVICE_IPS)))
        print(" ".join(f"--add-host {name}:{ip}" for name, ip in pairs))
    elif "--images" in args:
        imgs = compose_images()
        print(" ".join(imgs[s] for s in svcs if s in imgs))
    elif "--svcs" in args:
        print(" ".join(svcs))
    else:
        print(" ".join(svcs))
    return 0


if __name__ == "__main__":
    sys.exit(main())
