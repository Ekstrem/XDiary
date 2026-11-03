# DF API event-store / event-bus (извлечено через dotnet-reflection, 2026-11-14)

## Стенд
`dotnet run` на кэшированных пакетах (offline, `--no-restore` не нужен — пакеты в кэше).
Чтение через `Assembly.Load` + `GetTypes()`. Способ рабочий — он закрыл блокер чтения API.

## Event store (`CommandRepository.Postgres`)

`IEventStoreRepository<TBC, TAnemicModel>` (регистрируется `AddEventStorePostgres<TBC,TModel>`):
- `Task SaveEventAsync(DomainEventEntry entry, CancellationToken)`
- `Task SaveSnapshotAsync(Guid id, Int64 version, String aggregateJson, CancellationToken)`
- `Task<Int64> GetEventCountAsync(Guid id, CancellationToken)`
- `Task<TModel?> GetById(Guid id, CancellationToken)` / `GetByIdAndVersion(Guid, Int64)` /
  `GetByCorrelationToken(Guid)`

Репозитории: `FullEventSourcingRepository`, `SnapshotRepository`, `StateOnlyRepository`
(стратегия — `EventStoreOptions.Strategy`).

### `DomainEventEntry` (запись в store)
`Guid Id`, `Int64 Version`, `Guid CorrelationToken`, `String BoundedContext`,
`String CommandName`, `String SubjectName`, `String ChangedValueObjectsJson`,
`String Result`, `DateTime CreatedAt`.

## Event bus (`EventBus.InMemory` → `SeedWorks.Events`)

`IEventBusProducer`:
- `void Publish(IDomainEvent<TAggregate> domainEvent)` / `Task PublishAsync(...)`

### `DomainEvent<TCommand>` (событие агрегата)
`Guid Id`, `Guid CorrelationToken`, `Int64 Version`, `ICommandToAggregate Command`,
`IDictionary ChangedValueObjects`, `String BoundedContext`, `String ContextName`,
`Int32 MicroserviceVersion`, `DomainOperationResultEnum Result`, `String Reason`.

## Маппинг на конверт @AR (08:55)
- `eventId` → `DomainEvent.Id`
- `version` → `Version`; `aggregateId` → `Id`/`CorrelationToken` (уточнить)
- `correlationToken` → `CorrelationToken`
- `type` → `BoundedContext` + `CommandName` + `SubjectName`
- `occurredAt` → `CreatedAt`
- `payload` → `ChangedValueObjects` (VO) / `ChangedValueObjectsJson`
- **`clientId`** — в DF нет отдельного поля → входит в `ChangedValueObjects` (скаляр `ClientId`
  должен попасть в `GetValueObjects()`, иначе выпадет из события).

## Следствие для E1
E1 = вернуть наследование DF-базы (как в `0-df-issue/edm-own-implementations.md`):
команда → `IAggregateProvider.GetAggregate` → `AggregateBusinessOperation.Handle` →
`IEventBusProducer.Publish(DomainEvent)` → consumer → `SaveEventAsync` + `UpsertAsync`.
Свой `IRepository`/`JournalCommandRepository.SaveChangesAsync` — заменить на
`IEventStoreRepository.SaveEventAsync` (command-path перестаёт писать через EF).

## Статус
API прочитан. E1 разблокирован по сигнатурам; жду `04-architecture/event-delivery.md`
для контракта доставки (outbox/диспетчер/идемпотентность).

## Дополнено (round 6): DF сам строит событие
`AggregateResult<TBC,TModel>` уже содержит: `Event` (IDomainEvent), `ChangeValueObjects`
(IDictionary), `BusinessOperationData`, `Result`, `Reason`. Значит `AggregateBusinessOperation.Handle`
даёт готовый `result.Event` → `IEventBusProducer.Publish(result.Event)`. Маппинг
`DomainEvent → DomainEventEntry` делает DF (`DomainEventEntryMapping`).
Свой `SaveAsync(result)` → заменить на `Publish(result.Event)` + consumer → `SaveEventAsync`.

## Консьюмер (round 8)
`IEventBusConsumer.Subscribe(IDomainEventHandler<T> handler)` + `Unsubscribe`.
`IReadModelStore<TBC,TReadModel>.UpsertAsync(TReadModel, CancellationToken)` / `DeleteAsync(Guid)`.
`PostgresReadRepository.GetByIdAsync` / `GetAllAsync(IPaging)` / `CountAsync`.
**НЕЯСНО:** `IDomainEventHandler<T>` — маркер без `Handle` (отражение `GetMethods()` пусто).
Механизм вызова хендлера (reflection/convention) не прочитан офлайн — нужен пример из DF-доков
или исходник. До этого консьюмер `JournalEventConsumer` писать рискованно.
