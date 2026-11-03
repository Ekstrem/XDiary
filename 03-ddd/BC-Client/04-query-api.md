# Query API: Клиент (Client)

> Read-сторона (CEQRS): читает только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Текущий пользователь | `GET /api/v1/clients/me` | ClientView |
| Деталь | `GET /api/v1/clients/{id}` | ClientView |

## Response DTO
### ClientView
- `id`, `phone`, `verified` (bool), **`displayName`** (string), **`gender`** (`male | female`), `photo` (опционально).
- **`linkedPartnerId` удалён** (ADR-024): «я» — не партнёр; в акте «я» = `participant="me"`.
- PIN в `ClientView` **не отдаётся** (политика приватности того же контекста, отдельный контракт `/privacy/pin/*`).
