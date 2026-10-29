# Aggregate Design Canvas: GarminSync

> Bounded Context: Шлюз Garmin
> Агрегат: GarminSync
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **GarminSync** — состояние интеграции с Garmin (учётка, токен, окно синка).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием синка с Garmin.

**Value Objects:**
- **Root**: sourceId (string, «garmin»)
- **Credentials**: apiKey, oauthToken, tokenExpiry
- **SyncConfig**: metrics[], windowDays
- **LastSync**: timestamp

## 3. State Transitions
```
(нет) → [ConnectSource] → Configured
Configured → [Sync] → Synced (обновляет LastSync)
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

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| Sync | garminSync() | SyncConfig | UI/автосинк |
| ImportActivities | openGarminPicker() | — | UI («Из Garmin») |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| GarminSomaticsFetched | (наружу: точки/сводки) | Sync |
| GarminActivitiesFetched | (наружу: занятия) | ImportActivities |

## 8. Throughput
- Пакетная загрузка (тысячи точек за синк), редкие вызовы.

## 9. Size
- Один агрегат на источник; снапшоты не нужны.
