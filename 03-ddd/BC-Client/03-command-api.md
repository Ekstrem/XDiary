# Command API: Клиент (Client)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (POST) | Метод агрегата | Событие |
|---|---|---|---|
| Register | `POST /api/v1/clients` | `Register(phone)` | `ClientRegistered` |
| VerifyOtp | `POST /api/v1/clients/{id}/verify` | `VerifyOtp(code)` | `PhoneVerified` → **access + refresh** |
| RefreshToken | `POST /api/v1/clients/refresh` | TokenService (по refresh) | `{ accessToken, refreshToken }` |
| LinkPartner | `POST /api/v1/clients/{id}/link-partner` | `LinkPartner(partnerId)` | `PartnerLinked` |
| UnlinkPartner | `POST /api/v1/clients/{id}/unlink-partner` | `UnlinkPartner()` | `PartnerUnlinked` |

## Request DTO
```json
{ "phone": "+7…", "code": "123456", "partnerId": "uuid" }
```

## Response DTO (verify)
`ClientOperationResponse { aggregateId, version, result, reasons, accessToken, refreshToken }`

- **`accessToken` (client-JWT)** — **15 мин**, HS256, issuer `xdiary`, `sub = clientId`, claim `phone`. Тот же ключ, что и Privacy (`Jwt:SigningKey`).
- **`refreshToken`** — **30 дней** (как Privacy, ADR-008); обновление access — `POST /api/v1/clients/refresh`.
- JWT идентифицирует **пользователя** (клиента), не устройство. PIN — локальный замок (ADR-008), НЕ условие выдачи токена.
