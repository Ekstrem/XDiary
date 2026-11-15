#!/usr/bin/env bash
# import.sh — импорт openapi/*.json в Gravitee APIM (DO-056, contract-first).
#
# ВАЖНО (проверено 2026-11-04 на APIM 4.4):
#   * Management API не опубликован на хост: запросы идут ИЗНУТРИ контейнера
#     (`podman exec … curl 127.0.0.1:8083`);
#   * вход — **Basic** (`admin`/`admin` из штатного memory-провайдера `gravitee.yml`);
#     эндпоинта `/management/v2/user/login` в 4.4 НЕТ (`401` = «нет такого пути», не «пароль»);
#   * v2-пути обязаны содержать контекст организации/среды:
#     `/management/v2/organizations/<ORG>/environments/<ENV>/apis…`, иначе фильтр отдаёт 500
#     (`GraviteeContextFilter` берёт организацию из пути, `UserServiceImpl.create` падает на null).
#
# Использование (на VPS, где нет локального python — скрипт сам заходит в контейнер):
#   bash import.sh --dry-run            # план по openapi/*.json
#   bash import.sh                      # импорт + публикация
set -uo pipefail

ROOT_DIR="${XDIARY_ROOT:-/opt/xdiary}"
SECRETS="${XDIARY_SECRETS:-/opt/xdiary/secrets/apim.env}"
MG="${XDIARY_APIM_MGMT:-xdiary-apim-management}"
M="${XDIARY_APIM_MONGO:-xdiary-apim-mongo}"
ORG="${XDIARY_APIM_ORG:-DEFAULT}"
ENV_ID="${XDIARY_APIM_ENV:-DEFAULT}"
BASE="http://127.0.0.1:8083"
DRY=0
for a in "$@"; do
  case "$a" in
    --dry-run) DRY=1 ;;
    --no-publish) PUBLISH=0 ;;
    *) echo "usage: $0 [--dry-run] [--no-publish]" >&2; exit 2 ;;
  esac
done
PUBLISH="${PUBLISH:-1}"

SPECS=$(ls "$ROOT_DIR"/openapi/*.json 2>/dev/null | grep -vE '/(index|_)' | sort)
[ -n "$SPECS" ] || { echo "import.sh: в $ROOT_DIR/openapi нет спек" >&2; exit 2; }

# креды: из secrets-файла, иначе штатные admin/admin (memory-провайдер gravitee.yml)
USER="admin"; PASS="admin"
if [ -f "$SECRETS" ]; then
  u=$(grep -E '^GRAVITEE_ADMIN_USER=' "$SECRETS" | cut -d= -f2-); [ -n "${u:-}" ] && USER="$u"
  P=$(grep -E '^GRAVITEE_ADMIN_PASSWORD=' "$SECRETS" | cut -d= -f2-); [ -n "${P:-}" ] && PASS="$P"
fi

n=0; total=0
echo "спеки для импорта:"
# Снос существующих API — ОДИН раз перед импортом (а не на каждую спеку: иначе предыдущий
# импорт тут же уничтожался бы, и цикл шёл по кругу — это и случилось в прогоне 2026-11-15).
# Удаление опубликованного API Gravitee требует сначала `_stop` (иначе 400), а 404 = уже удалён.
if [ "$PUBLISH" = "1" ]; then
  ids=$(apim_api_ids)
  count=$(printf '%s' "$ids" | wc -w)
  echo "существующих API в APIM: $count — сношу перед импортом (иначе 400 path.invalid)"
  del_ok=0; del_skip=0
  for id in $ids; do
    podman exec "$MG" curl -s -o /dev/null -m 20 -X POST -u "$USER:$PASS" \
      "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$id/_stop" 2>/dev/null || true
    d=$(podman exec "$MG" curl -s -o /dev/null -w '%{http_code}' -m 20 -X DELETE \
      -u "$USER:$PASS" "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$id" 2>/dev/null)
    case "$d" in
      200|204) del_ok=$((del_ok+1)) ;;
      *) del_skip=$((del_skip+1)) ;;
    esac
  done
  echo "снос завершён: удалено $del_ok, пропущено $del_skip (404 = уже нет, 400 = нельзя удалить)"
fi

for f in $SPECS; do
  svc=$(basename "$f" .json)
  ops=$(grep -c '"operationId"' "$f" 2>/dev/null || echo 0)
  printf '  %-24s операций: %s\n' "$svc" "$ops"
  n=$((n+1)); total=$((total+ops))
done
echo "итого спек: $n, операций: $total"
[ "$DRY" -eq 1 ] && { echo "import.sh: --dry-run — ничего не отправлено"; exit 0; }

# AR-160 / DO-133 (@TL 2026-11-15 17:2x): эндпоинт исправлен по РАНБУКУ.
# `7-deploy/runbooks/apim.md:193-207` описывает рабочую последовательность для Gravitee 4.4:
#   1) POST …/apis/_import/swagger  с телом {"payload": "<спека строкой>", "withDocumentation": true}
#   2) POST …/apis/{id}/plans       {"name":"Default","security":{"type":"KEY_LESS"},"mode":"STANDARD",
#                                    "validation":"MANUAL","definitionVersion":"V4","flows":[]}
#   3) POST …/apis/{id}/plans/{planId}/_publish
#   4) POST …/apis/{id}/_start      → 204
# Старый путь `/apis/import` в 4.4 НЕ существует → 405 (это и видели в прогоне 2026-11-15).
mkdir -p /tmp/apim-import
ok=0; failed=0
apim_curl() {  # $1 = method, $2 = path, $3 = body file or empty, $4 = extra curl args
  local m="$1" path="$2" body="${3:-}" extra="${4:-}"
  if [ -n "$body" ]; then
    podman exec "$MG" curl -s -m 90 -w '\n%{http_code}' -X "$m" \
      -u "$USER:$PASS" -H 'Content-Type: application/json' $extra \
      --data-binary @/tmp/body.json "$BASE$path" 2>/dev/null
  else
    podman exec "$MG" curl -s -m 90 -w '\n%{http_code}' -X "$m" \
      -u "$USER:$PASS" -H 'Content-Type: application/json' $extra "$BASE$path" 2>/dev/null
  fi
}

for f in $SPECS; do
  svc=$(basename "$f" .json)
  # AR-160 ч.2 (@TL 2026-11-15 20:5x): Gravitee отвечает 400 `Path [...] already exists` — импорт
# создаёт НОВЫЙ API, а API с таким contextPath уже есть (мы импортировали их раньше). Поэтому
# перед импортом сносим существующие API (они всё равно сломаны: journal/partners отдают 500),
# и собираем заново из git-спек. Это и есть смысл «ре-импорта», а не «добавь ещё одну копию».
# Пагинация: у Gravitee `per_page` больше 10 не увеличивает страницу (см. runbooks/apim.md).
apim_api_ids() {
  local page=1 ids="" out chunk
  while [ "$page" -le 30 ]; do
    out=$(podman exec "$MG" curl -s -m 30 -u "$USER:$PASS" \
      "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis?page=$page&per_page=10" 2>/dev/null)
    chunk=$(printf '%s' "$out" | grep -o '"id"[[:space:]]*:[[:space:]]*"[^"]*"' | sed 's/.*"\([^"]*\)"$/\1/')
    [ -n "$chunk" ] || break
    ids="$ids $chunk"
    page=$((page+1))
  done
  printf '%s' "$ids"
}


# тело импорта: payload = содержимое спеки СТРОКОЙ (+ нормализация servers, см. AR-160)
  python3 - "$f" > "/tmp/apim-import/$svc.body" <<'PY' 2>/dev/null || \
    awk 'BEGIN{printf "{\"payload\":\""}{gsub(/\\/,"\\\\"); gsub(/"/,"\\\""); printf "%s\\n",$0}END{printf "\",\"withDocumentation\":true}"}' "$f" > "/tmp/apim-import/$svc.body"
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
# AR-160 (@TL 2026-11-13 23:3x): Gravitee берёт `servers[0].url` как базу API. Если в спеке
# стоит `"/"` (так было в journal/partners/api-gateway), backend получается невалидным и
# шлюз отдаёт 500 СВОИМ телом на ЛЮБОЙ путь этого API — проверено на проде:
#   GET /api/v1/journal/nonexistent-xyz → 500 (а /api/v1/catalog/nonexistent-xyz → 401).
srv = doc.get("servers")
if isinstance(srv, list) and srv and str((srv[0] or {}).get("url", "")).strip() in ("", "/"):
    doc.pop("servers", None)
    print("import: убран servers с корневым url", file=sys.stderr)
sys.stdout.write(json.dumps({"payload": json.dumps(doc), "withDocumentation": True}))
PY
  podman cp "/tmp/apim-import/$svc.body" "$MG:/tmp/body.json" >/dev/null 2>&1

  # Повторитель (@TL 2026-11-15): импорт ретраим на СЕТЕВЫЕ/серверные коды (000/429/5xx), но НЕ на
  # 4xx — 400/401/405 означают проблему в запросе или правах, и повтор лишь тратит время.
  # Причина: `deploy-one.sh`/`recreate-service.sh` уже ретраят pull и пересоздание (3 попытки),
  # а импорт в APIM ретрая не имел вовсе.
  attempt=1
  IMP_ATTEMPTS="${APIM_IMPORT_ATTEMPTS:-3}"
  while :; do
    resp=$(apim_curl POST "/management/v2/organizations/$ORG/environments/$ENV_ID/apis/_import/swagger" body)
    code=$(printf '%s' "$resp" | tail -1)
    case "$code" in
      000|429|5??)
        if [ "$attempt" -lt "$IMP_ATTEMPTS" ]; then
          echo "  импорт $svc: код $code — повтор $attempt/$IMP_ATTEMPTS через 5 с"
          attempt=$((attempt+1)); sleep 5; continue
        fi ;;
    esac
    break
  done

  # креды: при 401 пробуем заводские admin/admin ОДИН раз (DO-098 п.8)
  if [ "$code" = "401" ] && [ "${TRIED_FACTORY:-0}" = "0" ]; then
    TRIED_FACTORY=1
    resp=$(podman exec "$MG" curl -s -m 90 -w '\n%{http_code}' -X POST \
      -u "admin:admin" -H 'Content-Type: application/json' \
      --data-binary @/tmp/body.json \
      "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis/_import/swagger" 2>/dev/null)
    code=$(printf '%s' "$resp" | tail -1)
    if [ "$code" = "200" ] || [ "$code" = "201" ]; then
      echo "  ВНИМАНИЕ: креды из apim.env не приняты (401), импорт идёт под ЗАВОДСКИМИ admin/admin — обнови секреты (DO-098 п.8)"
      USER="admin"; PASS="admin"
    else
      echo "  креды НЕ подошли: и apim.env, и заводские admin/admin дают 401 — нужен актуальный админ Gravitee (DO-098 п.8)" >&2
    fi
  fi

  api_id=$(printf '%s' "$resp" | head -1 | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
  if [ "$code" != "200" ] && [ "$code" != "201" ]; then
    echo "  ОШИБКА $svc: HTTP $code" >&2
    # Тело ошибки Gravitee — многострочный JSON; `head -1` показывал только `{` и скрывал причину
    # (прогон 2026-11-15: 12×HTTP 400 без единого слова диагностики). Печатаем первые 12 строк.
    printf '%s' "$resp" | head -12 | cut -c1-300 >&2
    failed=$((failed+1))
    continue
  fi
  echo "  импортирован $svc (id=${api_id:0:8}…)"

  if [ "$PUBLISH" = "1" ] && [ -n "$api_id" ]; then
    # 1) план (без плана API не запускается: api.NotDeployable)
    printf '%s' '{"name":"Default","description":"auto","security":{"type":"KEY_LESS"},"mode":"STANDARD","validation":"MANUAL","definitionVersion":"V4","flows":[]}' > "/tmp/apim-import/$svc.plan"
    podman cp "/tmp/apim-import/$svc.plan" "$MG:/tmp/body.json" >/dev/null 2>&1
    presp=$(apim_curl POST "/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$api_id/plans" body)
    pcode=$(printf '%s' "$presp" | tail -1)
    plan_id=$(printf '%s' "$presp" | head -1 | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
    if [ "$pcode" != "200" ] && [ "$pcode" != "201" ]; then
      echo "    план: HTTP $pcode — API останется без плана ($svc)" >&2
    else
      # 2) публикация плана
      pub=$(apim_curl POST "/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$api_id/plans/$plan_id/_publish" empty)
      echo "    план ${plan_id:0:8}… публикация: $(printf '%s' "$pub" | tail -1)"
      # 3) запуск API
      st=$(apim_curl POST "/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$api_id/_start" empty)
      echo "    запуск $svc: $(printf '%s' "$st" | tail -1) (204 = запущен)"
    fi
  fi
  ok=$((ok+1))
done

count=$(podman exec "$MG" curl -s -m 30 -u "$USER:$PASS" \
  "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis?per_page=100" 2>/dev/null \
  | grep -c '"id"')
echo "import.sh: импортировано $ok, ошибок $failed; API в APIM: ~$count"
[ "$failed" -eq 0 ] || exit 1
