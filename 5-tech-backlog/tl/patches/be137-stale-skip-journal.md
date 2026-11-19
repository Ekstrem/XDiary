# Патч: `BE-137` — `LegacyTimestampFloor` в `JournalEventConsumer`

**Кому:** @BE. **Передан:** 2026-11-19 08:45. **Кто применяет:** @BE; если не применит за 1 час (до 09:45) — координатор @TL применяет сам с пометкой ответственности.

## Что делает
`6-msa/BC-Journal/src/Journal.Api/Events/JournalEventConsumer.cs:72` — stale-skip начинает учитывать пол legacy-версии:

```csharp
const long LegacyTimestampFloor = 1_000_000_000_000L;
if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
```

Ровно тот же приём, что уже в `main` в `BC-Client` (`#2889`) и `BC-Partners` (`BE-131`).

## Зачем
Read-модель `Journal` заморожена: `version` у строк = ms-таймстамп (`1795066210997`), у события — `1,2,3…`, значит `existing.Version >= domainEvent.Version` истинно всегда. Следствие — два падения `smoke-front` (`wizard-record`: запись не находится по времени; `FE-014`: партнёр не выбран по умолчанию).

## Замеры (независимые)
- @TL (стенд): `POST …/when` → 200, `root.startedAt = 2026-11-02T05:16`; `GET /journal/records/{id}` → `startedAt: 0001-01-01`, `durationMin: 0`, `place: ""`, `positions: []`, `partners: []`, `version: 1795066210997`.
- @DO (БД): `journal`: events=5876, `journal_read`=1296; у трёх последних записей `Version` = `1795066210997/1795066200035/1795065426345`, `StartedAt = 0001-01-01` у всех трёх; при этом в `DomainEvents` есть `Version 4 = SpecifyDuration`, `5 = AddPositions`, `6 = Complete`.

## Проверка после применения
1. `dotnet build 6-msa/BC-Journal/src/Journal.Api/Journal.Api.csproj`;
2. на стенде: `POST …/when` → `GET /journal/records/{id}` отдаёт переданный `startedAt`;
3. `smoke-front` — `wizard-record` и `FE-014` проходят.

## Риск
Минимальный: одна строка условия, поведение для новых строк не меняется (у новых `Version` < 1e12), legacy-строки начинают догонять события.

## Проверено координатором при передаче (2026-11-19 08:33)

- `git apply --check` → **rc=0** (патч применяется к текущему `main` без конфликтов);
- `dotnet build 6-msa/BC-Journal/src/Journal.Api/Journal.Api.csproj` с патчем → **0 ошибок** (предупреждения: дубль `PackageReference` в csproj, уязвимость `System.IdentityModel.Tokens.Jwt 7.0.3` — к правке не относятся);
- локальная правка после проверки **откачена**; кода в `6-msa/` координатор не коммитит (зона @BE).
