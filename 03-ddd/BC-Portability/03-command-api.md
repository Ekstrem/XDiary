# Command API: Переносимость данных (Portability)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| ExportRecords | `POST /api/v1/portability/export` | `ExportRecords(records)` | `RecordsExported` |
| ImportRecords | `POST /api/v1/portability/import/records` | `ImportRecords(csv)` | `RecordsImported` |
| ImportSomatic | `POST /api/v1/portability/import/somatic` | `ImportSomatic(csv)` | `SomaticImported` |

## Формат (Published Language — CSV_HEADERS)
```
id,started_at,duration_min,partner,place,positions,tags,costumes,toys,fantasies,contraceptives,contraceptive_count,orgasms_me,orgasms_partner,orgasm_me_type,orgasm_me_rating,orgasm_partner_type,orgasm_partner_rating,garmin
```
