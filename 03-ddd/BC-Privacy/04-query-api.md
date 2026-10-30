# Query API: Приватность и доступ (Privacy)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Есть ли PIN | `GET /api/v1/privacy/has-pin` | bool |
| Проверка PIN | `POST /api/v1/privacy/verify` | bool |

## Response DTO
- hasPin: `true | false`.
- verify: `true | false` (если PIN не установлен — true).

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Все контексты | hasPin/verify | gatekeeper перед доступом |
