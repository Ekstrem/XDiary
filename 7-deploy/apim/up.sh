#!/usr/bin/env bash
# up.sh — поднять APIM этап 1 (DO-056) с РАБОЧИМ адресом Mongo.
#
# Зачем не просто `podman-compose up`: в сети, созданной podman-compose, CNI-DNS
# не резолвит имена контейнеров (`getent hosts xdiary-apim-mongo` → пусто,
# `nc: bad address`) — та же болезнь, что днём с короткими именами в основной сети.
# Поэтому: поднимаем mongo, берём его IP из podman и подставляем в URI.
set -euo pipefail
cd "$(dirname "$0")"

echo "1) mongo"
podman-compose -f gravitee-compose.yml up -d mongo
for i in $(seq 1 30); do
  IP="$(podman inspect xdiary-apim-mongo --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' 2>/dev/null || true)"
  [ -n "$IP" ] && break
  sleep 1
done
[ -n "${IP:-}" ] || { echo "ошибка: не получил IP mongo" >&2; exit 1; }
echo "   IP mongo: $IP"

echo "2) подставляю IP в URI (файл — источник правды в репозитории)"
sed -i -E "s#mongodb://[^/]+:27017#mongodb://$IP:27017#g" gravitee-compose.yml

echo "3) management-api и gateway"
podman-compose -f gravitee-compose.yml up -d --force-recreate management-api gateway

echo "4) ждём готовности management-api (до 120 c)"
for i in $(seq 1 24); do
  CODE="$(curl -s -o /dev/null -w '%{http_code}' -m 5 http://127.0.0.1:18083/management/health 2>/dev/null || true)"
  if [ "$CODE" = "200" ]; then echo "   management-health: 200"; break; fi
  sleep 5
done
echo "   код management-health: ${CODE:-нет}"

echo "5) контроль: APIM НЕ в пути трафика"
echo "   YARP /api/v1/clients: $(curl -s -o /dev/null -w '%{http_code}' -m 8 http://127.0.0.1:8080/api/v1/clients)"
echo "   APIM gateway /:       $(curl -s -o /dev/null -w '%{http_code}' -m 8 http://127.0.0.1:18082/ 2>/dev/null)"
