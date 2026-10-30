# Materialized Views: Дневные метрики (DailyMetrics)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `weekly` | ReadModel (keyPath `date`) | DailySeries |
| `sleep` | ReadModel (keyPath `date`) | DailySeries |

## Store: weekly
| Поле | Тип | Источник |
|---|---|---|
| date | string PK | сущность (день) |
| stress | int | VO |
| resting_hr | int | VO |
| hrv | int | VO |

## Store: sleep
| Поле | Тип | Источник |
|---|---|---|
| date | string PK | сущность (день) |
| score | int | VO |
| durationMin / deepMin / lightMin / remMin / awakeMin | int | VO |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `StressRecorded` | PUT (merge по date) |
| `RestingHrRecorded` | PUT (merge по date) |
| `HrvRecorded` | PUT (merge по date) |
| `SleepRecorded` | PUT (upsert по date) |

## Индексы
- `date` — диапазон трендов.
