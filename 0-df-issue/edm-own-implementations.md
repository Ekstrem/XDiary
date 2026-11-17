# DF Issue: самописный агрегат/repository вместо наследования базовых классов EDM

## Суть
В `6-msa` реализован «классический CEQRS»: свой класс `Aggregate` с именованными
методами (`Register`/`ChangePin`/`Remove`/`Sync`…), свой `IRepository<TBC,TModel>`
(`GetByIdAsync → TModel?` + `SaveAsync`) и свой `AggregateProvider`. По замыслу EDM/DF
агрегат должен **наследовать** базовые классы библиотеки, а логику получения агрегата
должен давать библиотечный провайдер/репозиторий — не своя реализация.

## Где в библиотеке (эталон)
- `AggregateBusinessOperation<TSelf, TBoundedContext>` — CRTP-база бизнес-операции
  (`Handle(model, command, scope)`), наследник = одна операция, а не один класс на все.
- `IBusinessOperationValidator<TBoundedContext>` — валидация модели.
- `IAggregateProvider<TBC>.GetAggregate(id)` — возвращает **не-null** модель агрегата.
- `IAnemicModelRepository<TBC,TModel>.GetById(id)` → `Task<List<TAnemicModel>>`.
- `AddEventStorePostgres` / `AddReadStorePostgres` — управление схемой.

## Текущее состояние (6-msa)
- Рефакторинг `0d7abef` («классический CEQRS без AbstractAggregate») заменил наследование
  базы на самописные: `Domain/Implementation/Aggregate.cs` (по одному на BC), кастомный
  `IRepository` в `XDiary.Contracts`, `DomainServices/AggregateProvider.cs` (в хендлерах
  фактически не используется).
- Следствие: `GetByIdAsync → TModel?` + `if (m is null) return Fail(...)` в UPDATE-командах;
  в CREATE — `?? new AnemicModel()`. Null-модель — наша, а не из библиотеки.
- `AnemicModel` имеет `public set` на скалярах; в `GetValueObjects()` попадают только VO
  (Root), скаляры (`Photo`/`Gender`/`Notes`) не участвуют в change-треке `Commit()`.

## Рекомендация
1. Вернуться к наследованию базовых классов: бизнес-операции → наследники
   `AggregateBusinessOperation`; валидация → `IBusinessOperationValidator`.
2. Получение агрегата → `IAggregateProvider.GetAggregate(id)` + `IAnemicModelRepository`
   (убрать null-модель и самописный `IRepository`).
3. `AnemicModel` сделать иммутабельным (или явно закрыть сеттеры) и включить скаляры
   в change-трек (`GetValueObjects()`), если они должны порождать события.

## Что сделано (доводка Journal к канону, 2026-11-16, `role/be`)
- Иммутабельный `AnemicModel` (record + init, `with` вместо Clone-мутации);
- state machine `Draft→Recorded→Deleted`; богатые VO (records) + полный diff; сильная типизация
  (`RecordRoot.StartedAt` → `DateTimeOffset`); `CommandName` соразмерен методу; MV-проекция по версии.

## Осталось — 3 gap'а DF (требуют базовые классы/реализацию библиотеки)
1. **Replay из событий.** `FullEventSourcingRepository<TBC,TModel>.GetById` в `CommandRepository.Postgres`
   читает события, но **возвращает пусто** — тело `// TODO: Десериализация событий в анемичные модели через IAggregateFactory`
   (репозиторий `Ekstrem/DigiTFactory.Libraries`, файл `CommandRepository.Postgres/Repositories/FullEventSourcingRepository.cs`).
   То есть реплей — gap самой библиотеки, не микросервиса.
2. **BusAdapter «сохранить И опубликовать».** Ручной `IObserver` в `JournalEventHandler` — «свой адаптер»;
   эталон — `BusAdapter : IObserver<AggregateResult<...>>` (пример `Documents/Journal/DSF`).
3. **Provider + Notifier + `IBusinessOperationValidator`.** Базовые классы **существуют** в `Source/`:
   `AggregateBusinessOperation`, `IAggregateProvider<TBC,TModel>`, `DomainEventNotifier<TBC,TAggregate>`,
   `IBusinessOperationValidator` + `Result.Extension.ValidateCommand`. Нужна интеграция по примеру DSF.

## Статус
Открыт. Эталон читается из DF-доков (`edm-eventstore-api.md` §«Следствие для E1»:
`IRepository.SaveAsync` → `IEventBusProducer.Publish(result.Event)` + consumer → `SaveEventAsync`;
`GetById` → `IAnemicModelRepository`/`IEventStoreRepository`). Код микросервисов — зона @BE,
подтверждение кода от @AR не требуется; приоритизация — по E1-раскатке.
Источник базовых классов — репозиторий `Ekstrem/DigiTFactory.Libraries` (SeedWorks `Source/` + `CommandRepository.Postgres`),
пример интеграции — `Documents/Journal/DSF`.