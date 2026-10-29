# Command API: Шлюз Apple Health

> Внутренний API — ACL к Apple Health (ручной экспорт).

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| ImportFile | handleImportAnyFile() | `export.xml`/CSV | Imported |

## Request DTO
### AppleExport (файл)
```text
export.xml / CSV: пульс, HRV, сон, пульс покоя
```

## Цепочка корреляции имён
```
UI → handleImportAnyFile() → parse export.xml/CSV → нормализованные DTO → SomaticSeries/DailyMetrics
```
