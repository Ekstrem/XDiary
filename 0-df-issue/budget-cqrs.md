# Budget CQRS (дешёвый CQRS поверх DF EDM) — рецепт

Черновик-памятка по итогам раскатки E1 (Partners/Journal) + P0-приватности. Не ADR —
рабочая шпаргалка «как это собирается из кубиков DF без kafka».

## Идея

Полноценный CEQRS слушает события как источник истины (kafka). **Budget CQRS** = та же
семантика, но на **in-memory шине DF** и **синхронном наблюдателе**: команда → агрегат →
событие → наблюдатель → «сразу» пишет в event store и обновляет матпредставление.

```
Controller ──IMediator.Send(command)──> Handler
                                          ├─ Aggregate.CreateInstance(model).Метод(...)  → AggregateResult{Event,Model}
                                          └─ IRepository.SaveAsync(result)
                                                ├─ INSERT outbox (свой backstop, BE-059)
                                                └─ IEventBusProducer.PublishAsync(event)  ← DF InMemoryEventBus
                                                      └─ [cast] IObserver<IDomainEvent<T>>.OnNext(e)
                                                            └─ EventHandler.HandleAsync(e)
                                                                  ├─ IEventStoreRepository.SaveEventAsync(entry)  ← источник истины
                                                                  └─ IReadModelStore.UpsertAsync(readModel)         ← матпредставление

Read ──IMediator.Send(query)──> Handler → IQueryRepository.GetByIdAsync → read-модель
                                        └─ при промахе → IAnemicModelRepository.GetById → пересборка из событий
```

## DF-кубики (проверено рефлексией)

| Абстракция | Что делает | Где зарегистрирована |
|---|---|---|
| `IEventStoreRepository<TBC,TModel>` → `FullEventSourcingRepository` | `SaveEventAsync` — пишет `DomainEventEntry` | `AddEventStorePostgres` (внутри) |
| `IAnemicModelRepository<TBC,TModel>` | `GetById(id)` → **`List<TAnemicModel>`** (история версий) | `AddEventStorePostgres` (внутри) |
| `IReadModelStore<TBC,TRead>` | `UpsertAsync` — матпредставление | `AddReadStorePostgres` |
| `IEventBusProducer` / `IEventBusConsumer` | in-memory шина | `AddEventBusInMemory` |

`IAggregateProvider<TBC>` **не регистрируется** `AddEventStorePostgres` — читаем через
`IAnemicModelRepository.GetById`; агрегат создаём своим `Aggregate.CreateInstance(model).Метод(...)`.

## Ключевые грабли (каждая = реальный дефект из этой сессии)

1. **Хендлер обязан реализовать `IObserver<IDomainEvent<T>>`.** DF `InMemoryEventBus.Publish`
   кастит хендлер к `IObserver` — без `OnNext` ничего не вызовется (`InvalidCastException`
   глушится, `[handle] entered` = 0). Адаптер: `OnNext(e) => _ = HandleAsync(e)`.

2. **`OnNext` — fire-and-forget, `HandleAsync` — `await`.** Если держать
   `SaveEventAsync`/`UpsertAsync` через `.GetAwaiter().GetResult()` — запрос `POST` висит
   до всех DB-записей → **504**. Синхронный consumer в запросе = таймаут шлюза.

3. **`ChangedValueObjectsJson` — с `$type`-дискриминатором.** Без него event-sourced
   `GetById` не реконструирует `IValueObject` (интерфейс) → `List` пустой → «not found» 400.
   Конвертер общий — `XDiary.Contracts.IValueObjectJsonConverter`.

4. **`GetByIdAsync` — гибрид, не «только read-модель».** read-модель наполняется проекцией
   с лагом (fire-and-forget) ⇒ свежая запись ещё не в `partners_read`/`journal_read` ⇒
   `when` 404, `remove`/`link-client` 400. Правильно: `read → при промахе IAnemicModelRepository.GetById → Last()`.

5. **Владелец — `sub` из токена.** Команды пишут `ClientId` из `sub` (не из тела — IDOR);
   чтения фильтруют `Where(x => x.ClientId == clientId)`; чужой `id` → 404/403.

6. **Регистрация: `AddDbContext(MigrationsAssembly)` ДО `AddEventStorePostgres`** (TryAdd —
   первый выигрывает), иначе `MigrationsAssembly` теряется и `Migrate()` ничего не делает.

7. **Ручные миграции — с `[Migration]`/`[DbContext]`.** Без атрибутов EF не видит файл —
   `migrations list` его не покажет и `Migrate()` не применит.

## Порядок сборки сервиса (с нуля)

1. Домен: `IContext`/`IAnemicModel`/`IValueObject` + `Aggregate` (методы → `AggregateResult`).
2. `Program.cs`: `AddDbContext<EventStoreDbContext>(MigrationsAssembly)` → `AddEventStorePostgres` →
   `AddEventBusInMemory` → `AddReadStorePostgres` → `AddMediatR` → `AddHostedService<Consumer>`.
3. `EventHandler : IDomainEventHandler<TBC>, IObserver<IDomainEvent<TBC>>` — `HandleAsync` (event store + read model).
4. `CommandRepository : IRepository<TBC,TModel>` — `SaveAsync` = outbox + `PublishAsync`; `GetByIdAsync` = гибрид.
5. Миграции: событийная схема (`AddEventStore`) + read-схема (`AddReadStores`) + `ClientId`-колонки.
6. Контроллер: `[Authorize]` + `sub` → `ClientId` + `EnsureOwns` (владелец).

## Где это не «полный CEQRS» (осознанно)

- шина in-memory — нет гарантии доставки при падении процесса (компенсирует свой outbox, BE-059);
- диспатч событий — DF `IObserver`, **не** MediatR-нотификация (альтернатива — `INotification`,
  но тогда уходим от DF-канона; согласовать с @AR);
- read-модель — eventual consistent, лаг закрыт гибридом `GetByIdAsync`, а не синхронной проекцией.

## Simple: `MATERIALIZED VIEW` против проекции в приложении (рассуждение)

То, что выше — **проекция в приложении** (C# → `UpsertAsync`). Для «Simple»-профиля
(read-only агрегаты) есть альтернатива — **материализованное представление Postgres**:
`CREATE MATERIALIZED VIEW … AS SELECT … GROUP BY …` + `REFRESH`.

| Аспект | MV (Postgres) | Проекция в приложении |
|---|---|---|
| Простые агрегаты (count/sum/top) | проще — SQL вместо C# | код-проекция |
| Сложная математика (Pearson/physio/рейтинги) | не выразить | C# |
| Латентность обновления | O(n) полный refresh (даже `CONCURRENTLY`) | O(1) upsert на событие |
| История/audit | нет (текущее состояние) | event store + пересборка |
| Доменная логика на записи | не снимает | остаётся в домене |

**Вывод:** MV — правильная оптимизация **именно для Simple**, где чтение = `count/sum/top`
и история не нужна (объём «дневника пары» — тысячи строк — делает `REFRESH` копеечным).
Но два наших требования не дают уйти на чистый MV: **корреляции/физиология (не SQL)** и
**событийная история как источник истины (EDM)**. Это спектр, а не «или-или»: MV хорош
для «просто посчитать», пересборка — там, где нужна история или невыразимая в SQL математика.
Переход read-слоя Simple на MV — отдельное решение (ADR через @AR, §5.2), не правка втихую.
