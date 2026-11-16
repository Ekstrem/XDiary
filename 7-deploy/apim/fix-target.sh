#!/usr/bin/env bash
# fix-target.sh — точечная правка backend у API journal/partners в Gravitee APIM (DO-134 шаг 2).
#
# Зачем отдельным маленьким скриптом: `envs:` у ssh-action упёрся в ARG_MAX, а import.sh
# (~27 КБ) через него больше не проходит. Здесь только нужное: найти API по listener-пути
# /api/v1/<svc>, показать текущий target, поставить http://<svc>:8080, перезапустить API.
#
# Запуск на VPS-1:  bash /opt/xdiary/7-deploy/apim/fix-target.sh [journal partners]
set -uo pipefail

MG="${XDIARY_APIM_MGMT:-xdiary-apim-management}"
ORG="${XDIARY_APIM_ORG:-DEFAULT}"
ENV_ID="${XDIARY_APIM_ENV:-DEFAULT}"
SECRETS="${XDIARY_SECRETS:-/opt/xdiary/secrets/apim.env}"
BASE="http://127.0.0.1:8083"
APIS="/management/v2/organizations/$ORG/environments/$ENV_ID/apis"

USER="admin"; PASS="admin"
if [ -f "$SECRETS" ]; then
  u=$(grep -E '^GRAVITEE_ADMIN_USER=' "$SECRETS" | cut -d= -f2-); [ -n "${u:-}" ] && USER="$u"
  p=$(grep -E '^GRAVITEE_ADMIN_PASSWORD=' "$SECRETS" | cut -d= -f2-); [ -n "${p:-}" ] && PASS="$p"
fi
AUTH="$USER:$PASS"

call() { # method path [body-file] -> "<code>"; тело в /tmp/ft.body
  local m="$1" path="$2" body="${3:-}"
  if [ -n "$body" ]; then
    podman cp "$body" "$MG:/tmp/ft.json" >/dev/null 2>&1
    podman exec "$MG" curl -s -m 60 -X "$m" -u "$AUTH" -H 'Content-Type: application/json' \
      --data-binary @/tmp/ft.json -o /tmp/ft.body -w '%{http_code}' "$BASE$path" 2>/dev/null
  else
    podman exec "$MG" curl -s -m 60 -X "$m" -u "$AUTH" -H 'Content-Type: application/json' \
      -o /tmp/ft.body -w '%{http_code}' "$BASE$path" 2>/dev/null
  fi
}
body() { podman exec "$MG" cat /tmp/ft.body 2>/dev/null; }

list_ids() { # Gravitee отдаёт страницы по 10
  local page=1 out ids=""
  while [ "$page" -le 40 ]; do
    out=$(podman exec "$MG" curl -s -m 30 -u "$AUTH" "$BASE$APIS?page=$page&per_page=10" 2>/dev/null)
    chunk=$(printf '%s' "$out" | tr -d '\n' | grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' | sed 's/.*"\([^"]*\)"$/\1/')
    [ -n "$chunk" ] || break
    ids="$ids $chunk"
    page=$((page+1))
  done
  printf '%s' "$ids"
}

TARGETS="${*:-journal partners}"
echo "fix-target: сервисы:$TARGETS"
for svc in $TARGETS; do
  want="/api/v1/$svc"
  found=""
  for id in $(list_ids); do
    c=$(call GET "$APIS/$id")
    [ "$c" = "200" ] || continue
    if body | tr -d '\n' | grep -q "\"path\"[[:space:]]*:[[:space:]]*\"$want\""; then found="$id"; break; fi
  done
  if [ -z "$found" ]; then
    echo "  $svc: по listener $want не нашёл — ищу по имени"
    for id in $(list_ids); do
      c=$(call GET "$APIS/$id"); [ "$c" = "200" ] || continue
      nm=$(body | tr -d '\n' | sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
      case "$nm" in *"$(printf '%s' "$svc" | sed 's/^./\U&/')"*) found="$id"; echo "    по имени: ${id:0:8}… \"$nm\""; break ;; esac
    done
  fi
  if [ -z "$found" ]; then echo "  $svc: записи нет ни по listener, ни по имени"; continue; fi
  call GET "$APIS/$found" >/dev/null
  cur=$(body | tr -d '\n' | sed -n 's/.*"target"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "  $svc: API ${found:0:8}… target сейчас: \"$cur\""
  body | sed "s|\"target\"[[:space:]]*:[[:space:]]*\"[^\"]*\"|\"target\" : \"http://xdiary-$svc:8080\"|g" > "/tmp/ft.$svc.put"
  n=$(grep -c '"target"' "/tmp/ft.$svc.put" || true)
  echo "    целей в теле: $n"
  echo "    PUT: $(call PUT "$APIS/$found" "/tmp/ft.$svc.put")"
  echo "    _stop: $(call POST "$APIS/$found/_stop")  _start: $(call POST "$APIS/$found/_start")"
done
echo "fix-target: готово"
