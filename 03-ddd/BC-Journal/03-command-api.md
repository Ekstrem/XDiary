# Command API: Журнал интимной активности (Journal)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST** (без PUT/DELETE). ⚠️ = техдолг (AR-003).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| StartManualEntry | `POST /api/v1/journal/records` | `StartManualEntry()` | `ManualEntryStarted` |
| StartFromGarmin | `POST /api/v1/journal/records/from-garmin` | `StartFromGarmin(activity)` | `GarminEntryStarted` |
| SpecifyWhen | `POST /api/v1/journal/records/{id}/when` | `SpecifyWhen(startedAt)` | `WhenSpecified` |
| AddPartner | `POST /api/v1/journal/records/{id}/partners` | `AddPartner(partnerId)` | `PartnerAdded` |
| RemovePartner | `DELETE /api/v1/journal/records/{id}/partners/{partnerId}` ⚠️ | `RemovePartner(partnerId)` | `PartnerRemoved` |
| SpecifyPlace | `POST /api/v1/journal/records/{id}/place` | `SpecifyPlace(place)` | `PlaceSpecified` |
| AddPositions | `POST /api/v1/journal/records/{id}/positions` | `AddPositions(positions)` | `PositionsAdded` |
| AddAccessories | `POST /api/v1/journal/records/{id}/accessories` | `AddAccessories(...)` | `AccessoriesAdded` |
| RecordContraception | `POST /api/v1/journal/records/{id}/contraception` | `RecordContraception(...)` | `ContraceptionRecorded` |
| SpecifyDuration | `POST /api/v1/journal/records/{id}/duration` | `SpecifyDuration(durationMin)` | `DurationSpecified` |
| RecordOrgasms | `POST /api/v1/journal/records/{id}/orgasms` | `RecordOrgasms(orgasms[])` | `OrgasmsRecorded` |
| Complete | `POST /api/v1/journal/records/{id}/complete` | `Complete()` | `RecordCompleted` |
| Delete | `DELETE /api/v1/journal/records/{id}` ⚠️ | `Delete()` | `RecordDeleted` |

## Request DTO (RecordDraft)
```json
{ "startedAt": "YYYY-MM-DDTHH:mm", "partners": ["uuid"], "place": "string", "positions": ["string"],
  "costumes": ["string"], "toys": ["string"], "fantasies": ["string"], "tags": ["string"],
  "contraceptives": ["string"], "contraceptiveCount": 0, "durationMin": 0,
  "orgasms": [{ "participant": "me|partnerId", "count": 0, "types": ["string"], "rating": 0 }],
  "garmin": false, "garminId": "" }
```

> ⚠️ **Техдолг (AR-003):** `RemovePartner` и `Delete` должны быть `POST .../{action}`, не `DELETE`.
