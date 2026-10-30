# Query API: Приватность и доступ (Privacy)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Есть ли PIN | `GET /api/v1/privacy/has-pin` | bool |
| Проверка PIN | `POST /api/v1/privacy/verify` | bool |

## JWT (проверка)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Проверка токена | `POST /api/v1/privacy/verify-token` | `{ valid: bool }` |

- Stateless: проверка подписи/срока (HS256, issuer `xdiary`), без обращения к БД.
- Потребитель — шлюз (YARP) перед проксированием.

## Response DTO
- hasPin: `true | false`.
- verify: `true | false` (если PIN не установлен — true).

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Все контексты | hasPin/verify | gatekeeper перед доступом |
