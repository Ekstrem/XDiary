# EDM-верификация: отклонения кода BE (6-msa) от скилла

> Цель проекта — проверить генерацию микросервисов по EDM. Эталон — `edm-net.md`
> (DigiTFactory .NET, DDD + Event Sourcing + CEQRS). Аудит: код `6-msa/` сверен с
> каноническими паттернами скилла.

## Вывод (кратко)
BE взял **SeedWorks** (доменные блоки DigiTFactory: `BusinessOperationData`, `AggregateResult`,
`IAnemicModel`, `IBoundedContext`, `IValueObject`), но **не взял инфраструктуру DigiTFactory**
(Event Store, Event Bus, Read Store). Вместо неё — **собственный слой**: EF Core
(`SaveChangesAsync`) + ручной HTTP-паблишер. Это **системное** отклонение — паттерн одинаков
во всех BC (журнал, каталог, партнёры, клиент, гейтвеи и т.д.).

## Критические отклонения (ядро CEQRS нарушено)

### 1. Нет Event Sourcing (Event Store)
- **Скилл:** `AddEventStorePostgres` + `SaveEventAsync` (`EventStoreStrategy.FullEventSourcing`), Event Store = источник истины.
- **BE:** EF Core `AddDbContext` + `SaveChangesAsync` (state-персистентность). `SaveEventAsync`/`AddEventStorePostgres`/`EventStoreStrategy` — **0 вхождений**.
- **Док-во:** `BC-Journal/src/Journal.Storage/JournalCommandRepository.cs:22` (`_db.SaveChangesAsync`), `Program.cs:22` (`AddDbContext<CommandDbContext>`).

### 2. Нет IEventBus (шины событий)
- **Скилл:** `Command → Aggregate.BusinessMethod() → IEventBus.Publish(event)`, **НЕ пишет напрямую в БД**.
- **BE:** команда пишет напрямую в БД (`_repo.SaveAsync`) + публикует событие **ручным HTTP** (`HttpJournalEventPublisher` → `POST /analytics/events`). `IEventBus` из DF — **0 вхождений**.
- **Док-во:** `JournalHandlers.cs:160` (`CompleteHandler` → `_repo.SaveAsync` + `_publisher.PublishRecordCompletedAsync`), `HttpJournalEventPublisher.cs:34` (`HttpClient.PostAsJsonAsync`).

### 3. Нет Event Consumer (IHostedService)
- **Скилл:** `IEventBus → Event Consumer (IHostedService) → SaveEventAsync + UpsertAsync + Outbound Handler`.
- **BE:** `IHostedService` — **0 вхождений**. Деплоймент событий = ручной HTTP-вызов; приём на стороне Analytics = обычный контроллер `POST /analytics/events`, а не event consumer.

### 4. Нет Read Model Projector (UpsertAsync / MV)
- **Скилл:** проекция `IDomainEventHandler` + `UpsertAsync` → Materialized View (прекомпьют).
- **BE:** `UpsertAsync`/`IReadModelStore`/`IDomainEventHandler` — **0 вхождений**. MV нет; query считает на лету.

### 5. Нет разделения Command DB / Read DB
- **Скилл:** Command DB (Event Store) ≠ Read Store (MV), разные схемы/БД.
- **BE:** одна таблица `Records` (EF-entity) и для команды (`CommandDbContext`), и для чтения (`ReadDbContext`). Query читает ту же таблицу, что пишет команда.

## Запрещённое скиллом (явный пункт «Запрещено»)

### 6. Свои интерфейсы инфраструктуры
- **Скилл:** «❌ НЕ создавать свои `IEventBus`, `IAnemicModelRepository`, `IReadRepository`».
- **BE:** `Shared/XDiary.Contracts/Infrastructure.cs` определяет **свои**: `IReadRepository<TBC,TModel>` (L18), `IRepository<TBC,TModel>` (L27), `IBusAdapter<TBC>` (L36), `ICommandDbContext<TBC>` (L9), `INotifier<TBC>` (L42), `IReadModel<TBC>` (L15).

## Второстепенные отклонения

### 7. Валидаторы не через цепочку `ValidateCommand`
- **Скилл:** `BusinessOperationData.Commit(...).ValidateCommand(new {Invariant}Validator())`.
- **BE:** агрегат проверяет валидаторы **вручную** (`if (!validator.IsSatisfiedBy(...)) return Fail(...)`) либо не проверяет вовсе. `IBusinessOperationValidator` — **0 вхождений** (валидаторы свои: `DurationPositiveValidator` и т.п.).
- **Док-во:** `Journal.Domain/Implementation/Aggregate.cs:144-179`.

### 8. Мутабельные anemic-модели
- **Скилл:** «Все доменные классы иммутабельны (приватные конструкторы, фабрики)».
- **BE:** `AnemicModel` с публичными сеттерами; агрегат мутирует через `Clone()` + присваивание (`anemic.Place = place`).
- **Док-во:** `Aggregate.cs:34-55`, `SpecifyPlace` (L111-116).

### 9. Обработчик грузит агрегат из репозитория, а не из AggregateProvider
- **Скилл:** `AggregateProvider.GetAggregateAsync(id)` (загрузка из Event Store).
- **BE:** `_repo.GetByIdAsync(id)` (EF Core запрос по таблице).
- **Док-во:** `JournalHandlers.cs:38` (`_repo.GetByIdAsync`).

### 10. Рантайм-агрегация в query (нарушение MV)
- **Скилл:** MV = прекомпьют; query читает готовое, не считает на лету.
- **BE:** `JournalReadRepository` считает на лету: `GetPlacesAsync` → `Distinct()`, `GetFantasiesAsync` → `SortedSet` по всем строкам, `GetRecentAsync` → `OrderByDescending + First`.
- **Док-во:** `JournalReadRepository.cs:21-44`.

## Что корректно (соответствует скиллу)
- ✅ Домен зависит только от `SeedWorks` (`BusinessOperationData`, `AggregateResult`, `IAnemicModel`, `IBoundedContext`, `IValueObject`).
- ✅ MediatR (CQRS), Autofac (DI).
- ✅ Команды — **только POST** (нет `[HttpPut]`/`[HttpDelete]`/`[HttpPatch]`), чтение — GET.
- ✅ Именованные бизнес-методы (не CRUD): `StartManualEntry`, `SpecifyWhen`, `RecordOrgasms`, `Complete`, `Delete`.
- ✅ Value Objects с фабриками (`RecordRoot.CreateInstance`, `Orgasms.CreateInstance`).

## Рекомендация (путь возврата к EDM)
1. Вернуть инфраструктуру DF: `AddEventStorePostgres` (Event Store) + `AddEventBusInMemory` (шина) + `AddReadStorePostgres` (Read Store).
2. Команда: `Aggregate.BusinessMethod()` → **`IEventBus.Publish(event)`** (убрать `_repo.SaveAsync` + ручной HTTP-паблишер).
3. Деплоймент: `Event Consumer (IHostedService)` → `SaveEventAsync` (Event Store) + `UpsertAsync` (MV) + `Outbound Handler` (соседям).
4. Query: `QueryHandler → IReadRepository (DF) → Read Store` (убрать рантайм `Distinct`/`SortedSet` — заменить на MV).
5. Валидаторы — через `ValidateCommand(new {Invariant}Validator())` (реальный `IBusinessOperationValidator`).
6. Убрать `Shared/XDiary.Contracts/Infrastructure.cs` (свои `IReadRepository`/`IBusAdapter`/`IRepository`/`ICommandDbContext`/`INotifier`).

## Файлы-доказательства (журнал как представитель всех BC)
- `6-msa/Shared/XDiary.Contracts/Infrastructure.cs` — свои интерфейсы.
- `6-msa/BC-Journal/src/Journal.Api/Program.cs` — `AddDbContext` вместо `AddEventStorePostgres`/`AddEventBus`/`AddReadStore`.
- `6-msa/BC-Journal/src/Journal.Storage/JournalCommandRepository.cs` — `SaveChangesAsync` (state).
- `6-msa/BC-Journal/src/Journal.Storage/JournalReadRepository.cs` — рантайм-агрегация.
- `6-msa/BC-Journal/src/Journal.Application/Commands/JournalHandlers.cs` — `_repo.SaveAsync` + HTTP-паблишер.
- `6-msa/BC-Journal/src/Journal.Domain/Implementation/Aggregate.cs` — ручные валидаторы, мутабельная модель.
