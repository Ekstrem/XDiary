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

## Консьюмер: IDomainEventHandler — чистый маркер (round 9)
`IDomainEventHandler<T>`: интерфейсов нет, методов нет (проверено `GetMethods` всех binding-флагов).
`InMemoryEventBus.Publish(IDomainEvent)` + `Subscribe(IDomainEventHandler)` — как `Publish` вызывает
хендлер без `Handle`, из DLL не читается (внутренние лямбды только по `Type`-ключу).
`IDomainEvent<T>` → `IComplexKey, IHasKey, IHasVersion, IHasCorrelationToken, IBoundedContextDescription,
IDomainOperationResult`; геттеры `Command` + `ChangedValueObjects`.
**Вывод:** механизм хендлера задаётся конвенцией/рефлексией, которой нет в метаданных. Для консьюмера
нужен пример из DF-доков или исходник; писать `Handle` наугад нельзя.

## Канон агрегата/шины (round 41, рефлексия SeedWorks 0.5.0)

Найдены типы канона DF (то, что «классический CEQRS» заменил самописным):
- `AggregateBusinessOperation<TSelf, TBC>.Handle(IAnemicModel, CommandToAggregate, IBoundedContextScope<TBC>)` → `AggregateResult`;
- `IAggregateProvider<TBC>.GetAggregate(Guid[, long])` → `TAnemicModel` (non-null), `GetAggregateAsync`;
- `IBoundedContextScope<TBC>` — `Operations` + `Validators`;
- `CommandToAggregate` (конкретный) — `CorrelationToken`/`Version`/`CommandName`/`SubjectName`;
- `DomainEventNotifier<T, ...>` — публичных методов в метаданных нет (механизм notify→Publish внутренний).

То есть `BusAdapter`/`Notifier`/`IRepository` → канон: команда → `IAggregateProvider.GetAggregate` →
`AggregateBusinessOperation.Handle` → `result.Event` → `DomainEventNotifier`/`IEventBusProducer.Publish`.
Раскатка = «вернуть наследование DF-базы» (E1), по BC; точный `BusAdapter`-пример — в
`microservice-implementation-guide.md` (в нашем репо отсутствует).

### Регистрация (round 55–56, через `ServiceCollection` + перечень сервисов)

`CommandRepository.Postgres.Extensions.ServiceCollectionExtensions` — публичный метод **один**:
`AddEventStorePostgres(IServiceCollection, String, Action<EventStoreOptions>)`. Что он регистрирует
(7 сервисов, фактически):

1. `EventStoreOptions` — Singleton;
2. `ServiceProviderAccessor` — Singleton;
3. `DbContextOptions<EventStoreDbContext>` + `DbContextOptions` — Scoped;
4. `EventStoreDbContext` — Scoped;
5. `IEventStoreRepository<TBC,TAnemicModel>` → `FullEventSourcingRepository<…>` — Scoped;
6. `IAnemicModelRepository<TBC,TAnemicModel>` → (factory) — Scoped.

**Вывод для E1:** `IAnemicModelRepository`/`IEventStoreRepository` — есть в DI; **`IAggregateProvider`
не регистрируется** этим методом (значит `IAggregateProvider` — либо другая библиотека, либо пишется
свой). E1 заменяет `IRepository.GetByIdAsync` → `IAnemicModelRepository.GetById`, `SaveAsync` →
`IEventBusProducer.Publish(result.Event)`; `BusAdapter`/`Notifier` — не нужны (канон публикует напрямую).

### Точная сигнатура `IAnemicModelRepository` (round 58)

- `Task<List<TAnemicModel>> GetById(Guid, CancellationToken)` — **список** (история версий агрегата);
- `Task<TAnemicModel> GetByIdAndVersion(Guid, long, CancellationToken)` — одна версия;
- `Task<TAnemicModel> GetByCorrelationToken(Guid, CancellationToken)`.

`GetById` возвращает **список**, а не одиночную модель — для UPDATE-команды берётся последняя версия.
Это и есть семантика замены `IRepository.GetByIdAsync → TModel?` на канон.
