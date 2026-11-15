#!/usr/bin/env bash
# import.sh — импорт openapi/*.json в Gravitee APIM (contract-first).
#
# История: скрипт несколько раз правился патчами и в итоге получил ДВЕ петли по спекам и
# определение `apim_api_ids()` ПОСЛЕ первого использования — из-за этого вызов функции падал,
# снос видел «0 API», а импорт упирался в 400 `path.invalid` (прогоны 2026-11-15). Файл
# перезаписан целиком одним проходом (@TL), логика собрана в правильном порядке.
#
# Рабочая последовательность для Gravitee 4.4 (7-deploy/runbooks/apim.md:187-209):
#   1) POST …/apis/_import/swagger  {"payload": "<спека строкой>", "withDocumentation": true}
#   2) POST …/apis/{id}/plans        {"name":"Default","security":{"type":"KEY_LESS"},"mode":"STANDARD",
#                                     "validation":"MANUAL","definitionVersion":"V4","flows":[]}
#   3) POST …/apis/{id}/plans/{planId}/_publish
#   4) POST …/apis/{id}/_start       → 204     (без плана API не стартует: api.NotDeployable)
#
# Важно: старые пути (`/apis/import`) в 4.4 отдают 405; `servers` с корневым url ломает backend
# (AR-160), поэтому в payload он вырезается.
#
# Использование: bash import.sh [--dry-run] [--no-publish]
set -uo pipefail

ROOT_DIR="${XDIARY_ROOT:-/opt/xdiary}"
SECRETS="${XDIARY_SECRETS:-/opt/xdiary/secrets/apim.env}"
MG="${XDIARY_APIM_MGMT:-xdiary-apim-management}"
M="${XDIARY_APIM_MONGO:-xdiary-apim-mongo}"
ORG="${XDIARY_APIM_ORG:-DEFAULT}"
ENV_ID="${XDIARY_APIM_ENV:-DEFAULT}"
BASE="http://127.0.0.1:8083"
API_ATTEMPTS="${APIM_IMPORT_ATTEMPTS:-3}"

DRY=0
PUBLISH=1
for a in "$@"; do
  case "$a" in
    --dry-run) DRY=1 ;;
    --no-publish) PUBLISH=0 ;;
    *) echo "usage: $0 [--dry-run] [--no-publish]" >&2; exit 2 ;;
  esac
done

SPECS=$(ls "$ROOT_DIR"/openapi/*.json 2>/dev/null | grep -vE '/(index|_)' | sort)
[ -n "$SPECS" ] || { echo "import.sh: в $ROOT_DIR/openapi нет спек" >&2; exit 2; }

# креды: из secrets-файла, иначе штатные admin/admin (memory-провайдер gravitee.yml)
USER="admin"; PASS="admin"
if [ -f "$SECRETS" ]; then
  u=$(grep -E '^GRAVITEE_ADMIN_USER=' "$SECRETS" | cut -d= -f2-); [ -n "${u:-}" ] && USER="$u"
  P=$(grep -E '^GRAVITEE_ADMIN_PASSWORD=' "$SECRETS" | cut -d= -f2-); [ -n "${P:-}" ] && PASS="$P"
fi

AUTH="${USER:-admin}:${PASS:-admin}"
APIS_PATH="/management/v2/organizations/$ORG/environments/$ENV_ID/apis"

# --- helpers (объявлены ДО использования) -------------------------------------------------

# apim <method> <path> [body-file] → печатает "<code>\n<body>"
apim() {
  local method="$1" path="$2" body="${3:-}"
  if [ -n "$body" ]; then
    podman cp "$body" "$MG:/tmp/body.json" >/dev/null 2>&1 || true
    podman exec "$MG" curl -s -m 90 -X "$method" -u "$AUTH" \
      -H 'Content-Type: application/json' --data-binary @/tmp/body.json \
      -w '\n%{http_code}' "$BASE$path" 2>/dev/null
  else
    podman exec "$MG" curl -s -m 90 -X "$method" -u "$AUTH" \
      -H 'Content-Type: application/json' -w '\n%{http_code}' "$BASE$path" 2>/dev/null
  fi
}

body_of() { printf '%s' "$1" | sed '$d'; }
code_of() { printf '%s' "$1" | tail -1; }

# ids → список id API (по страницам; Gravitee не отдаёт больше 10 на страницу)
list_ids() {
  local page=1 out code body chunk ids="" first=1
  while [ "$page" -le 30 ]; do
    out=$(apim GET "$APIS_PATH?page=$page&per_page=10")
    code=$(code_of "$out"); body=$(body_of "$out")
    if [ "$first" = "1" ]; then
      echo "  листинг APIM: HTTP $code" >&2
      first=0
    fi
    chunk=$(printf '%s' "$body" | tr -d '\n' | grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' | sed 's/.*"\([^"]*\)"$/\1/')
    [ -n "$chunk" ] || break
    ids="$ids $chunk"
    page=$((page+1))
  done
  printf '%s' "$ids"
}

count_ids() { printf '%s' "$1" | wc -w | tr -d ' '; }

stop_and_delete() {
  local id="$1" sresp srespc dresp dcode body
  sresp=$(apim POST "$APIS_PATH/$id/_stop"); srespc=$(code_of "$sresp")
  dresp=$(apim DELETE "$APIS_PATH/$id"); dcode=$(code_of "$dresp")
  case "$dcode" in
    200|204) return 0 ;;
    *)
      # Диагностика первых трёх неудач: без неё «удалено 0, пропущено 66» ничего не объясняет.
      if [ "${DEL_DIAG:-0}" -lt 3 ]; then
        DEL_DIAG=$(( ${DEL_DIAG:-0} + 1 ))
        body=$(body_of "$dresp" | tr -d '\n' | head -c 220)
        echo "    DELETE ${id:0:8}… → $dcode (stop: $srespc) $body" >&2
      fi
      return 1 ;;
  esac
}

# --- plan --------------------------------------------------------------------------------

echo "спеки для импорта:"
n=0; total=0
for f in $SPECS; do
  svc=$(basename "$f" .json)
  ops=$(grep -c '"operationId"' "$f" 2>/dev/null || echo 0)
  printf '  %-24s операций: %s\n' "$svc" "$ops"
  n=$((n+1)); total=$((total+ops))
done
echo "итого спек: $n, операций: $total"
[ "$DRY" -eq 1 ] && { echo "import.sh: --dry-run — ничего не отправлено"; exit 0; }

# --- снос существующих API (один раз, до импорта) ----------------------------------------

# Снос по умолчанию ВЫКЛЮЧЕН (@TL 2026-11-16 01:4x): после перехода на contextPath из
# префикса путей конфликта `path.invalid` больше нет — новые API встают на /api/v1/<svc>,
# а старые (на /<title>/) никому не мешают. Удаление же на живом стенде стабильно не проходит
# (в прогоне 43769214988: удалено 0, пропущено 66 — DELETE отдаёт 400 для запущенных API), и
# из-за него импорт падал. Нужен снос — запускать с APIM_TEARDOWN=1.
if [ "$PUBLISH" = "1" ] && [ "${APIM_TEARDOWN:-0}" = "1" ]; then
  ids=$(list_ids)
  cnt=$(count_ids "$ids")
  echo "существующих API в APIM: $cnt — сношу перед импортом (иначе 400 path.invalid)"
  del_ok=0; del_skip=0
  for id in $ids; do
    if stop_and_delete "$id"; then del_ok=$((del_ok+1)); else del_skip=$((del_skip+1)); fi
  done
  echo "снос завершён: удалено $del_ok, пропущено $del_skip (404 = уже нет, 400 = нельзя удалить)"
  if [ "$cnt" = "0" ]; then
    # Листинг пуст, а импорт жалуется на занятые пути ⇒ API есть в Mongo, но management их не
    # отдаёт. Чистим коллекцию напрямую (база gravitee!) и перезапускаем management.
    echo "листинг пуст — чиню через mongo APIM (gravitee.apis) и перезапуск management"
    podman exec "$M" mongosh gravitee --quiet --eval 'db.apis.deleteMany({})' 2>/dev/null \
      || podman exec "$M" mongo gravitee --quiet --eval 'db.apis.deleteMany({})' 2>/dev/null \
      || echo "  ВНИМАНИЕ: mongosh/mongo в $M недоступны — снос не удался" >&2
    podman restart "$MG" >/dev/null 2>&1 || echo "  ВНИМАНИЕ: не смог перезапустить $MG" >&2
    sleep 25
  fi
fi

# --- импорт + план + публикация + старт ---------------------------------------------------

mkdir -p /tmp/apim-import
ok=0; failed=0
for f in $SPECS; do
  svc=$(basename "$f" .json)
  python3 - "$f" > "/tmp/apim-import/$svc.body" <<'PY' 2>/dev/null || \
    awk 'BEGIN{printf "{\"payload\":\""}{gsub(/\\/,"\\\\"); gsub(/"/,"\\\""); printf "%s\\n",$0}END{printf "\",\"withDocumentation\":true}"}' "$f" > "/tmp/apim-import/$svc.body"
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
# AR-160 (@TL 2026-11-16): Gravitee `_import/swagger` строит contextPath из `servers`/названия,
# а пути из спеки считает ОТНОСИТЕЛЬНЫМИ. Наши спеки содержат абсолютные пути `/api/v1/<svc>/...`,
# поэтому после импорта API живёт по адресу вида `/journalmicroserviceapi/` и прод отдаёт 404
# (проверено: до импорта 500, после — 404 на всех трёх ручках).
# Решение: вычисляем общий префикс путей, выносим его в `servers.url` (Gravitee сделает его
# contextPath), а сами пути делаем относительными. Снаружи адреса не меняются.
paths = doc.get("paths") or {}
segs = [p.strip("/").split("/") for p in paths if isinstance(p, str)]
prefix = []
if segs:
    for i in range(min(len(s) for s in segs)):
        col = {s[i] for s in segs}
        if len(col) == 1:
            prefix.append(segs[0][i])
        else:
            break
base = "/" + "/".join(prefix) if prefix else ""
new_paths = {}
for p, v in paths.items():
    if base and p.startswith(base):
        np_ = p[len(base):] or "/"
    else:
        np_ = p
    new_paths[np_] = v
doc["paths"] = new_paths
doc["servers"] = [{"url": base or "/"}]
sys.stdout.write(json.dumps({"payload": json.dumps(doc), "withDocumentation": True}))
PY

  attempt=1
  while :; do
    resp=$(apim POST "$APIS_PATH/_import/swagger" "/tmp/apim-import/$svc.body")
    code=$(code_of "$resp"); body=$(body_of "$resp")

    # 401 → один раз пробуем заводскую пару (DO-098 п.8)
    if [ "$code" = "401" ] && [ "${TRIED_FACTORY:-0}" = "0" ]; then
      TRIED_FACTORY=1
      AUTH="admin:admin"
      echo "  креды из $SECRETS не приняты (401) — пробую заводские admin/admin" >&2
      continue
    fi

    # 400 path.invalid → снимаем владельца пути и повторяем
    if [ "$code" = "400" ] && [ "${TRIED_PATHFIX:-0}" = "0" ]; then
      bad=$(printf '%s' "$body" | tr -d '\n' | sed -n 's/.*Path \[\([^]]*\)\].*/\1/p' | head -1)
      if [ -n "$bad" ]; then
        TRIED_PATHFIX=1
        echo "  путь $bad занят — ищу владельца и сношу" >&2
        owner=""
        for id in $(list_ids); do
          one=$(apim GET "$APIS_PATH/$id")
          if printf '%s' "$one" | grep -q "\"contextPath\"[[:space:]]*:[[:space:]]*\"$bad\""; then owner="$id"; break; fi
        done
        if [ -n "$owner" ]; then
          stop_and_delete "$owner" || true
          echo "  снёс API ${owner:0:8}…, повторяю импорт $svc" >&2
          continue
        fi
        echo "  владелец пути $bad не найден — оставляю как есть" >&2
      fi
    fi

    case "$code" in
      000|429|5??)
        if [ "$attempt" -lt "$API_ATTEMPTS" ]; then
          echo "  импорт $svc: код $code — повтор $attempt/$API_ATTEMPTS через 5 с"
          attempt=$((attempt+1)); sleep 5; continue
        fi ;;
    esac
    break
  done

  if [ "$code" != "200" ] && [ "$code" != "201" ]; then
    echo "  ОШИБКА $svc: HTTP $code" >&2
    printf '%s' "$body" | head -12 | cut -c1-300 >&2
    failed=$((failed+1))
    continue
  fi

  api_id=$(printf '%s' "$body" | tr -d '\n' | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "  импортирован $svc (id=${api_id:0:8}…)"
  ok=$((ok+1))

  if [ "$PUBLISH" = "1" ] && [ -n "$api_id" ]; then
    printf '%s' '{"name":"Default","description":"auto","security":{"type":"KEY_LESS"},"mode":"STANDARD","validation":"MANUAL","definitionVersion":"V4","flows":[]}' > "/tmp/apim-import/$svc.plan"
    presp=$(apim POST "$APIS_PATH/$api_id/plans" "/tmp/apim-import/$svc.plan")
    pcode=$(code_of "$presp"); pbody=$(body_of "$presp")
    plan_id=$(printf '%s' "$pbody" | tr -d '\n' | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
    if [ "$pcode" != "200" ] && [ "$pcode" != "201" ]; then
      echo "    план: HTTP $pcode — API останется без плана" >&2
      printf '%s' "$pbody" | head -6 | cut -c1-200 >&2
    else
      pub_code=$(code_of "$(apim POST "$APIS_PATH/$api_id/plans/$plan_id/_publish")")
      st_code=$(code_of "$(apim POST "$APIS_PATH/$api_id/_start")")
      echo "    план ${plan_id:0:8}… публикация: $pub_code, старт: $st_code (204 = запущен)"
      if [ "$st_code" != "204" ] && [ "$st_code" != "200" ]; then
        echo "    ВНИМАНИЕ: $svc не запустился (код $st_code) — шлюз будет отдавать 500" >&2
      fi
    fi
  fi
done

left=$(count_ids "$(list_ids)")
echo "import.sh: импортировано $ok, ошибок $failed; API в APIM: $left"
[ "$failed" -eq 0 ] || exit 1
