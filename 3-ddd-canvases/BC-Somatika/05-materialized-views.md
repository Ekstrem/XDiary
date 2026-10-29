# Materialized Views: Соматика

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `somatic` | минутные точки (keyPath `t`) | SomaticSlice |
| `weekly` | дневные метрики (keyPath `date`) | DailySeries |
| `sleep` | сон (keyPath `date`) | DailySeries |
| `sleep_weekly` | недельные агрегаты (keyPath `end`) | тренды (перспектива) |

## Store: somatic
| Поле | Тип | Источник |
|---|---|---|
| t | string PK | minute_of(ms) |
| hr | int | heartRateValues |
| body_battery | int | bodyBatteryValuesArray |

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
| SomaticImported | PUT (upsert по t) |
| StressImported / RestingImported / HrvImported | PUT (merge по date) |
| SleepImported | PUT (upsert по date) |
