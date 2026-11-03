#!/usr/bin/env bash
# up.sh — поднять APIM этап 1 (DO-056) так, как ПРОВЕРЕНО, а не как «красиво».
#
# Почему не `podman-compose up` (решение @TL 22:55, замеры @DO 2026-11-09):
#   1) compose 1.6.0 садит контейнеры в дефолтную сеть `podman`, игнорируя `external: true`
#      (`podman inspect` → `сети mongo: podman`);
#   2) при каждом `up` mongo пересоздаётся → IP меняется → management падает
#      `MongoTimeoutException` и уходит в петлю рестартов;
#   3) в этой сети CNI-DNS не резолвит имена (`getent hosts` → пусто, `nc: bad address`);
#   4) `podman network create` пишет conflist `cniVersion 1.0.0` + плагин `firewall`,
#      а установленный плагин понимает только `0.4.0` → контейнеры создаются и НЕ стартуют.
# Проверено: явный `podman run --network apim --memory=768m` доходит до инициализаторов.
# Поэтому compose остаётся описанием, а запуск — явный.
set -euo pipefail
cd "$(dirname "$0")"

NET=apim
MONGO=xdiary-apim-mongo
MGMT=xdiary-apim-management
GW=xdiary-apim-gateway
URI_TMPL='mongodb://%s:27017/gravitee?serverSelectionTimeoutMS=5000&connectTimeoutMS=5000'

say() { printf '  %s\n' "$*"; }

ensure_network() {
  if ! podman network exists "$NET" >/dev/null 2>&1; then
    mkdir -p "$HOME/.config/cni/net.d"
    cat > "$HOME/.config/cni/net.d/apim.conflist" <<'JSON'
{
  "cniVersion": "0.4.0",
  "name": "apim",
  "plugins": [
    {"type": "bridge", "bridge": "cni-podman10", "isGateway": true, "ipMasq": true, "hairpinMode": true,
     "ipam": {"type": "host-local", "routes": [{"dst": "0.0.0.0/0"}],
              "ranges": [[{"subnet": "10.89.10.0/24", "gateway": "10.89.10.1"}]]}},
    {"type": "portmap", "capabilities": {"portMappings": true}},
    {"type": "tuning"},
    {"type": "dnsname", "domainName": "dns.podman", "capabilities": {"aliases": true}}
  ]
}
JSON
    podman network create --subnet 10.89.10.0/24 "$NET" >/dev/null 2>&1 || true
  fi
  podman network exists "$NET" >/dev/null 2>&1 && say "сеть $NET есть" || { say "ошибка: сеть $NET не создана"; exit 1; }
}

mongo_ip() {
  podman inspect "$MONGO" --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' 2>/dev/null | tr -d '\n'
}

start_mongo() {
  if podman container exists "$MONGO" >/dev/null 2>&1; then
    podman start "$MONGO" >/dev/null 2>&1 || true
  else
    podman run -d --name "$MONGO" --network "$NET" --memory=512m --restart unless-stopped \
      docker.io/library/mongo:6 --wiredTigerCacheSizeGB 0.25 >/dev/null
  fi
  for _ in $(seq 1 30); do
    ip="$(mongo_ip)"
    if [ -n "$ip" ]; then say "mongo IP $ip"; return 0; fi
    sleep 1
  done
  say "ошибка: не получил IP mongo"; exit 1
}

run_mgmt() {
  local uri
  uri=$(printf "$URI_TMPL" "$(mongo_ip)")
  podman rm -f "$MGMT" >/dev/null 2>&1 || true
  # порт НЕ публикуем: health проверяется изнутри сети; публикация через rootlessport
  # на этой версии podman изредка роняет контейнер (проверено 2026-11-09)
  podman run -d --name "$MGMT" --network "$NET" --memory=768m --restart unless-stopped \
    -e gravitee_management_mongodb_uri="$uri" \
    -e gravitee_analytics_type=none \
    -e gravitee_reporters_elasticsearch_enabled=false \
    -e gravitee_services_core_http_port=8073 \
    docker.io/graviteeio/apim-management-api:4.4 >/dev/null
  say "management-api запущен (768m, сеть $NET)"
}

run_gw() {
  local uri
  uri=$(printf "$URI_TMPL" "$(mongo_ip)")
  podman rm -f "$GW" >/dev/null 2>&1 || true
  podman run -d --name "$GW" --network "$NET" --memory=512m --restart unless-stopped \
    -p 127.0.0.1:18082:8082 \
    -e gravitee_management_mongodb_uri="$uri" \
    -e gravitee_analytics_type=none \
    -e gravitee_reporters_elasticsearch_enabled=false \
    -e gravitee_services_core_http_port=8072 \
    -e gravitee_http_port=8082 \
    docker.io/graviteeio/apim-gateway:4.4 >/dev/null
  say "gateway запущен (512m, прокси 127.0.0.1:18082)"
}

health() {
  local mg_ip out=""
  mg_ip=$(podman inspect "$MGMT" --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' 2>/dev/null | tr -d '\n')
  for _ in $(seq 1 24); do
    out=$(podman exec "$GW" wget -q -O- "http://$mg_ip:8083/management/health" 2>/dev/null || true)
    case "$out" in *'"status"'*|*UP*) break;; esac
    sleep 5
  done
  say "management health: ${out:-нет ответа}"
}

echo "1) сеть"; ensure_network
echo "2) mongo"; start_mongo
echo "3) management-api"; run_mgmt
echo "4) gateway"; run_gw
echo "5) health management"; health
echo "6) контроль: APIM НЕ в пути трафика"
say "YARP /api/v1/clients: $(curl -s -o /dev/null -w '%{http_code}' -m 8 http://127.0.0.1:8080/api/v1/clients || echo 000)"
say "APIM gateway /:        $(curl -s -o /dev/null -w '%{http_code}' -m 8 http://127.0.0.1:18082/ || echo 000)"
