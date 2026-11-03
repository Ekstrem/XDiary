#!/usr/bin/env bash
# flaky-gateway.sh — ДЕТЕРМИНИРОВАННЫЙ контроль апстрима для B7 (502/504), @TL 01:21.
#
# Способ: не гасим сервис (это ломает соседей и оставляет «а вдруг не вернул»),
# а поднимаем ВТОРОЙ шлюз рядом (порт 18080) с копией конфига, где один кластер
# (по умолчанию `clients` — маршрут анонимный, токен для пробы не нужен) смотрит
# в заведомо мёртвый адрес http://127.0.0.1:1. Рабочий шлюз на 8080 не трогается.
#
#   ./flaky-gateway.sh up        поднять мёртвый апстрим (ждёт 502)
#   ./flaky-gateway.sh down      убрать (возврат к норме)
#   ./flaky-gateway.sh check     одна строка: код и время ответа
set -euo pipefail
cd "$(dirname "$0")"

NAME=xdiary-gw-deadupstream
PORT=18080
CFG_DIR=/opt/xdiary/testcfg
PROD_GW=xdiary-api-gateway
UPSTREAM="${1:-up}"
CLUSTER="${CLUSTER:-clients}"
DEAD=http://127.0.0.1:1

up() {
  mkdir -p "$CFG_DIR"
  # конфиг берём из ЖИВОГО шлюза, чтобы тест шёл по той же конфигурации, что прод/дев
  # ВАЖНО: маршруты живут в appsettings.json, а appsettings.Single.json их только
  # дополняет (там Services/Jwt). Копируем ОБА и правим базовый — иначе KeyError.
  for f in appsettings.json appsettings.Single.json; do
    podman cp "$PROD_GW:/app/$f" "$CFG_DIR/$f" >/dev/null 2>&1 \
      || podman exec "$PROD_GW" cat "/app/$f" > "$CFG_DIR/$f"
  done
  python3 - "$CFG_DIR/appsettings.json" "$CLUSTER" "$DEAD" <<'PY'
import json, sys
path, cluster, dead = sys.argv[1:4]
d = json.load(open(path, encoding="utf-8"))
clusters = d.get("ReverseProxy", {}).get("Clusters", {})
if cluster not in clusters:
    raise SystemExit("нет кластера %r; есть: %s" % (cluster, ", ".join(sorted(clusters))))
dest = clusters[cluster].setdefault("Destinations", {})
key = next(iter(dest))
dest[key]["Address"] = dead + "/"
json.dump(d, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("  кластер %s -> %s (мёртвый апстрим)" % (cluster, dead))
PY
  podman rm -f "$NAME" >/dev/null 2>&1 || true
  podman run -d --name "$NAME" --network bridge --memory=256m \
    -p "127.0.0.1:$PORT:8080" \
    -v "$CFG_DIR/appsettings.json:/app/appsettings.json:ro" \
    -v "$CFG_DIR/appsettings.Single.json:/app/appsettings.Single.json:ro" \
    ghcr.io/ekstrem/xdiary-api-gateway:single >/dev/null
  echo "  поднят $NAME на 127.0.0.1:$PORT (рабочий шлюз на 8080 не тронут)"
}

down() {
  podman rm -f "$NAME" >/dev/null 2>&1 || true
  echo "  $NAME убран"
}

check() {
  local out code t
  out=$(curl -s -o /dev/null -w '%{http_code} %{time_total}' -m 20 -X POST \
        -H 'Content-Type: application/json' -d '{}' "http://127.0.0.1:$PORT/api/v1/clients")
  code=${out%% *}
  t=${out##* }
  echo "  POST /api/v1/clients -> HTTP $code за ${t}s (ожидание B7: 502, НЕ 504 по таймауту)"
}

case "$UPSTREAM" in
  up) up ;;
  down) down ;;
  check) check ;;
  *) echo "usage: $0 up|down|check" >&2; exit 2 ;;
esac
