# Command API: Журнал интимной активности (Journal)

> Внутренний API (UI → сервис). В MSA — REST-команды на `journal` (см. ADR-006, ADR-001).
> Именованные команды повторяют экраны визарда (CJM); Id/Version — от фреймворка EDM.

## Команды (по экранам визарда)
| Команда | Метод агрегата | Входные VO | Состояние до |
|---|---|---|---|
| StartManualEntry | `StartManualEntry()` | — | (нет) |
| StartFromGarmin | `StartFromGarmin(activity)` | GarminActivity | (нет) |
| SpecifyWhen | `SpecifyWhen(startedAt)` | When | Draft |
| ChoosePartner | `ChoosePartner(partner)` | PartnerRef | Draft |
| SpecifyPlace | `SpecifyPlace(place)` | Place | Draft |
| AddPositions | `AddPositions(positions)` | Positions | Draft |
| AddAccessories | `AddAccessories(costumes, toys, fantasies, tags)` | Accessories | Draft |
| RecordContraception | `RecordContraception(contraceptives, count)` | Contraception | Draft |
| SpecifyDuration | `SpecifyDuration(durationMin)` | Duration | Draft |
| RecordOrgasms | `RecordOrgasms(me, partner, meTypes, partnerTypes, meRating, partnerRating)` | Orgasms | Draft |
| Complete | `Complete()` | — | Draft |
| Delete | `Delete()` | — | Recorded |

## Request DTO (RecordDraft)
```json
{
  "startedAt": "YYYY-MM-DDTHH:mm",
  "durationMin": 0,
  "partner": "string",
  "place": "string",
  "positions": ["string"],
  "costumes": ["string"],
  "toys": ["string"],
  "fantasies": ["string"],
  "tags": ["string"],
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

## Фабрики (два источника записи)
| Источник | Фабрика | Что предзаполняется |
|---|---|---|
| Ручная запись (визард) | `StartManualEntry()` | ничего (пустой Draft) |
| Импорт занятия Garmin | `StartFromGarmin(activity)` | startedAt, durationMin, garminId, garmin=true |

## Цепочка корреляции имён
```
UI «Новая запись» → StartManualEntry → Draft → (SpecifyWhen → ChoosePartner → … → RecordOrgasms) → Complete → RecordRecorded
UI «Из Garmin» → StartFromGarmin(activity) → Draft → Complete → RecordRecorded
UI → Delete → RecordDeleted
```
