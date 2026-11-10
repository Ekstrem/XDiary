# Command API: Партнёры (Partners)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | `operationId` | Метод агрегата | Событие |
|---|---|---|---|
| Register | `Partners.Register` | `Register(name)` | `PartnerRegistered` |
| Rename | `Partners.Rename` | `Rename(name)` | `PartnerRenamed` |
| UpdateProfile | `Partners.UpdateProfile` | `UpdateProfile(photo, gender, notes)` | `PartnerProfileUpdated` |
| Remove | `Partners.Remove` | `Remove()` | `PartnerRemoved` |
| LinkClient | `Partners.LinkClient` | `LinkClient(clientId)` — привязка партнёра к клиенту | — (события нет) |
| UnlinkClient | `Partners.UnlinkClient` | `UnlinkClient()` — отвязка | — |


## Request: обязательность полей
Поля, их типы и обязательность **не пересказываются здесь** — источник: `api-index.yaml`, операции `Partners.Register`
(`required: name, gender`; `optional: photo, notes`) и `Partners.UpdateProfile` (все опциональны: отсутствие = «не менялось»).
`clientId` в теле **не передаётся** — берётся из `sub` токена.

**Дополнено 2026-11-10 (@AR):** `LinkClient`/`UnlinkClient` здесь **отсутствовали**, хотя есть и в коде
(`PartnersController.cs:71` `[HttpPost("{id:guid}/link-client")]`, `:83` `[HttpPost("{id:guid}/unlink-client")]`),
и в каноне (`Partners.LinkClient`, `Partners.UnlinkClient`).
**Известный дефект (записан в каноне, `codes`):** оба отдают `200` **и на чужой `id`** — ownership не проверяется
(`codes` содержит пометку «ok (в т.ч. чужой — дефект)», а `404` заявлен для «чужой/несуществующий id», то есть канон уже
описывает расхождение). Пока не исправлено в коде — это долг, а не норма; проверка принадлежит @AR/@TL.
