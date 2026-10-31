# Command API: Шлюз Apple Health (AppleHealthGateway)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| Configure | `POST /api/v1/apple-health/configure` | `Configure(metrics)` | `AppleImportConfigured` |
| ImportFile | `POST /api/v1/apple-health/import` | `ImportFile(file)` | `AppleFileImported` |
