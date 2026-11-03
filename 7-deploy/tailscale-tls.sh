#!/usr/bin/env bash
# DO-004: TLS via Tailscale MagicDNS + tailscale cert + caddy (ADR-015)
# Запускать на VPS-1 после `tailscale up` (авторизации устройства).
set -euo pipefail

# 1. MagicDNS-имя узла (например xdiary-vps1.tail-xxxxx.ts.net)
HOST=$(tailscale status --json 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin)['Self']['DNSName'].rstrip('.'))" 2>/dev/null || true)
# DO-050: пустой HOST раньше уходил в конфиг молча (ошибка глоталась через 2>/dev/null).
if [ -z "${HOST:-}" ]; then
  echo "tailscale-tls: не удалось определить DNSName узла — выхожу, чтобы не писать пустой сертификат" >&2
  exit 1
fi
if [ -z "$HOST" ]; then
  echo "Tailscale ещё не авторизован (NeedsLogin). Сначала: sudo tailscale up" >&2
  exit 1
fi
echo "MagicDNS host: $HOST"

# 2. Let's Encrypt-сертификат от tailscale (валидный, без домена и без публичного 443)
sudo tailscale cert "$HOST"

# 3. Caddy: HTTPS на этом имени -> шлюз YARP на 127.0.0.1:8080
sudo mkdir -p /etc/caddy
sudo tee /etc/caddy/Caddyfile >/dev/null <<EOF
$HOST {
    tls /home/deploy/$HOST.crt /home/deploy/$HOST.key
    reverse_proxy 127.0.0.1:8080 {
        transport http {
            read_timeout 60s
            dial_timeout 10s
        }
    }
}
EOF
sudo systemctl reload caddy 2>/dev/null || sudo systemctl restart caddy
echo "TLS готов: https://$HOST"
