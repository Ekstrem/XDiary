# DF Issue: FullEventSourcingRepository не реализует реплей (GetById → пустой список)

## Суть
`CommandRepository.Postgres` (nuget 0.2.0): `FullEventSourcingRepository.GetById` / `GetByIdAndVersion` /
`GetByCorrelationToken` читают события из `EventStoreDbContext.Events`, но десериализация событий
в анемичную модель **НЕ реализована**:

- `GetById` → `// TODO: Десериализация событий в анемичные модели через IAggregateFactory` → `return new List<TAnemicModel>();`
- `GetByIdAndVersion` / `GetByCorrelationToken` → `// TODO: Десериализация события в анемичную модель` → `return default!;`

То есть событийный **реплей (fold событий → модель)** в библиотеке отсутствует.
`SaveEventAsync` при этом реализован — сохранение работает.

## Где
- `CommandRepository.Postgres/Repositories/FullEventSourcingRepository.cs` (origin/master).
- Тот же паттерн — в `SnapshotRepository` / `StateOnlyRepository` (там тоже TODO на десериализацию).

## Влияние на 6-msa (Journal)
Полный перенос storage Journal на библиотечный `AddEventStorePostgres` + `IEventStoreRepository`
заблокирован: `AggregateProvider` через `IAnemicModelRepository.GetById` всегда получит пустой список
→ соберёт `DefaultAnemicModel` даже при наличии событий. Конвейер Notifier/BusAdapter (QG101-109) не сможет
загрузить реальное состояние агрегата.

Сейчас реплей в Journal закрывает собственный `RecordEventReplay` (полнее библиотечного).

## Рекомендация
1. Реализовать десериализацию в `FullEventSourcingRepository` через `IAggregateFactory`
   (AbstractAggregate) — свернуть `DomainEventEntry` в `TAnemicModel`.
2. Либо вынести `RecordEventReplay` (Journal) в библиотеку как эталонный fold
   (это тот самый «перенести replay в библиотеку» из плана эталона EDM).
