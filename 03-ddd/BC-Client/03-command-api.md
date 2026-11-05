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

## Канал доставки OTP (MVP-упрощение, @TL 2026-11-06 01:00)

Домену всё равно, **как** код доехал до человека — важен факт «код доставлен». Поэтому `SmsOtpSender` — это **порт**,
а реализаций две, и выбор идёт **конфигурацией**, а не веткой в коде:

| Реализация | Когда | Как включается |
|---|---|---|
| **`TelegramOtpSender`** | **MVP** — доставка через Telegram-бот (второй пользователь уже в Telegram) | `Otp:Channel=telegram` |
| **`SmsOtpSender`** | **целевая** — SMS-провайдер (нужен аккаунт/деньги, поэтому позже) | `Otp:Channel=sms` |
| `log` (код в лог) | **только dev/CI** — никогда на проде | `Otp:Channel=log` |

**Правило:** `Otp:Channel` — единственное место выбора; в домене и в агрегате канала **нет**. Неизвестное значение ⇒
**падение на старте**, а не тихий откат в `log` (иначе прод может «успешно отправить» в лог и никто не заметит).

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
- **Что НЕ должно попадать в `refreshToken`:** PII. **Правило:** `refreshToken` несёт только `sub` + `kind`; `phone` при обновлении читается из БД (один запрос раз в 15 минут — цена нулевая). `accessToken` (15 мин) может нести `phone`, если он нужен экрану. Фронт хранит refresh в `localStorage` (`xdiary.refreshToken`, `api.js:9`) на **30 дней**, токен не отзывается (нет серверного состояния) — для дневника пары это лишняя экспозиция при XSS/общем устройстве/выгрузке хранилища.
  **Состояние кода (2026-11-05, `AR-018`):** приведён к правилу — `ClientTokenService.Issue` создаёт refresh через `CreateRefresh(sub, now)` (`ClientTokenService.cs:23`, `:53`), у которого параметра `phone` **нет вовсе** ⇒ PII в refresh **невозможен по сигнатуре**, а не по договорённости; `TryValidateRefresh` 2-аргументный и `phone` из токена **не читает** (`:27`, `:45`); тест `RefreshToken_HasNoPhone` — `6-msa/BC-Client/src/Client.IntegrationTests/ClientTokenServiceTests.cs:40`; политика одна и проверяема: `XDiary.Contracts/TokenPolicy.cs` (15/30/5) + тест `TokenPolicy_CanonicalValues_MatchADR`.
- JWT идентифицирует **пользователя** (клиента), не устройство. PIN — локальный замок (ADR-008), НЕ условие выдачи токена.
- **`sub` обязан доходить до сервиса без изменения** (ADR-025): на нём держится весь скоуп по `clientId`.
