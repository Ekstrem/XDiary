# Command API: Партнёры (Partners)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | `operationId` | Метод агрегата | Событие |
|---|---|---|---|
| Register | `Partners.Register` | `Register(name)` | `PartnerRegistered` |
| Rename | `Partners.Rename` | `Rename(name)` | `PartnerRenamed` |
| UpdateProfile | `Partners.UpdateProfile` | `UpdateProfile(photo, gender, notes)` | `PartnerProfileUpdated` |
| Remove | `Partners.Remove` | `Remove()` | `PartnerRemoved` |

## Request: обязательность полей
Поля, их типы и обязательность **не пересказываются здесь** — источник: `api-index.yaml`, операции `Partners.Register`
(`required: name, gender`; `optional: photo, notes`) и `Partners.UpdateProfile` (все опциональны: отсутствие = «не менялось»).
`clientId` в теле **не передаётся** — берётся из `sub` токена.
