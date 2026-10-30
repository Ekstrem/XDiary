# Query API: Шлюз Apple Health (AppleHealthGateway)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Статус импорта | `GET /api/v1/apple-health/status` | ImportStatus |

## Response DTO
- ImportStatus: lastImport, processedFiles.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | ImportStatus | показать состояние импорта |
