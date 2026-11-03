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
