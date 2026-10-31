# Command API: Приватность и доступ (Privacy)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**. ⚠️ = техдолг (AR-003).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| SetPin | `POST /api/v1/privacy/pin/set` | `SetPin(pin)` | `PinSet` |
| ChangePin | `POST /api/v1/privacy/pin/change` | `ChangePin(pin)` | `PinChanged` |
| Remove | `DELETE /api/v1/privacy/pin` ⚠️ | `Remove()` | `PinRemoved` |
| Lock | `POST /api/v1/privacy/pin/lock` | `Lock()` | `Locked` |
| Unlock | `POST /api/v1/privacy/pin/unlock` | `Unlock()` | `Unlocked` |
| IssueToken | `POST /api/v1/privacy/token` | TokenService (после PIN) | `{ accessToken, refreshToken, expiresIn }` |
| VerifyToken | `POST /api/v1/privacy/verify-token` | TokenService (stateless) | `{ valid }` |

> ⚠️ **Техдолг (AR-003):** `Remove` (DELETE) должен быть `POST .../pin/remove`.

## JWT (выпуск)
- HS256, issuer `xdiary`, access **15 мин**, refresh **30 дней**. Секрет — `Jwt:SigningKey` (не в git). Неверный PIN → **401**.
