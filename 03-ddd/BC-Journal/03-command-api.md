# Command API: Журнал интимной активности (Journal)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST** (без PUT/DELETE).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| StartManualEntry | `POST /api/v1/journal/records` | `StartManualEntry()` | `ManualEntryStarted` |
| StartFromGarmin | `POST /api/v1/journal/records/from-garmin` | `StartFromGarmin(activity)` | `GarminEntryStarted` |
| SpecifyWhen | `POST /api/v1/journal/records/{id}/when` | `SpecifyWhen(startedAt)` | `WhenSpecified` |
| AddPartner | `POST /api/v1/journal/records/{id}/partners` | `AddPartner(partnerId)` | `PartnerAdded` |
| RemovePartner | `POST /api/v1/journal/records/{id}/partners/{partnerId}/remove` | `RemovePartner(partnerId)` | `PartnerRemoved` |
| SpecifyPlace | `POST /api/v1/journal/records/{id}/place` | `SpecifyPlace(place)` | `PlaceSpecified` |
| AddPositions | `POST /api/v1/journal/records/{id}/positions` | `AddPositions(positions)` | `PositionsAdded` |
| AddAccessories | `POST /api/v1/journal/records/{id}/accessories` | `AddAccessories(...)` | `AccessoriesAdded` |
| RecordContraception | `POST /api/v1/journal/records/{id}/contraception` | `RecordContraception(...)` | `ContraceptionRecorded` |
| SpecifyDuration | `POST /api/v1/journal/records/{id}/duration` | `SpecifyDuration(durationMin)` | `DurationSpecified` |
| RecordOrgasms | `POST /api/v1/journal/records/{id}/orgasms` | `RecordOrgasms(orgasms[])` | `OrgasmsRecorded` |
| Complete | `POST /api/v1/journal/records/{id}/complete` | `Complete()` | `RecordCompleted` |
| Delete | `POST /api/v1/journal/records/{id}/delete` | `Delete()` | `RecordDeleted` |

## Тела запросов — по командам (переписано 2026-11-10, @AR)

**Здесь был выдуманный DTO `RecordDraft`** с 14 полями. Такого типа в коде **нет**, и он не соответствует ни одному
эндпоинту: тело у команд **разное**, а у `StartManualEntry` его **нет вовсе**. Источник — `JournalCommands.cs:9-21`,
маршруты — `JournalController.cs:49-92`; канон (`api-index.yaml`) с кодом совпадает.

| Команда | Тело (без `clientId` — из `sub`, без `id` — из маршрута) | Адрес |
|---|---|---|
| `StartManualEntry` | **тела нет** (`required: []`, `types` отсутствует) | `JournalController.cs:49`, канон `Journal.StartManualEntry` |
| `StartFromGarmin` | `startedAt` (ISO) · `durationMin` (int) · `garminId` (string) | `JournalCommands.cs:10` |
| `SpecifyWhen` | `startedAt` (ISO) | `:11` |
| `AddPartner` | `partnerId` (string) | `:12` |
| `RemovePartner` | **тела нет** — `partnerId` в маршруте | `JournalController.cs:67` |
| `SpecifyPlace` | `place` (string) | `JournalCommands.cs:14` |
| `AddPositions` | `positions` (string[]) | `:15` |
| `AddAccessories` | `costumes[]` · `toys[]` · `fantasies[]` · `tags[]` | `:16` |
| `RecordContraception` | `contraceptives[]` · `contraceptiveCount` (int) | `:17` |
| `SpecifyDuration` | `durationMin` (int) | `:18` |
| `RecordOrgasms` | `orgasms[]` — каждый: `participant` · `count` · `types[]` · `rating` (`OrgasmsDto`, `:7`) | `:19` |
| `Complete` / `Delete` | **тела нет** | `JournalController.cs:88`, `:91` |
