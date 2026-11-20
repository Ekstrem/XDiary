# Aggregate Design Canvas: GarminSync

> Bounded Context: Шлюз Garmin (GarminGateway)
> Агрегат: GarminSync
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **GarminSync** — состояние интеграции с Garmin (учётка, токен, окно синка).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием синка с Garmin.

> **Подключение (backlog G-01/G-06; владелец 2026-11-20):** в настройках есть диалог **«Подключить Garmin»**, в который пользователь вставляет полученные вне PWA `apiKey`/`oauthToken` и при наличии `tokenExpiry`. PWA **не выполняет** OAuth-redirect и не вызывает Garmin API напрямую: после `Connect` синхронизацию выполняет **серверный прокси** (GarminGateway = ACL). Локальный CSV-импорт остаётся **запасным путём**, а не заменой подключения по токену. Контракт: `06-connect-contract.md`.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **GarminSync** — совокупность признаков, идентифицирующих источник у конкретного клиента:
  - `clientId` (uuid) — чей Garmin (клиент, ADR-021)
  - `sourceId` (string) — «garmin»

**Объект-значения (Value Objects):**
- **Credentials**: apiKey, oauthToken, tokenExpiry
- **SyncConfig**: metrics[], windowDays
- **LastSync**: timestamp

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`IGarminSyncAnemicModel` (`Root`, `Credentials`, `SyncConfig`, `LastSync`) + `GarminSyncRoot` (`ClientId`, `SourceId`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.ClientId` | Guid | 1 | **да** | `Link(clientId)` | скоуп владельца; токен/креды одного клиента не используются для другого | ADR-008 |
| `Root.SourceId` | string | 1 | **да** | `Link` | идентификатор пользователя у провайдера; уникален в рамках `ClientId` | US-008 |
| `Credentials` | `ICredentials` (VO) | 1 | **да** | `Link`, `RefreshToken` | **секрет**: хранится только на сервере, в логи и события не попадает (§6 AGENTS) | ADR-021 |
| `SyncConfig` | `ISyncConfig` (VO) | 1 | да | `Configure` | период/типы данных для синхронизации | US-008 |
| `LastSync` | string (timestamp) | 0..1 | нет | `Sync` | монотонно; используется как курсор «с чего продолжать», поэтому откат назад ломает инкремент | US-008 |

**2.2.2. Расхождения:** `Credentials` лежит в **состоянии агрегата**, то есть попадает в аnemic-модель и потенциально
в события/снапшоты. Требование §6 (секрет не в событиях) выполняется только дисциплиной: в модели нет отдельного
«секретного» хранилища. Кандидат на фикс — держать креды вне агрегата (ссылка на секрет).

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `GarminGateway.Connect` |
| запись (команда) | `GarminGateway.FetchActivities` |
| запись (команда) | `GarminGateway.Sync` |
| чтение | `GarminGateway.State` |

## 3. State Transitions
```
(нет) → [Connect] → Configured
Configured → [Sync] → Configured (обновляет LastSync)
Configured → [истёк токен / отказ Garmin auth] → RequiresReauth
RequiresReauth → [Connect с новыми credentials] → Configured
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| SingleSourcePerClient | один экземпляр на (клиент, источник) — у каждого клиента свой Garmin-токен |
| TokenTtl | `tokenExpiry` проверяется на каждом Sync/FetchActivities; истёкший токен не используется, авто-refresh нет |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| ReauthOnExpiry | токен истёк или Garmin отклонил credentials | `requiresReauth=true`; Sync/FetchActivities → `400 token_expired`; UI просит новый токен и повторяет Connect |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Connect(credentials)` | Credentials |
| `Sync(config)` | SyncConfig |
| `FetchActivities()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Connect` | `GarminConnected` |
| `Sync` | `GarminSynced` |
| `FetchActivities` | `GarminActivitiesFetched` |

## 8. Throughput
- Пакетная загрузка (тысячи точек за синк), редкие вызовы.

## 9. Size
- Один агрегат на источник. Снапшоты не нужны.
