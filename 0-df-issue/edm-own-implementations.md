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

## Статус
Открыт. Ждёт подтверждения эталона от @AR и приоритизации (ре-рефакторинг по всем 11 BC).