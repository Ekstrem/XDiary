# Command API: Соматика — минутные точки (SomaticSeries)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| RecordPoint | `POST /api/v1/somatic-series/points` | `RecordPoint(t, hr, body_battery)` | `SomaticPointRecorded` |

## Request DTO (SomaticBatch)
```json
{ "somatic": [["YYYY-MM-DDTHH:mm", 72, 55]] }
```
