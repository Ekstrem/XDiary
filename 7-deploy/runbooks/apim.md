# Ранбук: APIM (Gravitee) этап 1 + реестр контрактов

Решение — ADR-023/ADR-025; здесь только «как».

## 1. Что развёрнуто и как запускать

`7-deploy/apim/up.sh` — **единственный** поддерживаемый запуск. Он поднимает сеть
`apim`, `mongo`, `management-api` и `gateway` **явным `podman run`**.

**Почему не `podman-compose up` (проверено 2026-11-09):**

- compose 1.6.0 садит контейнеры в дефолтную сеть `podman`, игнорируя `external: true`;
- при каждом `up` mongo пересоздаётся → IP меняется → `management-api` падает
  `MongoTimeoutException` и уходит в петлю рестартов;
- в этой сети CNI-DNS не резолвит имена (`getent hosts` пусто, `nc: bad address`);
- `podman network create` пишет conflist `cniVersion 1.0.0` + плагин `firewall`, а
  установленный плагин понимает только `0.4.0` → контейнеры создаются и **не стартуют**.

Поэтому: `gravitee-compose.yml` остаётся **описанием**, запуск — `up.sh` (явный
`podman run --network apim`, лимиты 512/768 МБ, `-e` как у рабочего пробника).
`management-api` под compose выходит — причина в наборе env compose; запускаем явным
`podman run`, проверено 2026-11-09.

**Health** проверяется изнутри сети (`podman exec xdiary-apim-gateway wget …`), порт
management наружу не публикуется: на этой версии podman публикация через rootlessport
иногда роняет контейнер.

### 1.1. Порты (не путать — иначе контейнер падает на старте)

| Что | Порт | Почему именно так |
|---|---|---|
| node-management API **gateway** | `8072` | не `8082`: там прокси-слушатель, и при совпадении gateway падает `Unable to start HTTP server` |
| прокси gateway | `8082` (публикуется как `127.0.0.1:18082`) | сюда приходят запросы control plane |
| node-management API **management-api** | `8073` | не `8083`: там HTTP API |
| HTTP API management-api | `8083` | `/management/health` живёт здесь |

Проверка «health 200» (порт наружу не публикуется, поэтому изнутри сети):

```bash
MG_IP=$(podman inspect xdiary-apim-management --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
podman exec xdiary-apim-gateway wget -q -O- "http://$MG_IP:8083/management/health"
# ожидаем JSON со "status": "UP"; до готовности приложения — пусто/отказ (boot 60–90 c)
```

### 1.2. CNI-DNS отсутствует → адрес Mongo задаётся явно

В сети `apim` **имена не резолвятся** (`getent hosts xdiary-apim-mongo` → пусто,
`nc: bad address`). Поэтому:

1. `up.sh` поднимает `mongo`, берёт его IP из `podman inspect` и подставляет в
   `mongodb://<ip>:27017`;
2. перед запуском `management-api` скрипт **сверяет** «URI в конфиге == живой IP»
   и при расхождении перезаписывает и перезапускает контейнер.

Без этой сверки `management-api` падает `MongoTimeoutException` и уходит в петлю
рестартов — при каждом `up` IP mongo может измениться.

## 2. Негативный контроль (обязателен при любом изменении)

APIM **не в пути трафика**. Проверка:

```bash
cd /opt/xdiary/7-deploy/apim && podman-compose stop    # или podman stop трёх контейнеров
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/api/v1/clients   # ожидаем 200
```

Если не 200 — APIM попал в путь запросов, это дефект, а не «почти работает».

## 3. Реестр контрактов `openapi/` и гейты

- Реестр — снимок спек с работающих сервисов (`openapi/README.md`: стенд, дата, команда,
  ревизии образов). Руками не правим.
- **Дрейф** (реестр в git ≠ свежесгенерированные спеки):
  `python tools/check_openapi.py --registry openapi --generated <dir>` → rc=1 при расхождении.
- **Breaking-change** между коммитами:
  `python tools/check_openapi.py --registry openapi --base <git-rev>` → rc=1 на удалённый
  маршрут/метод, снятый обязательный параметр, суженный enum; добавления не ломают.
- Самопроверка гейта: `python tools/check_openapi.py --selftest` (4 кейса, включая
  «добавление — не breaking»).

Путь реестра — **параметр**, а не константа: инкремент 2 BE-050 переносит генерацию в CI,
гейт при этом не переписывается.

## 4. Границы

- Управление трафиком через Gravitee **не включено** и включается только решением владельца
  (ADR-025): сейчас APIM — control plane для реестра и сверки, не прокси.
- Elasticsearch в этап 1 не входит: для маршрутизации не нужен, стоит ~875 МБ.
