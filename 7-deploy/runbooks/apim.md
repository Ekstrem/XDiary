# Ранбук: Gravitee APIM (DO-056, contract-first)

> **SSH-порт (2026-11-13):** на VPS-1 и VPS-2 sshd слушает **8237**, 22 закрыт; вход только по ключу
> (`PasswordAuthentication no`, `PermitRootLogin no`). Во всех командах ниже — `-p 8237`.

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

## Точное место, где упирается бутстрап (2026-11-04, измерения)

Состояние сейчас: `apim-management` — **200**, `apim-gateway` — `127.0.0.1:18082`,
`apim-management-ui` — `127.0.0.1:18084` (консоль, только loopback; поднимается `up.sh run_ui`).

**Basic auth `admin`/`admin` — аутентификация ПРОХОДИТ** (встроенный memory-провайдер
в `gravitee.yml`: `admin` / пароль `admin`, роли `ORGANIZATION:ADMIN,ENVIRONMENT:ADMIN`).
Доказательство: до входа любой `/management/v2/*` даёт **401**, после `-u admin:admin` —
**500**, то есть запрос дошёл до бизнес-логики.

**Почему 500:** в логе платформы
`java.lang.IllegalArgumentException: The given id must not be null`
→ `MongoOrganizationRepository.findById` → `OrganizationServiceImpl.findById`
← `UserServiceImpl.create` (user→org lookup при создании пользователя из IdP).
То есть платформа пытается **создать** пользователя из memory-IdP, у IdP-пользователя нет
`organizationId`, и создание падает. Значит установка (install) не завершена — её и
выполняет консоль.

**Следствие:** админ «существует» для аутентификации, но API не работает, пока не пройден
install-шаг консоли. Поэтому импорт спек (`import.sh`) запускать нечем: любой
`POST …/apis/import` упадёт так же.

## Рабочая последовательность (проверено 2026-11-04: 12/12 API STARTED)

**Вход:** HTTP **Basic** `admin`/`admin` (штатный memory-провайдер `gravitee.yml`).
**Пути:** только с контекстом — `/management/v2/organizations/DEFAULT/environments/DEFAULT/…`
(без них платформа отвечает `500 The given id must not be null`).

1. **Импорт спеки** (по одной на сервис):
   `POST …/apis/_import/swagger` с телом
   `{"payload": "<содержимое openapi/<svc>.json строкой>", "withDocumentation": true}`.
   Альтернативные пути из старых версий (`/apis/import`) дают **405** — их в 4.4 нет.
2. **План обязателен:** импортированный API создаётся **без планов**, а `POST …/apis/{id}/_start`
   отвечает `400 api.NotDeployable: can not be started without at least one published plan`.
   Создание плана: `POST …/apis/{id}/plans`
   ```json
   {"name":"Default","description":"auto","security":{"type":"KEY_LESS"},
    "mode":"STANDARD","validation":"MANUAL","definitionVersion":"V4","flows":[]}
   ```
   Важно: `mode` — **`STANDARD`** (значение `PROXY` не существует и даёт ошибку десериализации);
   `security` — **объект** `{"type":"KEY_LESS"}` (строка `"KEY_LESS"` не принимается).
3. **Публикация плана:** `POST …/apis/{id}/plans/{planId}/_publish` → `200`.
4. **Запуск API:** `POST …/apis/{id}/_start` → `204`.
5. **Проверка:** `GET …/apis?page=1&per_page=10` и `page=2` (по умолчанию страница = 10,
   `per_page` больше 10 не увеличивает её) → у всех `"state": "STARTED"`.

Шлюз APIM слушает `127.0.0.1:18082`. Консоль — `127.0.0.1:18084` (`up.sh run_ui`).

## Публичный вход: APIM В ПУТИ (2026-11-05, DO-076 — после исправления)

**Действительный путь (проверено снаружи):**

```
Tailscale Funnel  --(HTTP)-->  Caddy :8081
                                  ├─ handle /health*  -> Caddy  `respond "ok" 200`  (ADR-030: liveness края, приложение не involved)
                                  └─ handle           -> APIM   127.0.0.1:18082
```

**Три ошибки, которые были допущены до этого — их надо помнить:**

1. **Funnel не ходит в Caddy сам по себе.** `tailscale funnel status` показывал
   `/ proxy http://127.0.0.1:8080`, то есть трафик шёл **прямо в YARP**, минуя Caddy
   (это и есть `DO-067`). Правка `Caddyfile` на публичный путь не влияла.
   **Проверять всегда:** `sudo tailscale funnel status`.
2. **«Проверка с прода» — не проверка.** На самом проде имя `xdiary-vps1.taile0b6ee.ts.net`
   резолвится локально и запрос попадает в **Caddy на :443**, а не в Funnel. Поэтому с прода
   видно APIM, а снаружи — YARP. Мерить публичный вход можно **только с другой машины**
   (dev подходит: `ssh -p 8237 ubuntu@77.91.73.67`).
3. **Funnel терминирует TLS сам**, поэтому его апстрим — **HTTP**-порт. Отдать ему Caddy `:443`
   нельзя; и сайт Caddy должен матчить любой Host (`http://:8081`, не `http://127.0.0.1:8081`),
   иначе Caddy не находит сайт и отдаёт **пустой `200` на все пути** (включая `/internal/**`).

**Как включить (идемпотентно, на проде):**

```bash
# 1) HTTP-listener в Caddy (в конец Caddyfile):
#    http://:8081 { handle /health* { respond "ok" 200 } handle { reverse_proxy 127.0.0.1:18082 } log }
sudo caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
sudo systemctl reload caddy

# 2) проверка listener'а с публичным Host (иначе 200-пусто):
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: xdiary-vps1.taile0b6ee.ts.net' \
  http://127.0.0.1:8081/api/v1/catalog          # ждём 401

# 3) Funnel -> этот listener (HTTP, не 443):
sudo tailscale funnel --bg 8081
sudo tailscale funnel status                    # ждём `/ proxy http://127.0.0.1:8081`
```

**Откат одной командой:** `sudo tailscale funnel --bg 8080` (прежний прямой путь в YARP);
проверено исполнением — после отката `/health` локально 200.

**Приёмка — только снаружи** (с dev), 2026-11-05 04:0x:

| путь | код | `x-gravitee-*` | кто ответил |
|---|---|---|---|
| `/health` | 200 | 0 | **Caddy** (`respond`, ADR-030). До 2026-11-05 шёл в YARP; после правки заголовок `Server: Caddy`, а не `Kestrel` — это и есть дискриминатор |
| `/api/v1/catalog` | 401 | **3** | **APIM** |
| `/api/v1/journal/records` | 401 | **3** | **APIM** |
| `/api/v1/nonexistent-path-xyz` | 404, тело `No context-path matches the request URI.` (40 Б) | 0 | **APIM** |
| `/internal/journal/records` | 404 | 0 | **APIM** (нет маршрута) |

**Признак «кто ответил» без заголовков:** путь без маршрута — APIM даёт `404` + тело
`No context-path matches the request URI.`, YARP даёт `401` + `Unauthorized` (12 Б).
Заголовки `Via`/`X-Gravitee-*` могут срезаться промежуточными прокси, а код и тело — нет.

**Латентность снаружи (с dev, n=10):** `/health` p50 101 / p95 119; `/journal` 101/118;
`/catalog` 97/127; `/partners` 100/121 мс — в порогах 145/138/146/134.

**Метрики:** `podman stats --no-stream` по четырём контейнерам APIM; журнал Caddy —
`journalctl -u caddy`; коды и p95 — команды выше.

## Четыре точки замера — почему «переключено» полдня было ложным

Одно и то же имя `xdiary-vps1.taile0b6ee.ts.net` и один и тот же путь дают **разные ответы**
в зависимости от того, **откуда** мерить. Это не баг, а разные хопы:

| точка замера | что видно | как проверить |
|---|---|---|
| **снаружи по имени (интернет)** | `via: 1.1 Caddy` + `X-Gravitee-*`; неизвестный путь → **404** | `curl --resolve xdiary-vps1.taile0b6ee.ts.net:443:<IP ingress>` |
| **с прода/tailnet по имени** | то же, что и Caddy `:443` (tailnet-IP `100.x`) | `curl https://xdiary-vps1…` **с прода или с dev** |
| **APIM напрямую** | `401` + `X-Gravitee-Client-Identifier` | `curl http://127.0.0.1:18082/api/v1/catalog` |
| **YARP напрямую** | `401` **без** Gravitee, тело `Unauthorized` (12 Б) | `curl http://127.0.0.1:8080/api/v1/catalog` |

**Правило:** публичный вход доказывается только точкой, которую видит **пользователь** —
`--resolve` на ingress-IP Funnel или машина **вне tailnet**. Замер с прода/dev идёт tailnet-путём
и доказывает состояние **хопа** (Caddy), а не то, что интернет идёт через APIM.

**Дискриминатор без заголовков** (прокси может срезать `Via`/`X-Gravitee-*`):
неизвестный путь — APIM даёт `404` + тело `No context-path matches the request URI.` (40 Б),
YARP даёт `401` + `Unauthorized` (12 Б).

## Подъём APIM на контуре с rootful podman (dev/VPS-2) — грабли, проверенные 2026-11-05

Порядок, который реально приводит к запуску (иначе теряется час):

1. **RAM.** APIM нужен ≈1.26 ГБ RSS. На VPS-2 было 1963 МБ при 87 МБ свободных → без swap стек
   не поднимется. Проверка: `free -m`, `swapon --show`. Временное решение — swap 2 ГБ:
   `sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile &&
   sudo swapon /swapfile`, затем строка `/swapfile none swap sw 0 0` в `/etc/fstab`.
2. **CNI.** На **rootful** podman `podman network create apim` пишет в `/etc/cni/net.d/` конфиг с
   `cniVersion: 1.0.0` и плагином `firewall`, который в установленной версии понимает только
   `0.4.0`. Симптом: `Error: ... CNI network "apim" not found`, контейнеры создаются и не стартуют.
   Лечение — свой конфиг `/etc/cni/net.d/10-apim.conflist` с `cniVersion: 0.4.0`, своим бриджем и
   подсетью (пример: bridge `cni-podman30`, `10.89.30.0/24` — не пересекается с сервисной
   `10.89.9.0/24`). На **rootless** (прод) тот же файл лежит в `~/.config/cni/net.d/apim.conflist`.
3. **Запуск.** `sudo HOME=/root bash /opt/xdiary/7-deploy/apim/up.sh` (rootful) — скрипт ждёт, что
   сеть уже существует.
4. **Проверка.** `podman ps` (4 контейнера `running`, `restarts=0`), `ss -ltn | grep 18082`,
   `podman exec xdiary-apim-management curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8083/management/v2/`
   → `200`.
5. **Install — отдельный шаг, API его не делает.** `GET /management/v2/organizations` пуст, а
   `POST /management/v2/organizations` (`admin:admin`) отвечает **500** — первый админ и организация
   создаются **консолью** (`127.0.0.1:18084`, только через ssh-туннель). Без этого `import.sh`
   запускать нечем: импорт 12 API и `check_apim_drift` заблокированы.

## Что изменилось 2026-11-05 (ADR-030, `DO-083`-смежное)

`/health` больше не проксируется в YARP: его отдаёт **Caddy** (`respond "ok" 200`) — ADR-030
называет это liveness края. Проверка, что прокси нет: заголовок ответа **`Server: Caddy`**
(при проксировании в сервис было `Server: Kestrel`).

Замер после правки (на проде):
`http://127.0.0.1:8081/health` → **200 `Server: Caddy`**; tailnet-host `/health` → **200 `Server: Caddy`**;
`/api/v1/catalog` → **401** и **3** заголовка `x-gravitee` (APIM в пути не изменился);
неизвестный путь → **404**. Откат — вернуть `reverse_proxy 127.0.0.1:8080` из
`/etc/caddy/Caddyfile.bak-do-<TS>` и `systemctl reload caddy`.

**Смысл:** YARP в публичном пути больше **не участвует ни одной строкой** — до этого
`/health` был единственной оставшейся нагрузкой YARP, то есть фактическим «вторым шлюзом».

## Drift с APIM: три режима и что каждый доказывает (2026-11-05, @AR 19:05)

`tools/check_apim_drift.py` запускается в трёх разных состояниях, и **их `rc` нельзя смешивать**:

| режим | что делает | `rc` | что ДОКАЗЫВАЕТ |
|---|---|---|---|
| `--specs-only` | сверяет `api-index.yaml` с `openapi/*.json` | `0` | только «индекс и спеки согласованы между собой» |
| без `GRAVITEE_TOKEN` | до APIM не доходит | `2` | ничего (НЕ ИЗМЕРЕНО), печатается `::warning::` |
| с `GRAVITEE_TOKEN` | опрашивает Management API изнутри контейнера | `0`/`1` | дрейф «реестр ↔ APIM» по операциям |

**Ключевое (уточнение @AR, внесено по его просьбе):** `--specs-only` → `rc=0` **не является**
доказательством отсутствия дрейфа. Он говорит только, что реестр и спеки не разошлись между собой;
про APIM он не знает ничего — в ответе прямо пишется «APIM не опрашивался». Приводить его как
«дрейфа нет» — та же ошибка, что «зелёный, потому что не смотрел».

### Почему серая зона была вечной

`GRAVITEE_TOKEN` не был подключён **нигде**: `git grep GRAVITEE_TOKEN -- .github tools` давал
**0 совпадений**, то есть ветка сравнения с APIM **никогда не исполнялась** ни в CI, ни вручную.
Инструмент при этом честно писал `rc=2`, но в сводках это читалось как «серая зона».
Исправлено: добавлен `.github/workflows/apim-drift.yml` (`workflow_dispatch`), который передаёт
`secrets.GRAVITEE_TOKEN`, копирует инструмент и `api-index.yaml` на прод и запускает сверку там
(Management API доступен только изнутри: `127.0.0.1:8083`).

### Квитанция

Инструмент печатает шапку `REF=<sha> | образ=<image> | время=<UTC ISO>` (в CI `REF` из `GITHUB_SHA`),
а расхождения — с указанием, **что править**: `есть в реестре (индекс/спеки), НЕТ в APIM` и
зеркально `есть в APIM, НЕТ в реестре (индекс/спеки)`.

### Запуск одной командой (на проде, с токеном)

```bash
GRAVITEE_TOKEN=<токен> python3 tools/check_apim_drift.py \
  --root . --api http://127.0.0.1:18083 --container xdiary-apim-management
```

Без секрета шаг остаётся **видимо красным** (`rc=2`) — так и должно быть: серая зона не
закрашивается в зелёный «на всякий случай».

### Поправка 2026-11-05 19:20: UI уже есть, тупик другой

Выше (2026-11-04) записано «UI-контейнера нет, кнопки «создать админа» физически не существует».
**Это устарело:** контейнер `xdiary-apim-ui` (`graviteeio/apim-management-ui:4.4`) поднят и работает,
порт опубликован на **`127.0.0.1:18084`**, `GET /` → **200**.

Проверено сейчас:

| проверка | факт |
|---|---|
| `podman port xdiary-apim-ui` | `8080/tcp -> 127.0.0.1:18084` |
| UI отвечает | **200** на `http://127.0.0.1:18084/` |
| mongo `admin.users` | **0** (админ не создан) |
| `/management/v2|v3/installation`, `/v3/organizations` | **401** (нужен токен, взять негде) |

**Значит настоящий блокер — не отсутствие UI, а непройденный мастер первого запуска.**
Мастер открывается в браузере через туннель:

```bash
ssh -p 8237 -L 18084:127.0.0.1:18084 deploy@31.192.111.168
# затем открыть http://localhost:18084 и пройти первичную настройку (создать админа)
```

После создания админа: токен Management API → `/opt/xdiary/secrets/apim.env`
(**в репозиторий не попадает**, §6; в CI — секрет репозитория `GRAVITEE_TOKEN`).

`admin.users = 0` — это же объясняет, почему `GRAVITEE_TOKEN` не был подключён нигде:
его неоткуда было взять, а не «забыли».

### `apim.env`: креды лежат, но НЕ работают (проверено 2026-11-05 22:2x)

`/opt/xdiary/secrets/apim.env` (75 байт, 2 строки, создан 2026-11-04 08:34) содержит
**`GRAVITEE_ADMIN_USER`** (5 символов) и **`GRAVITEE_ADMIN_PASSWORD`** (24 символа).
Токена (`GRAVITEE_TOKEN`) там **нет**.

Проверка логина с этими кредами (значения не печатались, только коды):

| эндпоинт | код |
|---|---|
| `/management/v3/organizations/DEFAULT/environments/DEFAULT/login` | **401** |
| `/management/organizations/DEFAULT/environments/DEFAULT/login` | **401** |
| `/management/v2/user/login` | **401** |

**Вывод:** файл вводит в заблуждение — выглядит как «доступ есть», а логин не проходит
(`mongo admin.users = 0`, инициализатора админа в 4.4 нет). Настоящий путь остаётся тот же:
пройти мастер первого запуска в UI (`127.0.0.1:18084` через туннель), затем **положить рабочий
`GRAVITEE_TOKEN`** — либо **удалить `apim.env`**, чтобы он не читался как источник доступа.
Решение за владельцем секрета (@DO); удаление без токена — предпочтительнее, чем «креды, которые не работают».

## Портал для пользователей API (portal UI) — опционально

Поднимается тем же `up.sh` с флагом `--with-portal`, **только на loopback**:

```bash
bash $HOME/apimtools/up.sh --with-portal
# portal-ui: 127.0.0.1:18085  (образ graviteeio/apim-portal-ui:4.4, лимит 256 МБ)
```

Проверка (2026-11-06): `127.0.0.1:18085/` → **200**, контейнер `running`, `restarts=0`, потребление ≈58 МБ,
свободной памяти на проде оставалось ≈2.9 ГБ.

Туннель с рабочей машины (портал):

```bash
ssh -p 8237 -i <ключ> -N -L 18085:127.0.0.1:18085 deploy@31.192.111.168
# браузер: http://localhost:18085
```

Без флага контур поднимается как раньше (без портала) — так экономится память.
Управленческая консоль (`18084`) от флага не зависит и работает всегда.
