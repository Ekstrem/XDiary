# DF Issue: API event-store (SaveEventAsync) не читается офлайн

## Суть
Для E1 нужно переключить `JournalCommandRepository.SaveAsync` с `_db.SaveChangesAsync`
на DF `SaveEventAsync` (event sourcing). Но точная сигнатура `SaveEventAsync` и тип
события (`IDomainEvent` / конверт) не извлекаются из DLL офлайн:
- `GetTypes()` падает с `ReflectionTypeLoadException` (не грузится зависимость
  `Microsoft.Extensions.DependencyInjection.Abstractions`).
- `strings` даёт только имена (`SaveEventAsync`, `GetByIdAndVersion`,
  `GetByCorrelationToken`, `GetEventCountAsync`, `SaveSnapshotAsync`) без параметров.
- XML-doc в пакетах `commandrepository.postgres` / `eventbus.*` / `readrepository.postgres`
  отсутствует (только DLL).

## Что известно (достоверно, по ошибкам компилятора)
- `AddEventStorePostgres<TBoundedContext, TAnemicModel>(IServiceCollection, string, Action<EventStoreOptions>?)`
- `AddEventBusInMemory()` — без generic-аргументов.
- `AddReadStorePostgres<TBoundedContext, TReadModel, TDbContext>(IServiceCollection, string, Action<PostgresReadStoreOptions>)`
  где `TReadModel : DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IReadModel<TBC>`.
- Репозитории пакета: `FullEventSourcingRepository`, `SnapshotRepository`, `StateOnlyRepository`
  (пространства `...Repositories.*`).

## Конверт события (из @AR 2026-11-14 08:55, до выхода `event-delivery.md`)
`eventId`, `type`, `schemaVersion`, `aggregateId`, `version`, `clientId`, `occurredAt`,
`correlationToken`, `payload`. `clientId` обязателен.

## Разблокирует
1. `04-architecture/event-delivery.md` в origin (контракт доставки + точный тип события).
2. ИЛИ рабочий `dotnet`-стенд с восстановлением пакетов (прочитать сигнатуры через reflection).

## Статус
Открыт. E1 (переключение `SaveAsync` → store) стоит на этом.
