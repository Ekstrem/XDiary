# DF Issue: миграции схемы БД не покрыты скиллом EDM

## Суть
Скилл `ddd-microservice-csharp` (EDM) **не описывает** стратегию создания и эволюции схемы БД:
- нет EF Core миграций (`dotnet ef migrations add`, `Database.Migrate()`),
- нет `EnsureCreated()`.

## Где в скилле
- `references/implementation-guide.md` упоминает только пакеты DigiTFactory:
  - `AddEventStorePostgres<T,M>(conn, options)` → `IAnemicModelRepository<T,M>` (Postgres);
  - `AddReadStorePostgres<T,M,Ctx>(conn, options)` → `IReadRepository<T,M>`, `IReadModelStore<T,M>`.
- ~~Предполагается, что схему создают сами пакеты (event store / read store).~~ **НЕВЕРНО (проверено @AR по коду `43b6689`): пакеты регистрируют `DbContext` с `MigrationsAssembly` и сервисы, но схему НЕ создают — ни event store, ни read-store.** Явных инструкций по миграциям/созданию таблиц нет.

## Текущее состояние (6-msa)
- Классический CEQRS (по эталону EShop) использует ручные EF Core `CommandDbContext` / `ReadDbContext`.
- Схема создаётся через `Database.EnsureCreated()` при старте (BE-013).
- Папки `*/Storage/Migrations/` пустые; файлов `*Migration*.cs` — 0.
- `EnsureCreated()` и Migrations взаимоисключающие → будущая эволюция схемы потребует drop + recreate.

## Рекомендация
1. Добавить EF-миграции: `dotnet ef migrations add InitialCreate` для `CommandDbContext` каждого сервиса.
2. Заменить `EnsureCreated()` на `CommandDbContext.Database.Migrate()`.
3. ~~Либо (вариант B) вернуться к пакетам `AddEventStorePostgres` / `AddReadStorePostgres`, которые сами управляют схемой.~~ **НЕВЕРНО (проверено @AR по коду `43b6689`): пакеты схему НЕ создают — миграции добавляем сами в сборке контекста.**
## ✅ Реализовано (2026-10-31)
- `dotnet ef migrations add InitialCreate` для `CommandDbContext` всех 9 сервисов + `CommandDbContextFactory`.
- `Program.cs`: `EnsureCreated()` → `Database.Migrate()`.
- Portability: дополнительная миграция `AddKind` (поле `Kind`).

## Дефект: `AddEventStorePostgres` перекрывает `MigrationsAssembly` (2026-11-05)

`AddEventStorePostgres<T,M>` (CommandRepository.Postgres 0.1.0) регистрирует `EventStoreDbContext`/`DbContextOptions`
через `TryAdd` **первым** и **без** `MigrationsAssembly`. Поэтому `AddDbContext<EventStoreDbContext>(..., x.MigrationsAssembly(...))`,
вызванный ПОСЛЕ, не перекрывает регистрацию (`TryAdd` добавляет только отсутствующее) → `Migrate()` применяет пустой набор
(`GetMigrations()` = пусто; `[migrate] migrations=` пусто).

**Обход (рабочий):** регистрировать `AddDbContext<EventStoreDbContext>(o => o.UseNpgsql(conn, x => x.MigrationsAssembly("<Storage>")))`
**ДО** `AddEventStorePostgres` — тогда наш `TryAdd` срабатывает первым и `MigrationsAssembly` сохраняется.
Проверка: `[migrate] migrations=AddEventStore` (не пусто) + `to_regclass('"Commands"."DomainEvents"')` не NULL.

**Коммит (фикс):** `de5507f0` (Partners + Journal, swap порядка регистрации). Сборки зелёные. (`31e0d03a` — merge, не фикс.)