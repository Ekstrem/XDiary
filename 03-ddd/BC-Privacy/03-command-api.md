# Command API: Приватность и доступ (Privacy)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| SetPin | `SetPin(pin)` | `PinSet` | Salt, Hash, Iterations |
| ChangePin | `ChangePin(pin)` | `PinChanged` | Salt, Hash, Iterations |
| Remove | `Remove()` | `PinRemoved` | — |
| Lock | `Lock()` | `Locked` | — |
| Unlock | `Unlock()` | `Unlocked` | — |

## Request DTO
```json
{ "pin": "string (4–8 цифр)" }
```

## JWT (выпуск)
Выпуск токена — не метод агрегата, а **TokenService** (после успешной проверки PIN):

| Действие | Вход | Выход |
|---|---|---|
| `IssueToken` (после `VerifyPin`) | PIN (4–8 цифр) | `{ accessToken, refreshToken, expiresIn }` |

- Эндпоинт: `POST /api/v1/privacy/token`.
- HS256, issuer `xdiary`, access **15 мин**, refresh **30 дней**.
- Секрет — `Jwt:SigningKey` (не в git).
- Неверный PIN → **401**; нет PIN → **401**.

```
UI → SetPin(pin) → PinSet
UI → ChangePin(pin) → PinChanged
UI → Remove() → PinRemoved
UI → Lock() → Locked → Unlock() → Unlocked
```
