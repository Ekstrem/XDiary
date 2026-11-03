#!/usr/bin/env bash
# Самовосстановление rootless podman CNI (DO-010, вариант B).
# rootless podman 3.4.4 периодически теряет iptables-правила при redeploy.
# Этот скрипт идемпотентно возвращает: 1) rshared-монтирование /run,
# 2) masquerade для подсети xdiary, 3) портмап шлюза (пересоздаёт контейнер,
# если /health не отвечает). Вызывается из update.sh после каждого цикла
# авто-деплоя (systemd user timer, каждые 15 мин).
set -uo pipefail
LOG=/home/deploy/heal.log
ts() { date +%F_%T; }

# 1. rshared mount (теряется после перезапуска podman-сервисов)
if ! findmnt -no PROPAGATION /run/user/1000 2>/dev/null | grep -q shared; then
  sudo mount --make-rshared /run >/dev/null 2>&1
  sudo mount --make-rshared /run/user/1000 >/dev/null 2>&1
  echo "$(ts) fixed rshared /run" >> "$LOG"
fi

# 2. masquerade для 10.89.9.0/24
if ! sudo iptables -t nat -C POSTROUTING -s 10.89.9.0/24 ! -d 10.89.9.0/24 -j MASQUERADE >/dev/null 2>&1; then
  sudo iptables -t nat -A POSTROUTING -s 10.89.9.0/24 ! -d 10.89.9.0/24 -j MASQUERADE >/dev/null 2>&1
  echo "$(ts) fixed masquerade" >> "$LOG"
fi

# 3. портмап шлюза: /health не отвечает -> пересоздать шлюз (re-trigger portmap)
if ! curl -fsS -m 5 http://127.0.0.1:8080/health >/dev/null 2>&1; then
  echo "$(ts) gateway /health failed, recreating" >> "$LOG"
  # DO-034: список хостов берём из gen-hosts.py (он читает podman-compose.yml),
  # а не хардкодим — иначе снова забудем сервис, как забыли clients.
  # DO-050: раньше здесь было `2>/dev/null` — при отсутствии генератора gateway
# пересоздавался БЕЗ --add-host, и резолв внутри сети разваливался молча.
GEN_DIR="$(cd "$(dirname "$0")" && pwd)"
GEN=""
for cand in "$GEN_DIR/gen-hosts.py" "$GEN_DIR/../tools/gen-hosts.py" "/opt/xdiary/tools/gen-hosts.py"; do
  [ -f "$cand" ] && { GEN="$cand"; break; }
done
if [ -z "$GEN" ]; then
  echo "heal-network: не найден gen-hosts.py — пересоздавать gateway без --add-host нельзя" >&2
  exit 1
fi
ADD_HOSTS="$(python3 "$GEN" --add-host)"
# Страховка от дрейфа compose на сервере: если развёрнутый podman-compose.yml старее
# репозитория и в нём нет `clients`, генератор не вернёт этот хост — gateway
# пересоздастся без маршрута к клиентскому сервису. Именно так мы уже ловили
# «clients не обновляется» (P0 03.11). Добавляем явно.
case " $ADD_HOSTS " in
  *"clients:"*) ;;
  *) ADD_HOSTS="$ADD_HOSTS --add-host clients:10.89.9.31"
     echo "heal-network: в compose нет clients — хост добавлен явно" >&2 ;;
esac
if [ -z "$ADD_HOSTS" ]; then
  echo "heal-network: gen-hosts.py вернул пустой список --add-host — выхожу" >&2
  exit 1
fi
  if [ -z "$ADD_HOSTS" ]; then
    echo "$(ts) gen-hosts недоступен — НЕ пересоздаю шлюз (иначе потеряю сервисы)" >> "$LOG"
    exit 0
  fi
  podman rm -f xdiary-api-gateway >/dev/null 2>&1
  cd /opt/xdiary/7-deploy || exit 1
  podman run -d --name xdiary-api-gateway --network xdiary --ip 10.89.9.30 --hostname api-gateway \
    $ADD_HOSTS --label io.containers.autoupdate=registry \
    --env-file secrets/.env -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 \
    --read-only --tmpfs /tmp --restart unless-stopped -p 127.0.0.1:8080:8080 \
    ghcr.io/ekstrem/xdiary-api-gateway:single >/dev/null 2>&1
  sleep 8
  if curl -fsS -m 5 http://127.0.0.1:8080/health >/dev/null 2>&1; then
    echo "$(ts) gateway healed" >> "$LOG"
  else
    echo "$(ts) gateway STILL broken after recreate" >> "$LOG"
  fi
fi
