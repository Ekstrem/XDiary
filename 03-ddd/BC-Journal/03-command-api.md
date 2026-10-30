# Command API: Журнал интимной активности (Journal)

> Внутренний API (UI → сервис). В MSA — REST-команды на `journal` (ADR-006, ADR-001).
> Именованные команды повторяют экраны визарда (CJM); Id/Version — составной ключ `<guid, long>` фреймворка EDM (DF).

## Команды (по экранам визарда) → события
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| StartManualEntry | `StartManualEntry()` | `ManualEntryStarted` | — |
| StartFromGarmin | `StartFromGarmin(activity)` | `GarminEntryStarted` | GarminActivity |
| SpecifyWhen | `SpecifyWhen(startedAt)` | `WhenSpecified` | When |
| AddPartner | `AddPartner(partnerId)` | `PartnerAdded` | partnerId |
| RemovePartner | `RemovePartner(partnerId)` | `PartnerRemoved` | partnerId |
| SpecifyPlace | `SpecifyPlace(place)` | `PlaceSpecified` | Place |
| AddPositions | `AddPositions(positions)` | `PositionsAdded` | Positions |
| AddAccessories | `AddAccessories(costumes, toys, fantasies, tags)` | `AccessoriesAdded` | Accessories |
| RecordContraception | `RecordContraception(contraceptives, count)` | `ContraceptionRecorded` | Contraception |
| SpecifyDuration | `SpecifyDuration(durationMin)` | `DurationSpecified` | Duration |
| RecordOrgasms | `RecordOrgasms(orgasms[])` | `OrgasmsRecorded` | Orgasms |
| Complete | `Complete()` | `RecordCompleted` | — |
| Delete | `Delete()` | `RecordDeleted` | — |

## Request DTO (RecordDraft)
```json
{
  "startedAt": "YYYY-MM-DDTHH:mm",
  "durationMin": 0,
  "partners": ["uuid"],
  "place": "string",
  "positions": ["string"],
  "costumes": ["string"],
  "toys": ["string"],
  "fantasies": ["string"],
  "tags": ["string"],
  "contraceptives": ["string"],
  "contraceptiveCount": 0,
  "orgasms": [{ "participant": "me|partnerId", "count": 0, "types": ["string"], "rating": 0 }],
  "garmin": false,
  "garminId": ""
}
```

## Фабрики (два источника записи)
| Источник | Фабрика | Что предзаполняется |
|---|---|---|
| Ручная запись (визард) | `StartManualEntry()` | ничего (пустой Draft) |
| Импорт занятия Garmin | `StartFromGarmin(activity)` | startedAt, durationMin, garminId, garmin=true |

## Цепочка корреляции имён
```
UI «Новая запись» → StartManualEntry → ManualEntryStarted → Draft
  → SpecifyWhen → WhenSpecified → … → RecordOrgasms → OrgasmsRecorded
  → Complete → RecordCompleted
UI «Из Garmin» → StartFromGarmin(activity) → GarminEntryStarted → Draft → Complete → RecordCompleted
UI → Delete → RecordDeleted
```
