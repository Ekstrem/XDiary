# Aggregate Design Canvas: GarminSync

> Bounded Context: Шлюз Garmin (GarminGateway)
> Агрегат: GarminSync
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **GarminSync** — состояние интеграции с Garmin (учётка, токен, окно синка).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием синка с Garmin.

> **Функциональное ограничение (backlog G-01/G-06):** PWA не вызывает Garmin API напрямую (нет CORS/OAuth из браузера). Данные — **локальный CSV-экспорт** из Garmin Connect ИЛИ **серверный прокси** (GarminGateway = ACL). `Connect` = выбор источника + импорт CSV/прокси, **не OAuth-UI**.

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

## 3. State Transitions
```
(нет) → [Connect] → Configured
Configured → [Sync] → Configured (обновляет LastSync)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| SingleSourcePerClient | один экземпляр на (клиент, источник) — у каждого клиента свой Garmin-токен |
| TokenTtl | токен кэшируется ≤ 23 ч |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| ReauthOnExpiry | токен истёк | перевыпустить OAuth-токен |

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
