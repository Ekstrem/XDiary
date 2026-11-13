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
for f in $SPECS; do
  svc=$(basename "$f" .json)
  ops=$(grep -c '"operationId"' "$f" 2>/dev/null || echo 0)
  printf '  %-24s операций: %s\n' "$svc" "$ops"
  n=$((n+1)); total=$((total+ops))
done
echo "итого спек: $n, операций: $total"
[ "$DRY" -eq 1 ] && { echo "import.sh: --dry-run — ничего не отправлено"; exit 0; }

# тело запроса собираем питоном внутри контейнера (там python нет — берём jq/awk)
mkdir -p /tmp/apim-import
ok=0; failed=0
for f in $SPECS; do
  svc=$(basename "$f" .json)
  # JSON-escape содержимого спеки → кладём во временный файл тела
  python3 - "$f" > "/tmp/apim-import/$svc.body" <<'PY' 2>/dev/null || \
    awk 'BEGIN{printf "{\"type\":\"BOTH\",\"definition\":\""}{gsub(/\\/,"\\\\"); gsub(/"/,"\\\""); printf "%s\\n",$0}END{printf "\"}"}' "$f" > "/tmp/apim-import/$svc.body"
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
# AR-160 (@TL 2026-11-13 23:3x): Gravitee берёт `servers[0].url` как базу API. Если в спеке
# стоит `"/"` (так было в journal/partners/api-gateway), backend получается невалидным и
# шлюз отдаёт 500 СВОИМ телом на ЛЮБОЙ путь этого API — проверено на проде:
#   GET /api/v1/journal/nonexistent-xyz → 500 (а /api/v1/catalog/nonexistent-xyz → 401).
# Нормализуем на импорте: `servers` с пустым/корневым url убираем — импортёр должен быть
# устойчив к спеке, а не падать от одной строки.
srv = doc.get("servers")
if isinstance(srv, list) and srv and str((srv[0] or {}).get("url", "")).strip() in ("", "/"):
    doc.pop("servers", None)
    print("import: убран servers с корневым url", file=sys.stderr)
sys.stdout.write(json.dumps({"type": "BOTH", "definition": json.dumps(doc)}))
PY
  podman cp "/tmp/apim-import/$svc.body" "$MG:/tmp/body.json" >/dev/null 2>&1
  resp=$(podman exec "$MG" curl -s -m 90 -w '\n%{http_code}' -X POST \
    -u "$USER:$PASS" -H 'Content-Type: application/json' \
    --data-binary @/tmp/body.json \
    "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis/import" 2>/dev/null)
  code=$(printf '%s' "$resp" | tail -1)
  api_id=$(printf '%s' "$resp" | head -1 | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
  if [ "$code" = "200" ] || [ "$code" = "201" ]; then
    echo "  импортирован $svc (id=${api_id:0:8}…)"
    if [ "$PUBLISH" = "1" ] && [ -n "$api_id" ]; then
      dcode=$(podman exec "$MG" curl -s -o /dev/null -w '%{http_code}' -m 60 -X POST \
        -u "$USER:$PASS" -H 'Content-Type: application/json' -d '{}' \
        "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$api_id/deployments" 2>/dev/null)
      echo "    публикация: $dcode"
    fi
    ok=$((ok+1))
  else
    echo "  ОШИБКА $svc: HTTP $code" >&2
    printf '%s' "$resp" | head -1 | cut -c1-200 >&2
    failed=$((failed+1))
  fi
done

count=$(podman exec "$MG" curl -s -m 30 -u "$USER:$PASS" \
  "$BASE/management/v2/organizations/$ORG/environments/$ENV_ID/apis?per_page=100" 2>/dev/null \
  | grep -c '"id"')
echo "import.sh: импортировано $ok, ошибок $failed; API в APIM: ~$count"
[ "$failed" -eq 0 ] || exit 1
