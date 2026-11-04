#!/usr/bin/env bash
# import.sh — импорт openapi/*.json в Gravitee APIM (DO-056, contract-first).
#
# Почему так: Management API не опубликован на хост (только 8082 у gateway), поэтому
# запросы идут ИЗНУТРИ контейнера management (`podman exec … curl 127.0.0.1:8083`).
# Токен берём из /opt/xdiary/secrets/apim.env (в репозитории его нет и не будет, AGENTS §6).
#
# Использование:
#   bash 7-deploy/apim/import.sh --dry-run          # план по openapi/*.json, без токена
#   bash 7-deploy/apim/import.sh                    # импорт + публикация версии
#   bash 7-deploy/apim/import.sh --force            # переимпорт существующих (по title)
set -uo pipefail

ROOT_DIR="${XDIARY_ROOT:-/opt/xdiary}"
SECRETS="${XDIARY_SECRETS:-/opt/xdiary/secrets/apim.env}"
MG="${XDIARY_APIM_MGMT:-xdiary-apim-management}"
ORG="DEFAULT"
ENV_ID="DEFAULT"
API="http://127.0.0.1:8083"
DRY=0
FORCE=0
for a in "$@"; do
  case "$a" in
    --dry-run) DRY=1 ;;
    --force) FORCE=1 ;;
    *) echo "usage: $0 [--dry-run] [--force]" >&2; exit 2 ;;
  esac
done

SPECS=$(ls "$ROOT_DIR"/openapi/*.json 2>/dev/null | grep -v -E '/(index|_)' | sort)
[ -n "$SPECS" ] || { echo "import.sh: в $ROOT_DIR/openapi нет спек — нечего импортировать" >&2; exit 2; }

count=0; total_paths=0
echo "спеки для импорта:"
for f in $SPECS; do
  svc=$(basename "$f" .json)
  paths=$(grep -c '"operationId"' "$f" 2>/dev/null || echo 0)
  printf '  %-24s путей/операций: %s\n' "$svc" "$paths"
  count=$((count+1)); total_paths=$((total_paths+paths))
done
echo "итого спек: $count, операций: $total_paths"

if [ "$DRY" -eq 1 ]; then
  echo "import.sh: --dry-run — ничего не отправлено"
  exit 0
fi

[ -f "$SECRETS" ] || { echo "import.sh: нет $SECRETS (нужен токен Management API)" >&2; exit 2; }
TOKEN=$(grep -E '^GRAVITEE_TOKEN=' "$SECRETS" | cut -d= -f2-)
[ -n "$TOKEN" ] || { echo "import.sh: в $SECRETS нет GRAVITEE_TOKEN" >&2; exit 2; }

ok=0; failed=0
for f in $SPECS; do
  svc=$(basename "$f" .json)
  body=$(python3 - "$f" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding='utf-8'))
print(json.dumps({"type": "BOTH", "definition": json.dumps(doc)}))
PY
)
  resp=$(printf '%s' "$body" | podman exec -i "$MG" curl -s -m 60 -w '\n%{http_code}' \
    -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
    --data-binary @- "$API/management/v2/organizations/$ORG/environments/$ENV_ID/apis/import" 2>/dev/null)
  code=$(printf '%s' "$resp" | tail -1)
  api_id=$(printf '%s' "$resp" | head -1 | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
  if [ "$code" = "200" ] || [ "$code" = "201" ]; then
    echo "  импортирован $svc (id=${api_id:0:8}…)"
    if [ -n "$api_id" ]; then
      dcode=$(podman exec "$MG" curl -s -o /dev/null -w '%{http_code}' -m 30 \
        -X POST -H "Authorization: Bearer $TOKEN" \
        "$API/management/v2/organizations/$ORG/environments/$ENV_ID/apis/$api_id/deployments" 2>/dev/null)
      echo "    публикация: $dcode"
    fi
    ok=$((ok+1))
  else
    echo "  ОШИБКА $svc: HTTP $code" >&2
    printf '%s' "$resp" | head -1 | cut -c1-200 >&2
    failed=$((failed+1))
  fi
done

echo "import.sh: импортировано $ok, ошибок $failed"
echo "проверка: GRAVITEE_TOKEN=\$(grep ^GRAVITEE_TOKEN= $SECRETS | cut -d= -f2) python3 tools/check_apim_drift.py --root $ROOT_DIR"
[ "$failed" -eq 0 ] || exit 1
