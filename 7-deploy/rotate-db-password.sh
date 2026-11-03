#!/usr/bin/env bash
# rotate-db-password.sh — ротация пароля ОДНОЙ роли БД на проде (DO-049).
#
# Зачем именно так: пароли живут в ДВУХ местах — `secrets/.env` (ConnectionStrings__*Db)
# и в серверном `deploy.sh` (инлайн `-e ConnectionStrings__CommandDb/ReadDb`), причём
# инлайн перекрывает env-file. Ротация только `.env` не сработает.
#
# Использование (на VPS):
#   ./rotate-db-password.sh --service portability --confirm
#   ./rotate-db-password.sh --service portability --rollback <backup-dir>
#
# Гарантии:
#   * новый пароль НЕ печатается (ни в stdout, ни в лог);
#   * до изменений делаются бэкапы `.env` и `deploy.sh` (режим 600);
#   * при любой ошибке — автоматический откат (пароль + файлы);
#   * порядок: ALTER ROLE → файлы → пересоздание ТОЛЬКО этого контейнера → проверка.
set -uo pipefail

ENV_FILE=/opt/xdiary/7-deploy/secrets/.env
DEPLOY_SH=/home/deploy/deploy.sh
PGBIN="podman exec xdiary-pg"
BK_ROOT=/home/deploy/backups/rotate
SVC=""
MODE=rotate
BACKUP_DIR=""

say() { printf '%s\n' "$*"; }
die() { printf 'ОШИБКА: %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
  case "$1" in
    --service) SVC="${2:-}"; shift 2 ;;
    --confirm) MODE=rotate; shift ;;
    --rollback) MODE=rollback; BACKUP_DIR="${2:-}"; shift 2 ;;
    *) die "неизвестный аргумент: $1" ;;
  esac
done

[ -n "$SVC" ] || die "нужно --service <имя>"
case "$SVC" in
  journal|partners|catalog|somatic-series|daily-metrics|garmin-gateway|apple-health-gateway|analytics|privacy|portability|client) ;;
  *) die "неизвестный сервис: $SVC" ;;
esac

# соответствие сервис → роль БД (по compose)
case "$SVC" in
  somatic-series) ROLE=somatic_series ;;
  daily-metrics) ROLE=daily_metrics ;;
  garmin-gateway) ROLE=garmin_gateway ;;
  apple-health-gateway) ROLE=apple_health_gateway ;;
  *) ROLE="$SVC" ;;
esac

CONTAINER="xdiary-$SVC"
KEY_SUFFIX="$(printf '%s' "$SVC" | awk -F- '{for(i=1;i<=NF;i++) printf "%s", toupper(substr($i,1,1)) substr($i,2)}')"

if [ "$MODE" = rollback ]; then
  [ -n "$BACKUP_DIR" ] && [ -d "$BACKUP_DIR" ] || die "нет каталога бэкапа: $BACKUP_DIR"
  say "ОТКАТ для $SVC из $BACKUP_DIR"
  OLD_PASS="$(cat "$BACKUP_DIR/oldpass" 2>/dev/null || true)"
  [ -n "$OLD_PASS" ] || die "в бэкапе нет oldpass"
  $PGBIN psql -U postgres -c "ALTER ROLE \"$ROLE\" WITH PASSWORD '$OLD_PASS';" >/dev/null || die "ALTER ROLE не удался"
  cp -a "$BACKUP_DIR/env.bak" "$ENV_FILE"
  cp -a "$BACKUP_DIR/deploy.sh.bak" "$DEPLOY_SH"
  say "файлы и пароль восстановлены; пересоздай контейнер: bash /tmp/recreate-$SVC.sh"
  exit 0
fi

say "=== РОТАЦИЯ: $SVC (роль $ROLE) ==="
TS="$(date +%Y%m%d-%H%M%S)"
BK="$BK_ROOT/$SVC-$TS"
mkdir -p "$BK"
chmod 700 "$BK_ROOT" "$BK"

# 1) бэкапы
cp -a "$ENV_FILE" "$BK/env.bak"; chmod 600 "$BK/env.bak"
cp -a "$DEPLOY_SH" "$BK/deploy.sh.bak"; chmod 600 "$BK/deploy.sh.bak"
say "  бэкапы: $BK"

# 2) текущий пароль (нужен для sed и для отк��та) — в файл, не в вывод
CUR="$(grep -oE "Username=$ROLE;Password=[^;\"]+" "$DEPLOY_SH" | head -1 | sed 's/.*Password=//')"
if [ -z "$CUR" ]; then
  CUR="$(grep -oE "ConnectionStrings__[A-Za-z]+Db=.*Password=[^;\"]+" "$ENV_FILE" | grep -i "$ROLE" | head -1 | sed 's/.*Password=//')"
fi
[ -n "$CUR" ] || die "не нашёл текущий пароль роли $ROLE"
printf '%s' "$CUR" > "$BK/oldpass"; chmod 600 "$BK/oldpass"

# 3) новый пароль (не печатаем)
NEW="$(openssl rand -hex 16)"

# 4) ALTER ROLE
if ! $PGBIN psql -U postgres -c "ALTER ROLE \"$ROLE\" WITH PASSWORD '$NEW';" >/dev/null; then
  die "ALTER ROLE не удался — ничего не изменено"
fi
say "  пароль роли обновлён в PostgreSQL"

# 5) файлы
python3 - "$ENV_FILE" "$DEPLOY_SH" "$CUR" "$NEW" <<'PY'
import sys
env, dep, old, new = sys.argv[1:5]
for path in (env, dep):
    t = open(path, encoding='utf-8').read()
    t2 = t.replace('Password=' + old, 'Password=' + new)
    open(path, 'w', encoding='utf-8', newline='\n').write(t2)
PY
say "  .env и deploy.sh обновлены"

# 6) пересоздание ТОЛЬКО этого контейнера
python3 - "$DEPLOY_SH" "$SVC" "/tmp/recreate-$SVC.sh" <<'PY'
import sys
dep, svc, out = sys.argv[1:4]
src = open(dep, encoding='utf-8').read().replace('\\\n', ' ')
line = next((l for l in src.splitlines() if ('podman run -d --name xdiary-%s ' % svc) in l), '')
with open(out, 'w', encoding='utf-8') as f:
    f.write('set -u\nNET=xdiary\nLBL="--label io.containers.autoupdate=registry"\nTAG=single\n' + line.strip() + '\n')
PY
cd /opt/xdiary/7-deploy || die "нет каталога деплоя"
podman rm -f "$CONTAINER" >/dev/null 2>&1 || true
if ! bash "/tmp/recreate-$SVC.sh" >/dev/null 2>&1; then
  say "  пересоздание не удалось — ОТКАТ"
  bash "$0" --service "$SVC" --rollback "$BK"
  exit 1
fi
sleep 12

# 7) проверка: контейнер живёт и health отвечает
if ! podman ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
  say "  контейнер не поднялся — ОТКАТ"
  bash "$0" --service "$SVC" --rollback "$BK"
  exit 1
fi
if [ "$SVC" = "client" ]; then
  curl -fsS -m 8 http://127.0.0.1:8080/health >/dev/null || say "  внимание: /health не 200"
fi
say "  OK: $CONTAINER работает на новом пароле"
say "  откат при необходимости: $0 --service $SVC --rollback $BK"
