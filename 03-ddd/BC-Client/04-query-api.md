# Query API: Клиент (Client)

> Read-сторона (CEQRS): читает только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Текущий пользователь | `GET /api/v1/clients/me` | ClientView |
| Деталь | `GET /api/v1/clients/{id}` | ClientView |

## Response DTO
### ClientView
- `id`, `phone`, `verified` (bool), `linkedPartnerId` (`uuid | null`).
