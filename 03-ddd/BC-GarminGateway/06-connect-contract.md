# Контракт подключения Garmin по токену (AR-163)

> **Источник:** владелец 2026-11-20 — в настройках нужен диалог ввода токена Garmin.
> **Для:** FE-057 (UI), BE-142 (серверная реализация и тесты).
> **Граница:** PWA не делает OAuth-redirect и не вызывает Garmin API напрямую. Пользователь получает токен вне PWA и вставляет его в настройках; дальше работает серверный прокси GarminGateway. CSV-импорт остаётся запасным путём.

## 1. `POST /api/v1/garmin/connect`

Требуется JWT приложения. `clientId` берётся **только** из JWT и не передаётся телом.

Тело:

```json
{
  "credentials": {
    "apiKey": "<строка вида ключа>",
    "oauthToken": "<строка вида токена>",
    "tokenExpiry": "<ISO-8601 или null>"
  }
}
```

| Поле | Тип | Обязательность | Правило |
|---|---|---|---|
`credentials.apiKey` | string | **да** | непустое; серверный секрет |
`credentials.oauthToken` | string | **да** | непустое; серверный секрет |
`credentials.tokenExpiry` | string \| null | нет | ISO-8601 с часовым поясом; отсутствие/пустое значение = срок неизвестен |

Повторный `Connect` для того же `clientId` и `sourceId="garmin"` **заменяет** credentials и сбрасывает `requiresReauth`; второй источник Garmin у того же клиента не создаётся.

## 2. Единый ответ о состоянии — `GarminConnectionState`

`Connect` при успехе и `GET /api/v1/garmin/state` возвращают **один и тот же** DTO:

| Поле | Тип | Смысл |
|---|---|---|
`connected` | bool | credentials для `clientId`/`garmin` сохранены |
`sourceId` | string | всегда `garmin` |
`tokenExpiry` | string \| null | срок из Connect, без нормализации в секрет |
`metrics` | string[] | выбранные метрики синхронизации |
`windowDays` | int | окно синхронизации |
`lastSync` | string \| null | последняя успешная синхронизация |
`requiresReauth` | bool | токен истёк или Garmin отклонил credentials; нужен повторный Connect |

**В ответах нет** `apiKey`, `oauthToken` и их фрагментов. Секреты не печатаются в логи и не включаются в пользовательские сообщения.

`GET /state`:
- `200` — есть сохранённое подключение, **в том числе истёкшее** (`connected=true`, `requiresReauth=true`);
- `404` — подключения для текущего `clientId` ещё нет.

## 3. `POST /api/v1/garmin/sync`

Тело: `{ "config": { "metrics": ["<metric>"], "windowDays": 30 } }`.

- `metrics` — массив строк; `windowDays` — положительное целое;
- успешный `200` означает, что конфигурация принята и синхронизация через серверный прокси выполнена/обновила `lastSync`;
- `POST /fetch-activities` использует сохранённые credentials тем же скоупом владельца; тела нет.

## 4. Коды и машиночитаемые ошибки

| Ситуация | HTTP | `code` / результат |
|---|---|---|
успешный Connect/Sync/FetchActivities | 200 | без секретов в теле |
пустые `apiKey`/`oauthToken`, неверный `tokenExpiry`/`windowDays` | 400 | `validation_error` |
Sync/FetchActivities до Connect | 400 | `not_connected` |
токен истёк или Garmin отклонил credentials | 400 | `token_expired` |
Garmin/сеть недоступны, токен при этом не признан истёкшим | 502 | `upstream_unavailable` |
нет/невалиден JWT приложения | 401 | контракт `edm-canon.md` §25 |
State до Connect | 404 | состояния нет |

Для `400` и `502` тело — `{"code": "<из таблицы>", "message": "<безопасный текст>"}`; внутренний ответ Garmin, stack trace и секреты наружу не попадают.

## 5. Истечение и `ReauthOnExpiry`

1. Если `tokenExpiry <= now` **или** Garmin отвечает отказом аутентификации, сервер помечает состояние `requiresReauth=true`.
2. `State` продолжает отвечать `200` с `connected=true` и `requiresReauth=true`.
3. `Sync`/`FetchActivities` не делают повторных попыток с истёкшим токеном и отвечают `400 token_expired`.
4. Автоматического refresh нет: UI показывает «Срок действия токена Garmin истёк. Вставьте новый токен.» и открывает тот же диалог Connect.
5. Новый успешный Connect заменяет credentials и возвращает `requiresReauth=false`.

## 6. Что должен изменить BE-142 относительно текущего кода

Это **целевой контракт**, а не утверждение, что он уже реализован:
- убрать credentials из ответов `Connect`/`State` (сейчас возвращается модель/read-model с секретами);
- ввести `GarminConnectionState` и машиночитаемые ошибки из §4;
- реализовать `requiresReauth` и блокировку Sync/FetchActivities при истечении;
- сохранить per-client скоуп и добавить Domain/ApiTests на Connect, повторный Connect, истечение и отсутствие секретов в ответе.

После реализации @BE синхронизирует `04-architecture/api-index.yaml` с кодом через существующий гейт; этот файл остаётся единственным **продуктово-архитектурным** источником сценария и семантики ошибок.
