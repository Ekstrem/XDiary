# Materialized Views: Переносимость данных (Portability)

## Проекция
Постоянных хранилищ нет — контекст использует CSV как Published Language.

| Артефакт | Тип | Источник |
|---|---|---|
| CSV записей | файл | `records` |
| CSV соматики/сна | файл | `somatic`, `sleep`, `weekly` |

## Правила (событие → действие)
| Событие | Действие |
|---|---|
| `RecordsExported` | сформировать CSV-файл |
| `RecordsImported` | upsert в `records` (по id) |
| `SomaticImported` | запись в `somatic`/`sleep`/`weekly` |
