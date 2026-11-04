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

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`IDailyMetricsAnemicModel` (`Root`, `Stress`, `RestingHr`, `Hrv`, `Sleep`) + `DailyMetricsRoot` (`Date`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.Date` | string (`yyyy-MM-dd`) | 1 | **да** | `Recompute(date)` | одна запись на дату; повторный пересчёт **перезаписывает**, а не добавляет | US-006, ADR-004 (MV) |
| `Stress` | int | 1 | да | `Recompute` | 0..100; «нет данных» ≠ `0` (иначе график покажет ложный ноль) | US-006 |
| `RestingHr` | int | 0..1 | нет | `Recompute` | 30..120; отсутствует, если источник не дал | US-006 |
| `Hrv` | int | 0..1 | нет | `Recompute` | > 0; отсутствует, если источник не дал | US-006 |
| `Sleep` | `ISleep` (VO) | 0..1 | нет | `Recompute` | длительность ≥ 0, фазы не пересекаются | US-006 |

**2.2.2. Расхождения:** значения без данных (`null`) и нули не различаются на уровне типа (`int` для `Stress`), поэтому
«нет данных» и «стресс 0» в MV неотличимы — это же увидит фронт. Нужен nullable или флаг `HasData` (`@BE`).

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `DailyMetrics.Hrv` |
| запись (команда) | `DailyMetrics.RestingHr` |
| запись (команда) | `DailyMetrics.Sleep` |
| запись (команда) | `DailyMetrics.Stress` |
| чтение | `DailyMetrics.Series` |

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
