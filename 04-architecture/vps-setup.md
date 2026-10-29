# Памятка DevOps: настройка VPS (Ubuntu 22.04)

> Целевая архитектура: 10 микросервисов (.NET 8, профиль Single) + API Gateway (YARP).
> Два VPS: **VPS-1** (podman) и **VPS-2** (PostgreSQL). Нагрузка MVP: 2 пользователя, ~1 tpm.

---

## 1. Обзор топологии

| ВМ | Роль | Публичный доступ | Внутренний доступ |
|---|---|---|---|
| VPS-1 | podman: 10 сервисов + шлюз YARP | 443 (HTTPS) + 22 (SSH) | → VPS-2:5432 |
| VPS-2 | PostgreSQL: 1 кластер, 10 БД | **нет** (только 22 SSH) | ← VPS-1:5432 |

- Между ВМ — **приватная сеть** (VPC/private network хостера). PostgreSQL **не публикуется наружу**.
- Всё управление — по SSH с твоего ПК (белые IP или VPN).

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

### 2.3 TLS (шлюз)
- Шлюз YARP (Kestrel) терминирует HTTPS, либо перед ним `nginx`/`caddy` с авто-HTTPS.
- **Проще всего — caddy** (авто-Let's Encrypt): `caddy reverse-proxy --from xdiary.example.com --to 127.0.0.1:8080`.
- Альтернатива — certbot + nginx:
```bash
sudo apt install -y nginx certbot python3-certbot-nginx
sudo certbot --nginx -d xdiary.example.com
```

### 2.4 Развёртывание (черновик)
```bash
cd /opt/xdiary
git pull
podman compose build          # сборка 10 образов + шлюза
podman compose up -d          # поднять контейнеры
podman ps                     # проверка
```

---

## 3. VPS-2 (PostgreSQL)

### 3.1 Что установить
```bash
sudo apt update && sudo apt full-upgrade -y
sudo apt install -y postgresql postgresql-contrib fail2ban ufw unattended-upgrades

# (опционально) PostgreSQL 16 из PGDG вместо штатного 14:
# https://www.postgresql.org/download/linux/ubuntu/
```

### 3.2 Что должно быть на ВМ
- 1 кластер PostgreSQL, внутри — **10 БД + 10 ролей** (по одной на сервис), пароли — в secrets.
- Скрипт бэкапа (`pg_dumpall`/`pgBackRest`) в cron.
- Данные в `/var/lib/postgresql/...`, WAL, бэкапы на отдельный диск/том.

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

### 3.4 Настройка доступа
`/etc/postgresql/*/main/postgresql.conf`:
```ini
listen_addresses = '10.0.0.2'        # приватный IP VPS-2 (НЕ 0.0.0.0!)
port = 5432
password_encryption = scram-sha-256
```

`/etc/postgresql/*/main/pg_hba.conf`:
```conf
# только VPS-1 по приватной сети
host  all  all  10.0.0.1/32  scram-sha-256
# локально
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
sudo ufw allow 443/tcp    # HTTPS
sudo ufw allow 80/tcp     # для certbot/caddy
sudo ufw enable
```

VPS-2:
```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow from 10.0.0.1 to any port 5432 proto tcp   # только VPS-1
sudo ufw enable
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
- Не открывать 5432 наружу (только приватная сеть, только VPS-1).
- Отдельная роль на сервис, минимальные права (`CREATE/ALL` только на свою БД).
- Регулярный `VACUUM`/`ANALYZE` (autovacuum по умолчанию).

---

## 5. Порядок развёртывания (checklist)

- [ ] Создать 2 VPS, включить приватную сеть.
- [ ] Базово обновить, создать `deploy`, настроить SSH-ключи, отключить root/password.
- [ ] ufw: deny by default + разрешить минимум.
- [ ] fail2ban + unattended-upgrades.
- [ ] **VPS-2:** поставить PostgreSQL, настроить `listen_addresses`/`pg_hba`, создать 10 БД/ролей.
- [ ] **VPS-1:** поставить podman + .NET SDK, git clone, настроить TLS (caddy/nginx+certbot).
- [ ] Собрать образы и поднять 10 сервисов + шлюз.
- [ ] Прокинуть секреты `.env` (Garmin key, JWT secret, пароли БД).
- [ ] Проверить доступ: PWA → шлюз (HTTPS) → сервисы → БД.
- [ ] Настроить бэкап PostgreSQL (cron, `pg_dumpall`).

---

## 6. Проверки (acceptance)

- [ ] `ssh root@VPS` — **отказано**; вход только `deploy` по ключу.
- [ ] `nc -zv VPS2 5432` снаружи — **закрыто**; с VPS-1 — открыто.
- [ ] `https://xdiary.example.com` — валидный сертификат, PWA грузится.
- [ ] Контейнеры подняты: `podman ps` = 11 строк (10 сервисов + шлюз).
- [ ] БД доступна с сервисов, миграция CSV прошла.
- [ ] Бэкап создаётся и восстанавливается (`pg_restore` в тестовую БД).

---

## Связанные артефакты
- `vm-sizing.md` — расчёт ресурсов (4 vCPU / 6 ГБ / 80 ГБ суммарно).
- `ADL/adr-009.md` — развёртывание на двух ВМ.
- `ADL/adr-008.md` — аутентификация (JWT + PIN).
