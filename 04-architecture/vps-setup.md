# Памятка DevOps: настройка VPS (Ubuntu 22.04)

> Целевая архитектура: 10 микросервисов (.NET 8, профиль Single) + API Gateway (YARP).
> Два VPS: **VPS-1** (podman) и **VPS-2** (PostgreSQL). Нагрузка MVP: 2 пользователя, ~1 tpm.

---

## 1. Обзор топологии

| ВМ | Роль | Публичный доступ | Внутренний доступ |
|---|---|---|---|
| VPS-1 | podman: 10 сервисов + шлюз + **PostgreSQL (co-locate)** | 22 (SSH) + Tailscale (VPN) | локально (127.0.0.1) |
| VPS-2 | **бэкап-таргет** / заморожена | **нет** (только 22 SSH) | — |

- Доступ к приложению — **только через Tailscale** (частная VPN + валидный TLS, ADR-015); публичный 443 не открывается.
- **PostgreSQL co-locate на VPS-1** (ADR-016): у Hostkey нет приватной сети, публичный канал VPS-1↔VPS-2 непригоден для БД (`SELECT 1` 3–7 с).
- VPS-2 — бэкап-таргет (`pg_dump`) или заморозка до появления VPC у провайдера.
- Всё управление — по SSH (белый IP или через Tailscale SSH).

---

## 2. VPS-1 (приложения)

### 2.1 Что установить
```bash
sudo apt update && sudo apt full-upgrade -y
sudo apt install -y podman podman-compose git fail2ban ufw unattended-upgrades curl ca-certificates

# .NET 8 (для сборки на ВМ) — Microsoft repo
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/ms.deb
sudo dpkg -i /tmp/ms.deb && sudo apt update
sudo apt install -y dotnet-sdk-8.0
```

### 2.2 Что должно быть на ВМ
- `/opt/xdiary/` — исходники (git clone) и `podman-compose.yml`.
- `/opt/xdiary/secrets/.env` — секреты (Garmin key, JWT secret), права `600`, **в git не попадает**.
- `/etc/letsencrypt/` — TLS-сертификаты (certbot).
- Логи контейнеров → `journald` (`podman logs`).

### 2.3 Доступ и TLS (Tailscale, без домена)
У заказчика **нет доменных имён** → Let's Encrypt по HTTP-01 невозможен. Решение — **Tailscale** (ADR-015): частная VPN + MagicDNS + валидный Let's Encrypt-сертификат.

1. Установить Tailscale на VPS-1 и на телефоны пары:
```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
```
2. В админке Tailscale включить **MagicDNS** и **HTTPS Certificates**.
3. Выпустить сертификат (DNS-01, автопродление):
```bash
sudo tailscale cert xdiary.vps1.ts.net
```
4. Отдать сертификат шлюзу (caddy или YARP/Kestrel):
```bash
caddy reverse-proxy --from https://xdiary.vps1.ts.net --to 127.0.0.1:8080
```
5. **Публичный 443 не открывать** — доступ только через Tailscale (интерфейс `tailscale0`).

### 2.4 Развёртывание (черновик)
```bash
cd /opt/xdiary
git pull
podman compose build          # сборка 10 образов + шлюза
podman compose up -d          # поднять контейнеры
podman ps                     # проверка
```

---

## 3. PostgreSQL (co-locate на VPS-1)

### 3.1 Что установить (на VPS-1)
```bash
sudo apt install -y postgresql postgresql-contrib

# (опционально) PostgreSQL 16 из PGDG вместо штатного 14:
# https://www.postgresql.org/download/linux/ubuntu/
```

### 3.2 Что должно быть
- 1 кластер PostgreSQL на VPS-1, внутри — **10 БД + 10 ролей** (по одной на сервис), пароли — в secrets.
- Скрипт бэкапа (`pg_dump`) в cron → **на VPS-2** (или S3).
- Данные в `/var/lib/postgresql/...` на VPS-1.

### 3.3 Создание БД и ролей
```bash
sudo -u postgres psql <<'SQL'
CREATE ROLE journal  LOGIN PASSWORD '...';
CREATE ROLE partners LOGIN PASSWORD '...';
-- ... ещё 8 ролей (catalog, somatic_series, daily_metrics,
--    garmin_gateway, apple_health_gateway, analytics, privacy, portability)

CREATE DATABASE journal  OWNER journal;
CREATE DATABASE partners OWNER partners;
-- ... ещё 8 БД
SQL
```

### 3.4 Настройка доступа (co-locate)
`/etc/postgresql/*/main/postgresql.conf`:
```ini
listen_addresses = '127.0.0.1'        # только локально (co-locate)
port = 5432
password_encryption = scram-sha-256
```

`/etc/postgresql/*/main/pg_hba.conf`:
```conf
# только локальные контейнеры
host  all  all  127.0.0.1/32  scram-sha-256
local all  postgres          peer
```

```bash
sudo systemctl restart postgresql
```

---

## 4. Безопасность (обе ВМ)

### 4.1 SSH — только ключи
```bash
# создать пользователя (если нет) и отключить root
sudo adduser deploy && sudo usermod -aG sudo deploy
# /etc/ssh/sshd_config:
#   PermitRootLogin no
#   PasswordAuthentication no
#   PubkeyAuthentication yes
#   Port 22 (или нестандартный, напр. 2222)
sudo systemctl restart ssh
```

### 4.2 Firewall (ufw) — deny by default
VPS-1:
```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp     # SSH (или 2222)
sudo ufw enable
# HTTPS (443/80) НЕ открывать публично — доступ через Tailscale (tailscale0)
```

VPS-2 (бэкап-таргет):
```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw enable
# 5432 НЕ открывать — БД на VPS-1 (co-locate)
```

### 4.3 fail2ban
```bash
sudo cp /etc/fail2ban/jail.conf /etc/fail2ban/jail.local
# включить [sshd], при желании [postgres] на VPS-2
sudo systemctl enable --now fail2ban
```

### 4.4 Автообновления
```bash
sudo dpkg-reconfigure -plow unattended-upgrades   # включить security-обновления
```

### 4.5 Секреты
- `.env` (ключ Garmin, JWT secret, пароли БД) — только на ВМ, `chmod 600`, **не в git**.
- Пароли БД — в переменных окружения контейнеров, не в `compose.yml` (через `env_file`).

### 4.6 podman — rootless
- Запускать контейнеры от непривилегированного пользователя `deploy`.
- Ограничить ресурсы в compose: `--memory 256m`, `--pids-limit 512`, `read_only: true` (где можно).
- Для порта 443 от rootless podman — `CAP_NET_BIND_SERVICE` или caddy/nginx на 443 → шлюз на 8080.

### 4.7 PostgreSQL hardening
- Не открывать 5432 наружу (`listen_addresses = 127.0.0.1`, co-locate).
- Отдельная роль на сервис, минимальные права (`CREATE/ALL` только на свою БД).
- Регулярный `VACUUM`/`ANALYZE` (autovacuum по умолчанию).

---

## 5. Порядок развёртывания (checklist)

- [ ] Создать VPS-1 (и VPS-2 под бэкап, опционально).
- [ ] Базово обновить, создать `deploy`, настроить SSH-ключи, отключить root/password.
- [ ] ufw: deny by default + разрешить минимум.
- [ ] fail2ban + unattended-upgrades.
- [ ] **VPS-1:** поставить podman + PostgreSQL + .NET SDK, создать 10 БД/ролей, настроить TLS (Tailscale).
- [ ] Собрать образы и поднять 10 сервисов + шлюз + PostgreSQL.
- [ ] Прокинуть секреты `.env` (Garmin key, JWT secret, пароли БД).
- [ ] Проверить доступ: PWA → шлюз (HTTPS) → сервисы → БД (локально).
- [ ] Настроить бэкап PostgreSQL (cron, `pg_dump` → VPS-2/S3).

---

## 6. Проверки (acceptance)

- [ ] `ssh root@VPS` — **отказано**; вход только `deploy` по ключу.
- [ ] `SELECT 1` локально (psql) < 100 мс — стабильно.
- [ ] `https://xdiary.vps1.ts.net` (через Tailscale) — валидный сертификат, PWA грузится.
- [ ] Контейнеры подняты: `podman ps` = 12 строк (10 сервисов + шлюз + postgres).
- [ ] БД доступна с сервисов, миграция CSV прошла.
- [ ] Бэкап создаётся и восстанавливается (`pg_restore` в тестовую БД).

---

## Связанные артефакты
- `vm-sizing.md` — расчёт ресурсов (4 vCPU / 6 ГБ / 80 ГБ суммарно).
- `ADL/adr-009.md` — развёртывание на двух ВМ.
- `ADL/adr-008.md` — аутентификация (JWT + PIN).
