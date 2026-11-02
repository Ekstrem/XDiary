# Command API: Приватность и доступ (Privacy)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.
> **Per-client:** `clientId` из JWT (`sub = clientId`) — у каждого клиента свой PIN (ADR-021, AR-008).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| SetPin | `POST /api/v1/privacy/pin/set` | `SetPin(pin)` | `PinSet` |
| ChangePin | `POST /api/v1/privacy/pin/change` | `ChangePin(pin)` | `PinChanged` |
| Remove | `POST /api/v1/privacy/pin/remove` | `Remove()` | `PinRemoved` |
| Lock | `POST /api/v1/privacy/pin/lock` | `Lock()` | `Locked` |
| Unlock | `POST /api/v1/privacy/pin/unlock` | `Unlock()` | `Unlocked` |
| IssueToken | `POST /api/v1/privacy/token` | TokenService (после PIN) | `{ accessToken, refreshToken, expiresIn }` |
| VerifyToken | `POST /api/v1/privacy/verify-token` | TokenService (stateless) | `{ valid }` |

## JWT (выпуск)
- HS256, issuer `xdiary`, access **15 мин**, refresh **30 дней**. Секрет — `Jwt:SigningKey` (не в git). Неверный PIN → **401**.

## Bootstrap входа (AR-008 / BE-036)
- **Первичная идентификация:** телефон + OTP (BC-Client `/verify`) → client-JWT (`sub = clientId`).
- **PIN = локальный замок** (re-auth на Lock/Unlock), НЕ первичная аутентификация: re-auth по PIN = `{clientId (из сохранённого JWT `sub`), pin}` — клиент **не в теле запроса**.
- Чистое устройство (без `clientId`) → **онбординг (телефон + OTP)**, не PIN-first.
