# Materialized Views: Дневные метрики

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `weekly` | дневные метрики (keyPath `date`) | DailySeries |
| `sleep` | сон (keyPath `date`) | DailySeries |
| `sleep_weekly` | недельные агрегаты (keyPath `end`) | тренды (перспектива) |

## Store: weekly
| Поле | Тип | Источник |
|---|---|---|
| date | string PK | calendarDate |
| stress | int | overallStressLevel |
| resting_hr | int | restingHeartRate |
| hrv | int | lastNightAvg |

## Store: sleep
| Поле | Тип | Источник |
|---|---|---|
| date | string PK | calendarDate |
| score | int | sleepScores.overall |
| durationMin / deepMin / lightMin / remMin / awakeMin | int | sleepTimeSeconds и др. |

## Проекционные правила
| Событие | Действие |
|---|---|
| StressImported / RestingHrImported / HrvImported | PUT (merge по date) |
| SleepImported | PUT (upsert по date) |
