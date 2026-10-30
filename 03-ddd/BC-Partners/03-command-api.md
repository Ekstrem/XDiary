# Command API: Партнёры (Partners)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| Register | `Register(name)` | `PartnerRegistered` | name |
| Rename | `Rename(name)` | `PartnerRenamed` | name |
| UpdateProfile | `UpdateProfile(photo, gender, notes)` | `PartnerProfileUpdated` | Photo, Gender, Notes |
| Remove | `Remove()` | `PartnerRemoved` | — |

## Request DTO (PartnerDraft)
```json
{ "name": "string", "photo": "dataURL", "gender": "male|female", "notes": "string" }
```

## Цепочка корреляции имён
```
UI → Register(name) → PartnerRegistered
UI → Rename(name) → PartnerRenamed
UI → UpdateProfile(...) → PartnerProfileUpdated
UI → Remove() → PartnerRemoved
```
