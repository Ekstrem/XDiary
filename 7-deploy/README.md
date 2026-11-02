# 7-deploy — инфраструктура как код (DevOps, VPS XDiary)

Артефакты для задач `5-tech-backlog/devops` (DO-001…DO-006). Целевая архитектура:
10 микросервисов (.NET 8, профиль Single) + API Gateway (YARP) на ВМ-1 (podman rootless),
PostgreSQL (1 кластер, 10 БД) co-located на ВМ-1 (контейнер `xdiary-pg`); ВМ-2 — бэкап-таргет. Источник: `04-architecture/vps-setup.md`,
`04-architecture/vm-sizing.md`, ADR-001/003/004/009/010/015.

## Состав

| Файл | Назначение | Задача |
|---|---|---|
| `podman-compose.yml` | 11 контейнеров: 10 сервисов + шлюз (образы GHCR) | DO-003/005 |
| `secrets/.env.example` | шаблон секретов (JWT, Garmin, пароли БД) | DO-005 |
| `db/init-databases.sql` | 10 ролей + 10 БД (owner = своя роль) | DO-002 |
| `provision-vps1.sh` | база + podman + .NET SDK + Tailscale + caddy + безопасность | DO-001/003/004 |
| `provision-vps2.sh` | база + файрвол + бэкап-таргет (приём дампов с VPS-1) | DO-001/006 |
| `setup-caddy.sh` | TLS через Tailscale (MagicDNS + `tailscale cert`, 443 → 8080) | DO-004 |
| `backup-postgres.sh` | `pg_dumpall` с VPS-1 → VPS-2 + ротация (бэкап-таргет) | DO-006 |

## Топология

```
PWA (телефон) --HTTPS (Tailscale, xdiary.vps1.ts.net)--> VPS-1 (caddy -> api-gateway:8080 -> 10 сервисов -> PostgreSQL 10 БД, контейнер xdiary-pg)
                                                            \
                                                             --scp pg_dumpall (cron 02:30)--> VPS-2 (бэкап-таргет)
```

- **VPS-1** `ru-vps-msahost` (31.192.111.168): podman rootless от `deploy`, шлюз на `127.0.0.1:8080`; публично открыт только 22, доступ к PWA — только через Tailscale (ADR-015).
- **VPS-2**: бэкап-таргет — принимает ежедневные дампы `pg_dumpall` с VPS-1 (в `~/backups/` пользователя ubuntu).

### Прокси и таймауты (DO-021)

Перед шлюзом стоит **Caddy** — терминирует HTTPS на Tailscale-интерфейсе и проксирует
на `127.0.0.1:8080`. Топология: `клиент (tailnet) → Caddy → ApiGateway (YARP) → сервисы`.

Таймауты заданы явно (в `setup-caddy.sh` и `tailscale-tls.sh`):
- `read_timeout 60s` — сколько ждать ответ от шлюза/сервиса: медленные, но живые запросы
  (E2E-соматика 10–30 с) доходят без 502.
- `dial_timeout 10s` — таймаут установки соединения с апстримом.

Различимость отказов: сервис не слушает → Caddy отдаёт **502** (соединение не установлено);
сервис «думает» дольше 60 с → **504** (read_timeout). Раньше таймаутов не было — любой
долгий запрос выглядел как 502.

### Бюджет CI (DO-023)

Политика отмены прогонов (см. `.github/workflows/ci-tests.yml`):
- **`test`** — группа `ci-tests-test`, `cancel-in-progress: false`. Бюджет: дать отработать (зелёный ~69 с); GitHub не умеет «не отменять первые 5 мин», поэтому требование владельца реализовано как **не отменять вовсе**.
- **`front-e2e`** — отдельная группа `front-e2e`, `cancel-in-progress: false`: **никогда не отменяется**, всегда получает полные `timeout-minutes: 30`.
- **Правило приёмки:** отменённый run **не считается** ни зелёным, ни красным — валиден только прогон, завершившийся сам (`success`/`failure`).

Метрики длительности (p50/p95 по последним зелёным прогонам) — в `CI-BUDGET.md`
(обновляется `python tools/ci-metrics.py`).

### E2E и PIN стенда (DO-024)

⚠️ **Стенд dev используется и человеком.** Прогоны E2E/smoke требуют PIN `1234`.
`reset-dev-pin.sh` больше **не стирает** человеческий PIN (`DELETE` без `WHERE` убран):
если на стенде выставлен PIN ≠ 1234, сброс **отказывается** (`exit 1`) и прогон
пропускается. PIN один на всё развёртывание (модель `PinCredential` без
`ClientId`/`TenantId` — см. AR-008).

### Правило триггеров CI/CD (paths-фильтры)

Чат-сообщение **не должно** запускать тяжёлые джобы (деплой, сборку, тесты, синк).
Правило: **любой** workflow с `on.push` обязан иметь `paths` или `paths-ignore` —
иначе он триггерится на каждый пуш, включая переписку ролей (инцидент 2026-11-06:
«сообщение в чате → деплой в прод»).

- `deploy.yml` → `paths-ignore` (чат/задачи/доки/`.md` не триггерят dev→prod).
- `pages.yml` → `paths: 8-front/**`.
- `sync-board.yml` → `paths: 5-tech-backlog/**` (без `chat/**`).
- `ci-tests.yml` → `paths-ignore: 5-tech-backlog/chat/**`.

Проверка — `tools/check_workflow_paths.py` (в CI — джоб `guard-paths.yml`): падает,
если появился workflow с `on.push` без фильтра (allowlist — только `guard-paths.yml`).

## Порядок развёртывания

1. **VPS-2** (сначала — он отдаёт пароли БД):
   ```bash
   export PUBLIC_KEY="ssh-ed25519 AAAA... you@host"
   export VPS1_PRIVATE_IP=10.0.0.1 VPS2_PRIVATE_IP=10.0.0.2
   sudo -E bash 7-deploy/provision-vps2.sh
   # -> /opt/xdiary/7-deploy/secrets/.env (строки подключения) скопировать на VPS-1
   ```
2. **VPS-1**:
   ```bash
   export PUBLIC_KEY="ssh-ed25519 AAAA... you@host"
   sudo -E bash 7-deploy/provision-vps1.sh
   sudo tailscale up                      # авторизация; в админке включить MagicDNS + HTTPS Certificates
   # положить secrets/.env, затем из /opt/xdiary/7-deploy:
   sudo -u deploy bash setup-caddy.sh     # tailscale cert + caddy (ADR-015)
   sudo -u deploy podman compose -f podman-compose.yml up -d
   ```
3. **Проверки** (см. `vps-setup.md` §6): `podman ps` = 11, `nc -zv VPS2 5432` снаружи закрыто,
   `https://xdiary.vps1.ts.net` (через Tailscale) грузит PWA с валидным сертификатом, бэкап создаётся и восстанавливается.

## Примечание про podman

В Ubuntu 22.04 штатный `podman` — 3.4.4 (без подкоманды `compose`). Рабочие варианты:

- `podman-compose -f podman-compose.yml up -d` (python-обёртка, ставится вместе с пакетом `podman-compose`);
- либо обновить podman до 4.x и использовать `podman compose -f podman-compose.yml up -d`.

Лимиты (`mem_limit`, `pids_limit`) и `read_only`/`tmpfs` поддерживаются обоими инструментами
(в старых `podman-compose` `pids_limit` мог отсутствовать — тогда поднять podman до 4.x).

## Безопасность (обязательно)

- `secrets/.env` — `chmod 600`, **не коммитить** (репозиторий публичный; `.gitignore` уже исключает).
- SSH только по ключам (`PermitRootLogin no`, `PasswordAuthentication no`).
- Скрипты НЕ отключают парольный вход, пока не установлен `authorized_keys` (`PUBLIC_KEY`).
- PostgreSQL локальный на VPS-1 (контейнер `xdiary-pg`, доступен только внутри сети xdiary), наружу не публикуется.
