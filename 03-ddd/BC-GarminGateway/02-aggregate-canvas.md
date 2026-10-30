# Aggregate Design Canvas: GarminSync

> Bounded Context: Шлюз Garmin (GarminGateway)
> Агрегат: GarminSync
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **GarminSync** — состояние интеграции с Garmin (учётка, токен, окно синка).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием синка с Garmin.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **GarminSync** — совокупность признаков, идентифицирующих источник:
  - `sourceId` (string) — «garmin»

**Объект-значения (Value Objects):**
- **Credentials**: apiKey, oauthToken, tokenExpiry
- **SyncConfig**: metrics[], windowDays
- **LastSync**: timestamp

## 3. State Transitions
```
(нет) → [Connect] → Configured
Configured → [Sync] → Configured (обновляет LastSync)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| SingleSource | один экземпляр на источник |
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
