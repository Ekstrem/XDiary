# BC-Journal — микросервис `journal`

Журнал интимной активности (Core Domain, роль Execution). Один ограниченный контекст `Journal`
и один агрегат **Record** (запись / акт).

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002).

## Агрегат Record

Сущность = **partnerId** (uuid) + **startedAt** («YYYY-MM-DDTHH:mm»). 12 команд по экранам визарда.

| VO | Поля |
|---|---|
| Root | partnerId, startedAt |
| Duration | durationMin |
| Place | place |
| Positions | positions[] |
| Accessories | costumes[], toys[], fantasies[], tags[] |
| Contraception | contraceptives[], contraceptiveCount |
| Orgasms | orgasmsMe, orgasmsPartner, orgasmMeType[], orgasmPartnerType[], orgasmMeRating, orgasmPartnerRating (0–5) |
| Origin | garmin, garminId, createdAt, updatedAt |

## Command API (`/api/v1/journal`) — визард

| Команда | Эндпоинт |
|---|---|
| StartManualEntry | `POST /records` |
| StartFromGarmin | `POST /records/from-garmin` |
| SpecifyWhen | `POST /records/{id}/when` |
| ChoosePartner | `POST /records/{id}/partner` |
| SpecifyPlace | `POST /records/{id}/place` |
| AddPositions | `POST /records/{id}/positions` |
| AddAccessories | `POST /records/{id}/accessories` |
| RecordContraception | `POST /records/{id}/contraception` |
| SpecifyDuration | `POST /records/{id}/duration` |
| RecordOrgasms | `POST /records/{id}/orgasms` |
| Complete | `POST /records/{id}/complete` |
| Delete | `DELETE /records/{id}` |

## Query API (читает Read Store, не Event Store)

| Запрос | Эндпоинт |
|---|---|
| Журнал (список) | `GET /records` (startedAt DESC) |
| Деталь записи | `GET /records/{id}` |

## Сборка и тесты

```bash
cd src
dotnet restore Journal.sln
dotnet build Journal.sln --configuration Release
dotnet test Journal.Domain.Tests/Journal.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-journal:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Journal/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-002.md`
