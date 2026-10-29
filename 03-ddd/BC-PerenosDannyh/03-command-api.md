# Command API: Переносимость данных

> Внутренний API (UI → сервис), без HTTP.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| ExportRecords | downloadCsv() | все записи | — |
| ImportRecords | importCsvFile(file) | CSV | — |
| ImportSomatic | handleImportAnyFile(file) | CSV (соматика/сон/стресс) | — |

## Формат (Published Language)
### CSV записей (CSV_HEADERS)
```
id,started_at,duration_min,partner,place,positions,tags,costumes,toys,fantasies,contraceptives,contraceptive_count,orgasms_me,orgasms_partner,orgasm_me_type,orgasm_me_rating,orgasm_partner_type,orgasm_partner_rating,garmin
```

## Цепочка корреляции имён
```
UI → downloadCsv() → recordsToCsv() → blob → файл
UI → importCsvFile() → csvToRecords() → DB.putRecord(по id) → RecordImported
UI → handleImportAnyFile() → распознать тип CSV → DB.put* → …Imported
```
