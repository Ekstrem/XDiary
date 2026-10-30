#!/usr/bin/env bash
set -euo pipefail

# XDiary — VPS-1 (приложения): базовое + podman rootless + .NET SDK + caddy (TLS).
# Соответствие: DO-001 (безопасность), DO-003 (podman + деплой), DO-004 (TLS).
#
# Запуск от root на свежем Ubuntu 22.04:
#   export PUBLIC_KEY="ssh-ed25519 AAAA... user@host"   # ваш открытый ключ (обязателен!)
#   export VPS1_PRIVATE_IP="10.0.0.1"                   # приватный IP этой ВМ
#   TLS — через Tailscale (ADR-015): домен и публичный 443 не нужны.
#   sudo -E bash 7-deploy/provision-vps1.sh
#
# БЕЗ PUBLIC_KEY скрипт НЕ отключит парольный вход (защита от потери доступа).

: "${PUBLIC_KEY:?Задайте PUBLIC_KEY — открытый ключ исполнителя}"
: "${DEPLOY_USER:=deploy}"
: "${APP_DIR:=/opt/xdiary}"
: "${GIT_URL:=https://github.com/Ekstrem/XDiary.git}"
: "${DOMAIN:=}"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

say "1/8 apt update + full-upgrade"
apt-get update && apt-get -y full-upgrade

say "2/8 пакеты (podman 4.x, git, fail2ban, ufw, unattended-upgrades)"
apt-get install -y ca-certificates curl gnupg lsb-release git fail2ban ufw \
  unattended-upgrades python3-pip
# podman 4.x из OBS: штатный podman 3.4.4 в Ubuntu 22.04 ломает rootless CNI-сеть
# (ошибка "unknown FS magic" при join netns) — нужен netavark из podman 4.x.
echo "deb https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/xUbuntu_22.04/ /" \
  > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
curl -fsSL "https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/xUbuntu_22.04/Release.key" \
  | gpg --dearmor -o /etc/apt/trusted.gpg.d/devel_kubic.gpg
apt-get update
apt-get install -y podman netavark aardvark-dns
# podman-compose нет в репозиториях Ubuntu 22.04 — ставим из PyPI
pip3 install podman-compose

say "3/8 .NET 8 SDK (на случай пересборки образов на ВМ)"
wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/ms.deb
dpkg -i /tmp/ms.deb
apt-get update
apt-get install -y dotnet-sdk-8.0

say "4/8 пользователь $DEPLOY_USER + sudo (NOPASSWD)"
if ! id -u "$DEPLOY_USER" >/dev/null 2>&1; then
  adduser --disabled-password --gecos "" "$DEPLOY_USER"
fi
usermod -aG sudo "$DEPLOY_USER"
echo "$DEPLOY_USER ALL=(ALL) NOPASSWD:ALL" > "/etc/sudoers.d/$DEPLOY_USER"
chmod 440 "/etc/sudoers.d/$DEPLOY_USER"
# hostname в /etc/hosts (иначе sudo ругается "unable to resolve host")
grep -qF "127.0.1.1 $(hostname)" /etc/hosts || echo "127.0.1.1 $(hostname)" >> /etc/hosts

say "5/8 SSH: только ключи (root/password выключаем после установки ключа)"
install -d -m 700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh"
if ! grep -qsF "$PUBLIC_KEY" "/home/$DEPLOY_USER/.ssh/authorized_keys" 2>/dev/null; then
  echo "$PUBLIC_KEY" >> "/home/$DEPLOY_USER/.ssh/authorized_keys"
fi
chown "$DEPLOY_USER:$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh/authorized_keys"
chmod 600 "/home/$DEPLOY_USER/.ssh/authorized_keys"

if ! grep -qs 'ssh-' "/home/$DEPLOY_USER/.ssh/authorized_keys"; then
  echo "!! Нет authorized_keys — НЕ отключаю парольный вход (риск потерять доступ)." >&2
  exit 1
fi
sed -i -E 's/^#?PermitRootLogin .*/PermitRootLogin no/' /etc/ssh/sshd_config
sed -i -E 's/^#?PasswordAuthentication .*/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i -E 's/^#?PubkeyAuthentication .*/PubkeyAuthentication yes/' /etc/ssh/sshd_config
systemctl restart ssh

say "6/8 firewall (ufw): deny incoming, только 22 (+ Tailscale через tailscale0)"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw --force enable
# 80/443 публично НЕ открываем (ADR-015): PWA доступен только через Tailscale.
# После `tailscale up`:  sudo ufw allow in on tailscale0

say "7/8 fail2ban + unattended-upgrades"
cp -n /etc/fail2ban/jail.conf /etc/fail2ban/jail.local || true
systemctl enable --now fail2ban
dpkg-reconfigure -plow unattended-upgrades || true

say "8/8 git clone + Tailscale + caddy (TLS, ADR-015)"
install -d "$APP_DIR" && chown "$DEPLOY_USER:$DEPLOY_USER" "$APP_DIR"
if [[ ! -d "$APP_DIR/.git" ]]; then
  sudo -u "$DEPLOY_USER" git clone "$GIT_URL" "$APP_DIR"
fi

# Tailscale (VPN + MagicDNS + Let's Encrypt-сертификат)
curl -fsSL https://tailscale.com/install.sh | sh
# ВНИМАНИЕ: `tailscale up` требует авторизации (открыть URL или auth-key).

# Caddy (терминирует HTTPS на tailscale0 -> шлюз на 8080)
apt-get install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
  | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
  | tee /etc/apt/sources.list.d/caddy-stable.list
apt-get update && apt-get install -y caddy

echo
echo "Готово (VPS-1). Дальше:"
echo "  1) положить secrets/.env (см. secrets/.env.example) в $APP_DIR/7-deploy/secrets/.env, chmod 600"
echo "  2) sudo tailscale up (авторизация) + включить MagicDNS/HTTPS Certificates"
echo "  3) TLS:             TS_HOST=xdiary.vps1.ts.net bash 7-deploy/setup-caddy.sh"
echo "  4) поднять:         cd $APP_DIR/7-deploy && sudo -u $DEPLOY_USER podman compose -f podman-compose.yml up -d"
