# Command API: Клиент (Client)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.
> **AR-016:** пути, коды и тела — только в `04-architecture/api-index.yaml`; здесь — смысл операции и метод агрегата.

## Команды
| Команда | `operationId` | Метод агрегата | Событие |
|---|---|---|---|
| Register | `Client.Register` | `Register(phone)` | `ClientRegistered` |
| VerifyOtp | `Client.Verify` | `VerifyOtp(code)` | `PhoneVerified` → **access + refresh** |
| RefreshToken | `Client.Refresh` | TokenService (по refresh) | `{ accessToken, refreshToken }` |
| **CompleteProfile** | **`Client.UpdateProfile`** | `CompleteProfile(displayName, gender, photo?)` | `ClientProfileCompleted` |
| **UpdateProfile** | **`Client.UpdateProfile`** (идемпотентно) | `UpdateProfile(displayName, gender, photo?)` | `ClientProfileUpdated` |

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
- **Что НЕ должно попадать в `refreshToken` (уточнение 2026-11-04):** PII. Сейчас `CreateToken(..., phone)` кладёт claim `phone` **и в access, и в refresh** (`ClientTokenService.cs:55`), а фронт хранит refresh в `localStorage` (`xdiary.refreshToken`, `api.js:9`) на **30 дней**. Итог: номер телефона лежит в браузере месяц, токен не отзывается (нет серверного состояния) — для дневника пары это лишняя экспозиция при XSS/общем устройстве/выгрузке хранилища. **Правило:** `refreshToken` несёт только `sub` + `kind`; `phone` при обновлении читается из БД (один запрос раз в 15 минут — цена нулевая). `accessToken` (15 мин) может нести `phone`, если он нужен экрану.
- JWT идентифицирует **пользователя** (клиента), не устройство. PIN — локальный замок (ADR-008), НЕ условие выдачи токена.
- **`sub` обязан доходить до сервиса без изменения** (ADR-025): на нём держится весь скоуп по `clientId`.
