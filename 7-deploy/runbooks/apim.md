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
