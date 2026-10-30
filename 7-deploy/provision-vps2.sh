#!/usr/bin/env bash
set -euo pipefail

# XDiary — VPS-2 (PostgreSQL): базовое + PG16 + 10 ролей/БД + файрвол + бэкап.
# Соответствие: DO-001 (безопасность), DO-002 (10 БД), DO-006 (бэкап).
#
# Запуск от root на свежем Ubuntu 22.04:
#   export PUBLIC_KEY="ssh-ed25519 AAAA... user@host"   # ваш открытый ключ (обязателен!)
#   export VPS1_PRIVATE_IP="10.0.0.1"                   # приватный IP VPS-1 (разрешён в pg_hba/ufw)
#   export VPS2_PRIVATE_IP="10.0.0.2"                   # приватный IP этой ВМ (listen_addresses)
#   sudo -E bash 7-deploy/provision-vps2.sh
#
# Пароли ролей генерируются и сохраняются в PG_PASSWORD_FILE (по умолчанию
# /opt/xdiary/7-deploy/secrets/.env) — этот файл потом копируется на VPS-1.

: "${PUBLIC_KEY:?Задайте PUBLIC_KEY — открытый ключ исполнителя}"
: "${VPS1_PRIVATE_IP:?Задайте VPS1_PRIVATE_IP (приватный IP VPS-1)}"
: "${VPS2_PRIVATE_IP:?Задайте VPS2_PRIVATE_IP (приватный IP этой ВМ)}"
: "${DEPLOY_USER:=deploy}"
: "${PG_PASSWORD_FILE:=/opt/xdiary/7-deploy/secrets/.env}"
: "${SCRIPTS_DIR:=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

declare -A DB_ENV=(
  [journal]=JournalDb
  [partners]=PartnersDb
  [catalog]=CatalogDb
  [somatic_series]=SomaticSeriesDb
  [daily_metrics]=DailyMetricsDb
  [garmin_gateway]=GarminGatewayDb
  [apple_health_gateway]=AppleHealthGatewayDb
  [analytics]=AnalyticsDb
  [privacy]=PrivacyDb
  [portability]=PortabilityDb
)
ROLES=(journal partners catalog somatic_series daily_metrics garmin_gateway apple_health_gateway analytics privacy portability)

say "1/8 apt update + full-upgrade"
apt-get update && apt-get -y full-upgrade

say "2/8 пакеты + PGDG PostgreSQL 16"
apt-get install -y ca-certificates curl gnupg lsb-release fail2ban ufw unattended-upgrades
install -d /usr/share/postgresql-common/pgdg
curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
  -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc
echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org $(lsb_release -cs)-pgdg main" \
  > /etc/apt/sources.list.d/pgdg.list
apt-get update
apt-get install -y postgresql-16 postgresql-contrib-16

say "3/8 postgresql.conf (listen = приватный IP, scram-sha-256)"
PGCONF=/etc/postgresql/16/main/postgresql.conf
sed -i -E "s/^#?listen_addresses .*/listen_addresses = '${VPS2_PRIVATE_IP}'/" "$PGCONF"
sed -i -E "s/^#?port .*/port = 5432/" "$PGCONF"
sed -i -E "s/^#?password_encryption .*/password_encryption = scram-sha-256/" "$PGCONF"

say "4/8 pg_hba.conf (5432 только с VPS-1 по приватной сети)"
PGHBA=/etc/postgresql/16/main/pg_hba.conf
cat >> "$PGHBA" <<EOF

# XDiary: только VPS-1 по приватной сети
host  all  all  ${VPS1_PRIVATE_IP}/32  scram-sha-256
local all  postgres                    peer
EOF
systemctl restart postgresql

say "5/8 10 ролей + 10 БД (пароли генерируются)"
mkdir -p "$(dirname "$PG_PASSWORD_FILE")"
: > "$PG_PASSWORD_FILE"
chmod 600 "$PG_PASSWORD_FILE"

PSQL_ARGS=()
for r in "${ROLES[@]}"; do
  pw="$(openssl rand -hex 16)"
  PSQL_ARGS+=(-v "pw_${r}=${pw}")
  printf 'ConnectionStrings__%s=Host=%s;Port=5432;Database=%s;Username=%s;Password=%s\n' \
    "${DB_ENV[$r]}" "$VPS2_PRIVATE_IP" "$r" "$r" "$pw" >> "$PG_PASSWORD_FILE"
done

sudo -u postgres psql -v ON_ERROR_STOP=1 "${PSQL_ARGS[@]}" -f "$SCRIPTS_DIR/db/init-databases.sql"

say "6/8 SSH + firewall (ufw) + fail2ban"
if ! id -u "$DEPLOY_USER" >/dev/null 2>&1; then
  adduser --disabled-password --gecos "" "$DEPLOY_USER"
fi
usermod -aG sudo "$DEPLOY_USER"
echo "$DEPLOY_USER ALL=(ALL) NOPASSWD:ALL" > "/etc/sudoers.d/$DEPLOY_USER"
chmod 440 "/etc/sudoers.d/$DEPLOY_USER"
grep -qF "127.0.1.1 $(hostname)" /etc/hosts || echo "127.0.1.1 $(hostname)" >> /etc/hosts
install -d -m 700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh"
if ! grep -qsF "$PUBLIC_KEY" "/home/$DEPLOY_USER/.ssh/authorized_keys" 2>/dev/null; then
  echo "$PUBLIC_KEY" >> "/home/$DEPLOY_USER/.ssh/authorized_keys"
fi
chown "$DEPLOY_USER:$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh/authorized_keys"
chmod 600 "/home/$DEPLOY_USER/.ssh/authorized_keys"
if ! grep -qs 'ssh-' "/home/$DEPLOY_USER/.ssh/authorized_keys"; then
  echo "!! Нет authorized_keys — НЕ отключаю парольный вход." >&2
  exit 1
fi
sed -i -E 's/^#?PermitRootLogin .*/PermitRootLogin no/' /etc/ssh/sshd_config
sed -i -E 's/^#?PasswordAuthentication .*/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i -E 's/^#?PubkeyAuthentication .*/PubkeyAuthentication yes/' /etc/ssh/sshd_config
systemctl restart ssh

ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow from "${VPS1_PRIVATE_IP}" to any port 5432 proto tcp
ufw --force enable

cp -n /etc/fail2ban/jail.conf /etc/fail2ban/jail.local || true
systemctl enable --now fail2ban
dpkg-reconfigure -plow unattended-upgrades || true

say "7/8 бэкап (DO-006): скрипт + cron (ежедневно 02:30)"
install -m 0755 "$SCRIPTS_DIR/backup-postgres.sh" /usr/local/bin/xdiary-pg-backup
( crontab -l 2>/dev/null | grep -v xdiary-pg-backup; \
  echo "30 2 * * * /usr/local/bin/xdiary-pg-backup >> /var/log/xdiary-pg-backup.log 2>&1" ) | crontab -

say "8/8 итог"
echo "Пароли/строки подключения -> $PG_PASSWORD_FILE (скопируйте на VPS-1 в /opt/xdiary/7-deploy/secrets/.env)"
echo "Проверка с VPS-1:  nc -zv ${VPS2_PRIVATE_IP} 5432"
