# Aggregate Design Canvas: DailyMetrics

> Bounded Context: Дневные метрики
> Агрегат: DailyMetrics
> Профиль: Append-only (импорт), Single-writer

## 1. Name (Имя)
- **DailyMetrics** — дневная сводка физиологии + сон.
- **Создаётся когда:** импорт дневных метрик/сна (синк/CSV).
- **Завершается когда:** полная очистка данных.

## 2. Description (Описание)
Дневная сводка. Хранится в IndexedDB `weekly` (стресс/пульс покоя/HRV) и `sleep` (сон).

**Value Objects:**
- **Root**: date (string)
- **Stress**: stress (int)
- **RestingHR**: resting_hr (int)
- **Hrv**: hrv (int)
- **Sleep**: score, durationMin, deepMin, lightMin, remMin, awakeMin

## 3. State Transitions
```
(нет) → [ImportDaily] → Stored
Stored → [ReimportDaily] → Stored (upsert по date)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| DateFormat | date — «YYYY-MM-DD» |
| NonNegative | stress / resting_hr / hrv ≥ 0 |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| AnalyticsRefresh | новые дневные метрики | Аналитика пересчитывает тренды |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| SyncStress | garminSync → importWeekly(items, "stress") | stress | UI/автосинк |
| SyncSleep | garminSync → DB.putSleepBatch | sleep | UI/автосинк |
| SyncHrv | garminSync → importWeekly(items, "hrv") | hrv | UI/автосинк |
| ImportCsv | handleImportAnyFile() | CSV (stress/sleep/resting) | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| StressImported | Stress | SyncStress/ImportCsv |
| SleepImported | Sleep | SyncSleep/ImportCsv |
| HrvImported | Hrv | SyncHrv/ImportCsv |
| RestingHrImported | RestingHR | SyncSomatic(resting)/ImportCsv |

## 8. Throughput
- Частота: пакетная (31 день за синк), редкие вызовы.

## 9. Size
- Объём: дни (не месяцы точек). Снапшоты не нужны.
