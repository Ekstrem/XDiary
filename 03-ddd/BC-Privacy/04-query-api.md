# Query API: Приватность и доступ (Privacy)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Есть ли PIN | `GET /api/v1/privacy/pin/has` | bool |
| Проверка PIN | `POST /api/v1/privacy/pin/verify` | bool |

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

> **Исправлено 2026-11-10 (@AR):** строки выше указывали `GET /api/v1/privacy/has-pin` и `POST /api/v1/privacy/verify` —
> таких маршрутов **нет**. По коду `PrivacyController.cs:48` — `[HttpGet("pin/has")]`, `:52` — `[HttpPost("pin/verify")]`;
> канон `api-index.yaml` (`Privacy.HasPin`, `Privacy.VerifyPin`) с кодом совпадает. То есть устарела была **эта страница**, а не канон.
> Проверка: `git grep -n 'Http.*\("pin/' 6-msa/BC-Privacy/src/Privacy.Api/Controllers/PrivacyController.cs`.
