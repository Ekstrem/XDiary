#!/usr/bin/env bash
set -euo pipefail

# XDiary — TLS без домена через Tailscale (ADR-015): MagicDNS + Let's Encrypt (DNS-01).
# caddy терминирует HTTPS на интерфейсе tailscale0 -> шлюз podman на 127.0.0.1:8080.
# Публичный 443 НЕ открывается — доступ только внутри tailnet (частная VPN).
#
# Предусловия: `sudo tailscale up` выполнен, в админке Tailscale включены
# MagicDNS и HTTPS Certificates.
#
#   TS_HOST=xdiary.vps1.ts.net bash setup-caddy.sh

: "${TS_HOST:=xdiary.vps1.ts.net}"
: "${UPSTREAM:=127.0.0.1:8080}"

CERT="/etc/caddy/${TS_HOST}.crt"
KEY="/etc/caddy/${TS_HOST}.key"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

say "1/3 tailscale cert (Let's Encrypt, DNS-01, автопродление)"
mkdir -p /etc/caddy
tailscale cert --cert-file "$CERT" --key-file "$KEY" "$TS_HOST"
chown caddy:caddy "$CERT" "$KEY"
chmod 640 "$CERT" && chmod 600 "$KEY"

say "2/3 Caddyfile (HTTPS на tailscale0 -> $UPSTREAM)"
cat > /etc/caddy/Caddyfile <<EOF
https://${TS_HOST} {
    bind tailscale0
    tls ${CERT} ${KEY}
    reverse_proxy ${UPSTREAM}
    encode gzip
    log {
        output stdout
    }
}
EOF

say "3/3 reload caddy"
systemctl enable --now caddy
systemctl reload caddy 2>/dev/null || systemctl restart caddy
echo "Готово: https://${TS_HOST} -> ${UPSTREAM} (только через Tailscale)"
