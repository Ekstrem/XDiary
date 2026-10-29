# 3. Command API

Канонический артефакт: [`03-ddd/BC-Journal/03-command-api.md`](../../../03-ddd/BC-Journal/03-command-api.md)

| Команда | HTTP | Request | Response |
|---|---|---|---|
| CreateRecord | `POST /api/v1/journal` | RecordDraft | JournalOperationResult |
| UpdateRecord | `PUT /api/v1/journal/{id}` | RecordDraft | JournalOperationResult |
| DeleteRecord | `DELETE /api/v1/journal/{id}` | — | JournalOperationResult |
| ImportRecord | `POST /api/v1/journal/import` | RecordDraft + id | JournalOperationResult |

## Request DTO (RecordDraft)

```json
{
  "startedAt": "YYYY-MM-DDTHH:mm",
  "durationMin": 30,
  "partner": "string",
  "place": "string",
  "positions": ["string"],
  "tags": ["string"],
  "costumes": ["string"],
  "toys": ["string"],
  "fantasies": ["string"],
  "contraceptives": ["string"],
  "contraceptiveCount": 2,
  "orgasmsMe": 1,
  "orgasmsPartner": 2,
  "orgasmMeType": ["string"],
  "orgasmPartnerType": ["string"],
  "orgasmMeRating": 4,
  "orgasmPartnerRating": 5,
  "garmin": false,
  "garminId": ""
}
```

## Цепочка

`Request → MediatR Command → Aggregate.CreateRecord/UpdateRecord/… → RecordCreated/… → EventBus → проекция/Command Store`
