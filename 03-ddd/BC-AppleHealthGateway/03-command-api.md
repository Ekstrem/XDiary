# Command API: Шлюз Apple Health (AppleHealthGateway)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| Configure | `Configure(metrics)` | `AppleImportConfigured` | ImportConfig |
| ImportFile | `ImportFile(file)` | `AppleFileImported` | файл |

## Request DTO
```json
{ "metrics": ["hr","hrv","sleep","resting_hr"] }
```

## Цепочка корреляции имён
```
UI → Configure(metrics) → AppleImportConfigured
UI → ImportFile(export.xml/CSV) → AppleFileImported (норм. соматика → SomaticSeries/DailyMetrics)
```
