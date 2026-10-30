# Command API: Соматика — минутные точки (SomaticSeries)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| RecordPoint | `RecordPoint(t, hr, body_battery)` | `SomaticPointRecorded` | t, HeartRate, BodyBattery |

## Request DTO (SomaticBatch)
```json
{ "somatic": [["YYYY-MM-DDTHH:mm", 72, 55]] }
```

## Цепочка корреляции имён
```
UI/автосинк (из шлюза) → RecordPoint(t, hr, bb) → SomaticPointRecorded
```
