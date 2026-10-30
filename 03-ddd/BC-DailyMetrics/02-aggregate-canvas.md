# Aggregate Design Canvas: DailyMetrics

> Bounded Context: Дневные метрики (DailyMetrics)
> Агрегат: DailyMetrics
> Профиль: Append-only (импорт), Single-writer

## 1. Name (Имя)
- **DailyMetrics** — дневная сводка физиологии + сон.
- **Создаётся когда:** импорт дневных метрик/сна (синк/CSV).
- **Завершается когда:** полная очистка данных.

## 2. Description (Описание)
Дневная сводка. Хранится в IndexedDB `weekly` (стресс/пульс покоя/HRV) и `sleep` (сон).

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **DailyMetrics** — совокупность признаков, идентифицирующих сводку:
  - `date` (string) — день «YYYY-MM-DD»

**Объект-значения (Value Objects):**
- **Stress**: stress (int)
- **RestingHR**: resting_hr (int)
- **Hrv**: hrv (int)
- **Sleep**: score, durationMin, deepMin, lightMin, remMin, awakeMin

## 3. State Transitions
```
(нет) → [RecordX] → Stored
Stored → [RecordX] → Stored (upsert по date)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| DateFormat | date — «YYYY-MM-DD» |
| NonNegative | stress / resting_hr / hrv ≥ 0 |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| AnalyticsRefresh | любое событие метрики | Аналитика пересчитывает тренды |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `RecordStress(date, stress)` | Stress |
| `RecordRestingHr(date, resting_hr)` | RestingHR |
| `RecordHrv(date, hrv)` | Hrv |
| `RecordSleep(date, sleep)` | Sleep |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `RecordStress` | `StressRecorded` |
| `RecordRestingHr` | `RestingHrRecorded` |
| `RecordHrv` | `HrvRecorded` |
| `RecordSleep` | `SleepRecorded` |

## 8. Throughput
- Пакетная (31 день за синк), редкие вызовы.

## 9. Size
- Объём: дни. Снапшоты не нужны.
