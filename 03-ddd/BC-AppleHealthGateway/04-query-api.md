# Query API: Шлюз Apple Health

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Статус импорта | — | ImportStatus | агрегат AppleImport |

## Response DTO
- ImportStatus: lastImport, processedFiles.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | ImportStatus | показать состояние импорта |
