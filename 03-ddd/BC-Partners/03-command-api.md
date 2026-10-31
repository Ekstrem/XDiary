# Command API: Партнёры (Partners)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**. ⚠️ = техдолг (AR-003).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| Register | `POST /api/v1/partners` | `Register(name)` | `PartnerRegistered` |
| Rename | `PUT /api/v1/partners/{id}/name` ⚠️ | `Rename(name)` | `PartnerRenamed` |
| UpdateProfile | `PUT /api/v1/partners/{id}/profile` ⚠️ | `UpdateProfile(photo, gender, notes)` | `PartnerProfileUpdated` |
| Remove | `DELETE /api/v1/partners/{id}` ⚠️ | `Remove()` | `PartnerRemoved` |

## Request DTO (PartnerDraft)
```json
{ "name": "string", "photo": "dataURL", "gender": "male|female", "notes": "string" }
```

> ⚠️ **Техдолг (AR-003):** `Rename`/`UpdateProfile` (PUT) и `Remove` (DELETE) должны быть `POST .../{action}`.
