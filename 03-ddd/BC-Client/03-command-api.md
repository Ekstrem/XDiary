# Command API: Клиент (Client)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (POST) | Метод агрегата | Событие |
|---|---|---|---|
| Register | `POST /api/v1/clients` | `Register(phone)` | `ClientRegistered` |
| VerifyOtp | `POST /api/v1/clients/{id}/verify` | `VerifyOtp(code)` | `PhoneVerified` → **access + refresh** |
| RefreshToken | `POST /api/v1/clients/refresh` | TokenService (по refresh) | `{ accessToken, refreshToken }` |
| **CompleteProfile** | **`POST /api/v1/clients/me/profile`** | `CompleteProfile(displayName, gender, photo?)` | `ClientProfileCompleted` |
| **UpdateProfile** | **`POST /api/v1/clients/me/profile`** (идемпотентно) | `UpdateProfile(displayName, gender, photo?)` | `ClientProfileUpdated` |

> **Один эндпоинт для онбординга и редактирования** (`me/profile`): первая запись — `CompleteProfile`
> (профиль пуст), повторная — `UpdateProfile`. Разделение состояний — в агрегате, не в маршрутах.

## Удалено / выведено (ADR-024, 2026-11-09)
| Было | Стало |
|---|---|
| `POST /api/v1/clients/{id}/link-partner` | **deprecated no-op** (`200 {deprecated:true}` + `Deprecation: true`) на время перехода фронта; затем **удаляется** |
| `POST /api/v1/clients/{id}/unlink-partner` | то же |
| VO `LinkedPartnerId` («партнёр-«я»») | **удалён** — «я» не партнёр; в акте `participant="me"` |

> **`me` в пути, а не `{id}`** — клиент управляет **только своим** профилем, идентификатор берётся
> из JWT `sub`, а не из URL. Это убирает класс IDOR целиком, а не «проверяем владельца в контроллере».

## Request DTO
```json
{ "phone": "+7…", "code": "123456" }
```
```json
{ "displayName": "…", "gender": "male", "photoUrl": null }
```
- **`gender` — словарь `male` | `female`** (решение владельца 2026-11-03 16:54: «обойдёмся двумя»).
  Неизвестное значение → **400**; свободная строка не принимается.
- `displayName` — обязателен для онбординга; `photoUrl` — опционален.
- **`partnerId` из запроса убран** — привязка «я к партнёру» больше не существует (ADR-024).

## Response DTO (verify)
`ClientOperationResponse { aggregateId, version, result, reasons, accessToken, refreshToken }`

- **`accessToken` (client-JWT)** — **15 мин**, HS256, issuer `xdiary`, `sub = clientId`, claim `phone`. Тот же ключ, что и Privacy (`Jwt:SigningKey`).
- **`refreshToken`** — **30 дней** (как Privacy, ADR-008); обновление access — `POST /api/v1/clients/refresh`.
- JWT идентифицирует **пользователя** (клиента), не устройство. PIN — локальный замок (ADR-008), НЕ условие выдачи токена.
- **`sub` обязан доходить до сервиса без изменения** (ADR-025): на нём держится весь скоуп по `clientId`.
