# Command API: Партнёры

> Внутренний API (UI → сервис), без HTTP.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| CreatePartner | savePartnerEdit() | PartnerDraft | (нет) |
| UpdatePartner | savePartnerEdit() | PartnerDraft + editingPartnerId | Active |
| DeletePartner | deletePartnerEdit() | editingPartnerId | Active |
| OpenEdit | openPartnerEdit(id) | id? | — |

## Request DTO
### PartnerDraft
```json
{
  "name": "string",
  "photo": "dataURL | null",
  "gender": "male | female",
  "notes": "string"
}
```

## Цепочка корреляции имён
```
UI → openPartnerEdit(id) → DB.getPartner(id) → форма → savePartnerEdit() → DB.putPartner → PartnerCreated/Updated
UI → deletePartnerEdit() → DB.deletePartner(id) → PartnerDeleted
```
