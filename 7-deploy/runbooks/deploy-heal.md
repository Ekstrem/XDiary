# Ранбук: деплой и heal стендов

> «Почему» — [`ADR-020`](../../04-architecture/ADL/adr-020.md) (CD pipeline, pull-based).
> Топология Caddy — [`7-deploy/caddy/README.md`](../caddy/README.md). Список сервисов —
> единственный источник `tools/gen-hosts.py`, он читает `7-deploy/podman-compose.yml`.

- **Класс:** деплой/передеплой стенда, heal сети и шлюза, «образ не обновился», 502
- **Владелец:** `@DO`
- **Когда применять:** 502/503 на стенде, `/clients` не отвечает, сервис крутит старый образ,
  после правки compose или `deploy.sh`/`update.sh`
- **ADR (почему):** `ADR-020`, `ADR-019` (среды)
- **Зависит от ADR (что править при изменении процесса):**
  | ADR | что здесь придётся изменить |
  |---|---|
  | `ADR-020` | меняется модель деплоя → все шаги раздела 2 |
  | `ADR-019` | меняются адреса/роли стендов → таблица «где что лежит» |
- **Дата ревизии:** 2026-11-02

## 1. Проверка перед началом

```bash
# 1) стенд вообще жив?
curl -s -o /dev/null -w 'health=%{http_code}\n' https://<стенд>/health
curl -s -o /dev/null -w 'clients=%{http_code}\n' https://<стенд>/api/v1/clients
# 2) что реально крутится
sudo podman ps --format '{{.Names}} {{.Status}}'
# 3) список сервисов, который ждём (12)
python3 /opt/xdiary/7-deploy/gen-hosts.py --check
```

Ожидание: `health=200`, `clients=200` (на dev корень `/` отдаёт **200 «API-only»**, а не 502).

## 2. Шаги

1. **Передеплой через штатный `update.sh`** (он сам решит, изменились ли образы):
   ```bash
   sudo bash /opt/xdiary/7-deploy/update.sh single
   ```
   В выводе должно быть: `сервисов 12 (из gen-hosts.py): api-gateway … clients`.
   Если строки нет или сервисов меньше — **остановиться**: генератор не прочитал compose.
2. **Проверить, что образ действительно новый** (главный урок: «контейнер Up» ≠ «образ свежий»):
   ```bash
   sudo podman inspect xdiary-client --format 'created={{.Created}}'
   sudo podman exec xdiary-client sh -c 'grep -ac AddJwtBearer /app/Client.Api.dll'
   sudo podman exec xdiary-client sh -c 'grep -ac UseAuthentication /app/Client.Api.dll'
   ```
   Ожидание: дата сегодняшняя, оба `grep` = `1`. **`0` означает старый образ** — именно так
   полдня искали «пустой `sub`», хотя в образе просто не было JWT-middleware.
3. **Проверить шлюз после пересоздания** — он ходит к сервисам по `--add-host`:
   ```bash
   sudo podman inspect xdiary-api-gateway --format '{{json .HostConfig.ExtraHosts}}'
   ```
   В списке обязан быть `clients:10.89.9.31`, иначе `/api/v1/clients` даёт 502.
4. **Heal, если портмап шлюза отвалился** (rootlessport-баг):
   ```bash
   sudo bash /opt/xdiary/7-deploy/heal-network.sh && sudo cat /var/log/xdiary-heal.log
   ```
   Если в логе `gen-hosts недоступен — НЕ пересоздаю шлюз` — это **правильное** поведение:
   лучше не тронуть, чем потерять сервисы. Чинить генератор, а не обходить.
5. **Проверить, что поднялось** (после heal шлюз поднимается ~8 с):
   ```bash
   curl -s -o /dev/null -w 'health=%{http_code}\n' http://127.0.0.1:8080/health
   curl -s -o /dev/null -w 'partners=%{http_code}\n' http://127.0.0.1:8080/api/v1/partners   # ожидаем 401
   ```

## 3. Гейты (кто и чем проверит)

| Гейт | Команда | Ожидание |
|---|---|---|
| Локальный | `gen-hosts.py --check` | `OK — 12 сервисов` |
| Стенд | `curl /health` | 200 |
| Авторизация | `curl /api/v1/partners` без токена | **401** (доказывает, что JWT обязателен) |
| CI | `smoke-api` / `front-e2e` | зелёный, завершился сам |
| **Негативный тест** | `podman rm -f xdiary-api-gateway` → `/health` = 502 → вернуть | стенд **падает** предсказуемо, heal поднимает |

## 4. Грабли (из реальных случаев)

- **Сервис не в списке `SVCS` → образ не тянется никогда.** Так `clients` отстал на 18 часов,
  и `front-e2e` падал с 401 на `/clients/me` — искали баг в коде, а причина была в списке.
  Проверка: в `update.sh` список берётся **только** из `gen-hosts.py`.
- **`update.sh`/`heal-network.sh` под `sudo` видят `$HOME=/root`.** Поэтому генератор ищется
  **рядом со скриптом** (`dirname "$0"`), а не в `$HOME/XDiary/...`; и `podman-compose.yml`
  копируется в каталог деплоя — иначе генератор не находит источник.
- **На сервере нет PyYAML.** `gen-hosts.py` имеет fallback-парсер; если сломать fallback —
  `update.sh` выходит с ошибкой (это специально: неполный набор хуже, чем «не обновил»).
- **Шлюз без `--add-host clients` → 502 на `/api/v1/clients`.** Контейнер, созданный старым
  `deploy.sh`, живёт с прежним набором хостов; пересоздание обязательно.
- **`podman inspect` показывает *локальный* образ, а не запущенный контейнер.** Дата
  «created» у контейнера выводится из его образа — если сомневаешься, смотри `Image` и
  сверяй `grep` по содержимому.
- **Деплой — это транзиент.** Сразу после `podman run` `/health` может быть 502 ещё
  несколько секунд. Не считать это регрессией: `front-e2e` из-за этого один раз упал за 43 с.

## 5. Критерий готовности

- [ ] `update.sh` напечатал 12 сервисов; в наборе есть `clients`.
- [ ] Образ клиента содержит `AddJwtBearer` и `UseAuthentication` (оба `= 1`).
- [ ] `GET /health` = 200, `GET /api/v1/clients` = 200, `GET /api/v1/partners` = 401.
- [ ] На dev: `GET /` = 200 «API-only»; на проде: `/` = 502 — **известное состояние**, не дефект.
- [ ] Изменения скриптов закоммичены, а не только применены на машине (`AGENTS.md` §5.2).

## 6. Откат

```bash
# вернуть предыдущий образ сервиса
sudo podman pull ghcr.io/ekstrem/xdiary-client:single   # нужен конкретный digest для откатa
sudo podman tag <digest> ghcr.io/ekstrem/xdiary-client:single
sudo bash /opt/xdiary/7-deploy/deploy.sh single
```
