# Command API: Журнал интимной активности

> Внутренний API (UI → сервис), без HTTP — локальная PWA.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| CreateRecord | saveWizard() | RecordDraft | (нет) |
| UpdateRecord | saveWizard() | RecordDraft + editingId | Created |
| DeleteRecord | deleteFromWizard() | editingId | Created |
| OpenDraft | openWizard(id\|null) | id? | — |

## Request DTO

### RecordDraft
```json
{
  "startedAt": "YYYY-MM-DDTHH:mm",
  "durationMin": 0,
  "partner": "string",
  "place": "string",
  "positions": ["string"],
  "tags": ["string"],
  "costumes": ["string"],
  "toys": ["string"],
  "fantasies": ["string"],
  "contraceptives": ["string"],
  "contraceptiveCount": 0,
  "orgasmsMe": 0,
  "orgasmsPartner": 0,
  "orgasmMeType": ["string"],
  "orgasmPartnerType": ["string"],
  "orgasmMeRating": 0,
  "orgasmPartnerRating": 0,
  "garmin": false,
  "garminId": ""
}
```

## Цепочка корреляции имён
```
UI (визард) → draft (RecordDraft) → saveWizard() → DB.putRecord(record) → RecordCreated
UI → openWizard(id) → DB.getRecord(id) → draft → renderWizard()
UI → deleteFromWizard() → DB.deleteRecord(id) → RecordDeleted
CSV → csvToRecords() → DB.putRecord(record) → RecordImported
```
