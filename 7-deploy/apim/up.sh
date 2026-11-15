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

# ВАЖНО (2026-11-05, DO-076): стек СЕЙЧАС В ПУТИ ТРАФИКА
# `Funnel -> Caddy:8081 -> APIM:18082 -> сервисы`. Раньше скрипт поднимал
# «control plane вне пути» — это больше не так: упавший gateway = недоступный API.
# Проверка после запуска: `python3 tools/check_apim_health.py --remote deploy@<host>`.
# Вручную то же самое: `sudo tailscale funnel status` (ждём апстрим 8081) и
# `curl -H 'Host: <публичное имя>' http://127.0.0.1:8081/api/v1/catalog` -> 401 + `X-Gravitee-*`.
cd "$(dirname "$0")"

NET=apim
MONGO=xdiary-apim-mongo
UI=xdiary-apim-ui
PORTAL=xdiary-apim-portal-ui
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
  # DO-075: если в secrets лежит правленый gravitee.yml (пароль админа не заводской) —
  # монтируем его. Иначе пересоздание контейнера вернуло бы заводской admin/admin.
  local cfg=( -v /opt/xdiary/secrets/apim-gravitee.yml:/opt/graviteeio-management-api/config/gravitee.yml:ro -e GRAVITEE_CFG_FROM=secrets )
  if [ ! -f /opt/xdiary/secrets/apim-gravitee.yml ]; then cfg=(); fi
  podman run -d --name "$MGMT" --network "$NET" --memory=768m --restart unless-stopped "${cfg[@]}" \
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

run_ui() {
  # Management Console UI. Нужен для первичной установки: первый админ Gravitee 4.x
  # создаётся мастером консоли, а не инициализатором (в jar-ах его нет).
  # Порт только на loopback: UI не для внешнего мира, смотреть через ssh-туннель.
  podman rm -f "$UI" >/dev/null 2>&1 || true
  podman run -d --name "$UI" --network "$NET" --memory=256m --restart unless-stopped \
    -p 127.0.0.1:18084:8080 \
    docker.io/graviteeio/apim-management-ui:4.4 >/dev/null
  say "management-ui запущен: http://127.0.0.1:18084 (только loopback)"

# DO-075 (2026-11-06): портал для пользователей API — ОПЦИОНАЛЬНО, только loopback.
# Включается флагом --with-portal: без него контур поднимается как раньше (меньше памяти).
if printf '%s\n' "${ARGS:-$*}" | grep -q -- "--with-portal"; then
  podman rm -f "$PORTAL" >/dev/null 2>&1 || true
  podman run -d --name "$PORTAL" --network "$NET" --memory=256m --restart unless-stopped \
    -p 127.0.0.1:18085:8080 \
    -e MANAGEMENT_API_URL="http://$MGMT:8083" \
    -e MGMT_API_URL="http://localhost:8083/management" \
    -e PORTAL_BASE_HREF=/ \
    docker.io/graviteeio/apim-portal-ui:4.4 >/dev/null
  say "portal-ui запущен: http://127.0.0.1:18085 (только loopback)"
fi
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
echo "3b) management-ui"; run_ui
echo "4) gateway"; run_gw
echo "5) health management"; health
echo "6) контроль: APIM В ПУТИ ТРАФИКА (с 2026-11-05, DO-076)"
# DO-085 (этап 2 ADR-030): YARP-проба удалена — публичный путь API идёт через
# APIM/Caddy (18082/8081); проверка входа ниже это и подтверждает.
say "APIM gateway /:        $(curl -s -o /dev/null -w '%{http_code}' -m 8 http://127.0.0.1:18082/ || echo 000)"
# Публичный вход обязан идти через gateway: 401 + наличие X-Gravitee (на проде — Caddy :8081,
# на тесте/локально публичного входа в APIM может не быть — тогда здесь будет 000/404, и это
# означает «вход не переключён», а не «APIM не в пути»).
if [ -n "${PUBLIC_HOST:-}" ]; then
  hdr=$(curl -sS -D - -o /dev/null -m 8 -H "Host: $PUBLIC_HOST" http://127.0.0.1:8081/api/v1/catalog 2>/dev/null || true)
  code=$(printf '%s' "$hdr" | head -1 | awk '{print $2}')
  gcount=$(printf '%s' "$hdr" | grep -ci 'x-gravitee' || true)
  say "публичный вход /api/v1/catalog: ${code:-000}, x-gravitee: ${gcount:-0}"
  [ "${code:-000}" = "401" ] && [ "${gcount:-0}" -ge 1 ] \
    && say "вердикт: APIM в пути (401 + X-Gravitee)" \
    || say "вердикт: вход НЕ через APIM (ожидали 401 + X-Gravitee)"
else
  say "публичная проверка пропущена: задай PUBLIC_HOST=<публичное имя> для проверки входа"
fi
