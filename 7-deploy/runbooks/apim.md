# Ранбук: Gravitee APIM (DO-056, contract-first)

Решение владельца 2026-11-04: контракты правятся **в APIM**, а не в коде; реестр OpenAPI
(`openapi/*.json`, `BE-050`) — источник для импорта; гейт `DO-056` следит за расхождением.

## Что есть на проде (факты 2026-11-04, проверено командами)

| Компонент | Состояние |
|---|---|
| `xdiary-apim-management` | `graviteeio/apim-management-api:4.4`, **Up** |
| `xdiary-apim-gateway` | `graviteeio/apim-gateway:4.4`, опубликован как `127.0.0.1:18082 -> 8082` |
| `xdiary-apim-mongo` | `mongo:6`, Up |
| Management API | **порт 18083 на хост НЕ опубликован**; изнутри контейнера `:8083/management/v2/` → **200** |
| `openapi/` на проде | **0 файлов** — реестр ещё не выкачен (`BE-050`) |

Проверка одной командой (на проде):

```bash
podman exec xdiary-apim-management curl -s -o /dev/null -w '%{http_code}\n' \
  http://127.0.0.1:8083/management/v2/          # ждём 200
```

**Почему не публикуем 18083 наружу:** гейту и импорту достаточно `podman exec` внутри
контейнера; лишний порт на хосте — лишняя поверхность атаки без пользы.

## Drift-check (гейт)

```bash
GRAVITEE_TOKEN=<management-token> python tools/check_apim_drift.py --root /opt/xdiary
```

Сравнивает `operationId` из `04-architecture/api-index.yaml` и `openapi/*.json` с тем, что
реально опубликовано в APIM. `::error::(apim) <op> — есть в индексе/спеках, нет в APIM`
и наоборот; `rc=1` при расхождении.

**Важно про «нет токена»:** без `GRAVITEE_TOKEN` гейт возвращает **rc=2** и предупреждение
«сравнение не выполнено». Это не «совпадает» — специально, чтобы отсутствие проверки не
выглядело как успех.

## Импорт спек (когда `BE-050` даст 11/11)

1. Забрать `openapi/*.json` из `origin` на хост прода (это делает деплой).
2. Импортировать как API-определения в Management API (`--container xdiary-apim-management`):
   по одному API на сервис, с версией = `lock@<sha>` (ADR-027 §3.3).
3. Опубликовать версию (publish), затем прогнать `check_apim_drift` — он и есть приёмка.

Импорт запускается **только** после того, как реестр полон: импорт неполного реестра создаёт
APIM-состояние, которое потом придётся чинить руками (та же ошибка, что «применить миграции
вручную»).

## Открытые пункты

- `openapi/*.json` сейчас **2 из 11** (client, analytics) — импорт ждёт `BE-050`.
- Токен Management API: нужен от владельца/Apim-админа; в репозитории его нет и быть не должно (§6).

## Bootstrap админа: три проверенных тупика (2026-11-04) и вывод

Проверено на проде, чтобы не искать заново:

1. **Ручная вставка пользователя в mongo** (`users` + `memberships` с bcrypt-хешем,
   `source: memory`, `status: ACTIVE`) → запись создаётся (`users=1`), но
   `POST /management/v2/user/login` → **401**. Значит Gravitee проверяет больше, чем
   пару «хеш + активность», и ручная схема — ловушка.
2. **`gravitee_installation_type=standalone` + чистая база** → инициализаторы проходят
   (`DefaultParameter`, `DefaultTheme`, `IdentityProvider`, …), но **инициализатора
   пользователя в списке нет**, `users=0`, логин **401**.
3. **`POST /management/v2/installation` на пустой платформе** (после удаления
   `organizations`/`environments`) → **401**: эндпоинт требует токен, которого взять негде.

**Вывод:** в `apim-management-api:4.4` **нет инициализатора первого администратора**;
в Gravitee 4.x его создаёт **Management Console UI** (мастер первого запуска). У нас
в `up.sh` поднимаются только `mongo` + `management-api` + `gateway` — **UI-контейнера нет**,
поэтому кнопки «создать админа» физически не существует.

**Что делать (следующий шаг, зона @DO):** добавить в `up.sh` `graviteeio/apim-management-ui:4.4`
(loopback-порт, например `127.0.0.1:18084`), пройти мастер один раз, затем:
пароль/токен → `/opt/xdiary/secrets/apim.env` (в репозиторий не попадает, §6),
`check_apim_drift` и импорт спек начинают работать.

## Импорт спек (DO-056)

Реестр готов: **12 спек / 80 операций** (`openapi/*.json`, `_meta.json` рядом).

```bash
# план без токена (работает всегда)
bash 7-deploy/apim/import.sh --dry-run

# импорт + публикация версии (нужен GRAVITEE_TOKEN в /opt/xdiary/secrets/apim.env)
bash 7-deploy/apim/import.sh

# проверка расхождения «спеки <> APIM»
GRAVITEE_TOKEN=$(grep ^GRAVITEE_TOKEN= /opt/xdiary/secrets/apim.env | cut -d= -f2) \
  python3 tools/check_apim_drift.py --root /opt/xdiary
```

Скрипт делает по одному API на сервис (`POST …/apis/import`, `type=BOTH`), затем публикует
(`POST …/apis/{id}/deployments`) и печатает `id` каждого API.

**Семантика `check_apim_drift` (уточнено 2026-11-04):** drift считается **спеки ↔ APIM**
(реестр — источник импорта), а не «индекс ↔ APIM». Поэтому:
- спек нет и APIM пуст → `rc=0` (импортировать нечего);
- спеки есть, APIM пуст → `rc=1` (импорт не запускали — это и есть расхождение);
- `--specs-only` — посчитать индекс/спеки без обращения к APIM (для CI и при отсутствии токена).
## Снятие спек с dev (проверено 2026-11-04)

```bash
# 1) включить swagger в dev (флаг кода: Swagger:Enabled, по умолчанию false)
grep -q '^Swagger__Enabled=' /opt/xdiary/7-deploy/secrets/.env || \
  echo 'Swagger__Enabled=true' | sudo tee -a /opt/xdiary/7-deploy/secrets/.env

# 2) пересоздать контейнеры штатно (env-file подхватывается только при создании)
cd /opt/xdiary/7-deploy && sudo bash deploy.sh

# 3) снять спеку с живого сервиса (curl В ХОСТЕ: в .NET-образах curl нет)
ip=$(sudo podman inspect xdiary-catalog --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
curl -s "http://$ip:8080/swagger/v1/swagger.json" -o catalog.json
```

**Факт, который меняет сверку:** живой `swagger.json` (.NET 8 + Swashbuckle) **не содержит
`operationId`** — только `paths` и методы. Значит реестр с кодом сверяется **по `(method, path)`**;
`operationId` в `api-index.yaml` — производное имя действия контроллера.

**Замер 2026-11-04 (dev, 9 сервисов из 12):** `analytics 8/8`, `catalog 8/8`, `client 8/8`,
`privacy 9/9`, `daily-metrics 5/5`, `garmin-gateway 4/4`, `portability 4/4`,
`apple-health-gateway 3/3`, `somatic-series 2/2` → **51 = 51, расхождений 0**.
`journal`/`partners` снять нельзя: рестарт-петля (`RestartCount=20`), в логе
`Cannot resolve parameter 'JournalEventHandler'` — дефект DI (@BE). `api-gateway` — `401`
(swagger закрыт авторизацией).

**Почему dev вообще не поднимался:** на dev, как и на проде, `clone: No space left on device`
при свободном диске — виноваты **лимиты namespace**: `user.max_*_namespaces` подняты до
`1048576`, после чего `deploy.sh` создал все 13 контейнеров.

## Bootstrap админа: почему нельзя и что осталось (проверено 2026-11-04)

Пять путей проверены на живом проде — **ни один не даёт админа**:

1. **Инициализатора админа в Gravitee 4.4 нет.** `grep` по всем jar-ам
   (`DefaultUserInitializer`, `InstallationInitializer`, `installDefaultUser`) → **0**.
2. **У пользователя нет поля `username`.** В байткоде `UserMongo` поля: `id`, `email`,
   `password`, `firstname`, `lastname`, `source`, `sourceId`, `status`, `organizationId`,
   `picture`, `newsletterSubscribed`, `createdAt`, `updatedAt`, `lastConnectionAt`,
   `firstConnectionAt`, `loginCount`. Логин — это **`_id`**, не имя.
3. **Эндпоинта `/management/v2/user/login` в 4.4 нет**: в `management-v2-rest` 1192 класса,
   среди них ни одного `Login`/`Auth`/`Token`-ресурса. Поэтому любой `POST` даёт **401** от
   security-фильтра — это «эндпоинта нет», а не «неверный пароль». Ошибка читается неверно.
4. **Прямая вставка в `users` + `memberships`** (bcrypt `$2b$`, `source: memory`,
   `status: ACTIVE`, роль `ORGANIZATION/ADMIN`) → запись есть, Basic/Bearer → **401**.
   Причина в п.2–3: проверять пароль некому.
5. **`memory`-провайдер через env** (`gravitee_security_providers_0_type=memory` +
   `_users_0_username/password`) → management **не поднялся** (3.5 мин без ответа);
   откатил на рабочую конфигурацию, `/management/v2/` = **200**.

**Вывод:** первый админ создаётся только **Management Console UI** (`graviteeio/apim-management-ui`),
которого в `up.sh` нет. До его появления `DO-056` (импорт + drift-check) остаётся
заблокированным: `import.sh`/`check_apim_drift` без токена дают `rc=2`, а не ложный зелёный.

**Осторожно:** пересоздание `xdiary-apim-management` занимает ~45 с, и в это время
`/management/v2/` не отвечает. `heal-network.sh` это не лечит (он про сеть), поэтому
пересоздавать только осознанно и проверять `200` после.
