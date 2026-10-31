# Command API: Партнёры (Partners)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| Register | `POST /api/v1/partners` | `Register(name)` | `PartnerRegistered` |
| Rename | `POST /api/v1/partners/{id}/name` | `Rename(name)` | `PartnerRenamed` |
| UpdateProfile | `POST /api/v1/partners/{id}/profile` | `UpdateProfile(photo, gender, notes)` | `PartnerProfileUpdated` |
| Remove | `POST /api/v1/partners/{id}/remove` | `Remove()` | `PartnerRemoved` |

## Request DTO (PartnerDraft)
```json
{ "name": "string", "photo": "dataURL", "gender": "male|female", "notes": "string" }
```
