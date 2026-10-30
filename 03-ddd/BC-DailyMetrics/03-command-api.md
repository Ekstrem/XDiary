# Command API: Дневные метрики (DailyMetrics)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| RecordStress | `RecordStress(date, stress)` | `StressRecorded` | Stress |
| RecordRestingHr | `RecordRestingHr(date, resting_hr)` | `RestingHrRecorded` | RestingHR |
| RecordHrv | `RecordHrv(date, hrv)` | `HrvRecorded` | Hrv |
| RecordSleep | `RecordSleep(date, sleep)` | `SleepRecorded` | Sleep |

## Request DTO
```json
{ "date": "YYYY-MM-DD", "stress": 0, "resting_hr": 0, "hrv": 0, "sleep": { "score": 0, "durationMin": 0, "deepMin": 0, "lightMin": 0, "remMin": 0, "awakeMin": 0 } }
```

## Цепочка корреляции имён
```
UI/автосинк (из шлюза) → RecordStress / RecordRestingHr / RecordHrv / RecordSleep → StressRecorded / …
```
