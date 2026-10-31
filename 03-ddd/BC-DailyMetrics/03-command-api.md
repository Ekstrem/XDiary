# Command API: Дневные метрики (DailyMetrics)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| RecordStress | `POST /api/v1/daily-metrics/stress` | `RecordStress(date, stress)` | `StressRecorded` |
| RecordRestingHr | `POST /api/v1/daily-metrics/resting-hr` | `RecordRestingHr(date, resting_hr)` | `RestingHrRecorded` |
| RecordHrv | `POST /api/v1/daily-metrics/hrv` | `RecordHrv(date, hrv)` | `HrvRecorded` |
| RecordSleep | `POST /api/v1/daily-metrics/sleep` | `RecordSleep(date, sleep)` | `SleepRecorded` |
