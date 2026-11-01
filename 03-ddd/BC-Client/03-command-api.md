# Command API: Клиент (Client)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (POST) | Метод агрегата | Событие |
|---|---|---|---|
| Register | `POST /api/v1/clients` | `Register(phone)` | `ClientRegistered` |
| VerifyOtp | `POST /api/v1/clients/{id}/verify` | `VerifyOtp(code)` | `PhoneVerified` |
| LinkPartner | `POST /api/v1/clients/{id}/link-partner` | `LinkPartner(partnerId)` | `PartnerLinked` |
| UnlinkPartner | `POST /api/v1/clients/{id}/unlink-partner` | `UnlinkPartner()` | `PartnerUnlinked` |

## Request DTO
```json
{ "phone": "+7…", "code": "123456", "partnerId": "uuid" }
```

## Response DTO
`ClientOperationResponse { aggregateId, version, result, reasons }`
