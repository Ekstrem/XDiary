# Command API: Переносимость данных (Portability)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| ExportRecords | `ExportRecords(records)` | `RecordsExported` | Row[] |
| ImportRecords | `ImportRecords(csv)` | `RecordsImported` | Row[] |
| ImportSomatic | `ImportSomatic(csv)` | `SomaticImported` | Row[] |

## Формат (Published Language — CSV_HEADERS)
```
id,started_at,duration_min,partner,place,positions,tags,costumes,toys,fantasies,contraceptives,contraceptive_count,orgasms_me,orgasms_partner,orgasm_me_type,orgasm_me_rating,orgasm_partner_type,orgasm_partner_rating,garmin
```

## Цепочка корреляции имён
```
UI → ExportRecords(records) → RecordsExported (файл CSV)
UI → ImportRecords(csv) → RecordsImported (upsert по id)
UI → ImportSomatic(csv) → SomaticImported
```
