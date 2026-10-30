# DF Issue: миграции схемы БД не покрыты скиллом EDM

## Суть
Скилл `ddd-microservice-csharp` (EDM) **не описывает** стратегию создания и эволюции схемы БД:
- нет EF Core миграций (`dotnet ef migrations add`, `Database.Migrate()`),
- нет `EnsureCreated()`.

## Где в скилле
- `references/implementation-guide.md` упоминает только пакеты DigiTFactory:
  - `AddEventStorePostgres<T,M>(conn, options)` → `IAnemicModelRepository<T,M>` (Postgres);
  - `AddReadStorePostgres<T,M,Ctx>(conn, options)` → `IReadRepository<T,M>`, `IReadModelStore<T,M>`.
- Предполагается, что схему создают сами пакеты (event store / read store). Явных инструкций по миграциям/созданию таблиц нет.

## Текущее состояние (6-msa)
- Классический CEQRS (по эталону EShop) использует ручные EF Core `CommandDbContext` / `ReadDbContext`.
- Схема создаётся через `Database.EnsureCreated()` при старте (BE-013).
- Папки `*/Storage/Migrations/` пустые; файлов `*Migration*.cs` — 0.
- `EnsureCreated()` и Migrations взаимоисключающие → будущая эволюция схемы потребует drop + recreate.

## Рекомендация
1. Добавить EF-миграции: `dotnet ef migrations add InitialCreate` для `CommandDbContext` каждого сервиса.
2. Заменить `EnsureCreated()` на `CommandDbContext.Database.Migrate()`.
3. Либо (вариант B) вернуться к пакетам `AddEventStorePostgres` / `AddReadStorePostgres`, которые сами управляют схемой.
## ✅ Реализовано (2026-10-31)
- `dotnet ef migrations add InitialCreate` для `CommandDbContext` всех 9 сервисов + `CommandDbContextFactory`.
- `Program.cs`: `EnsureCreated()` → `Database.Migrate()`.
- Portability: дополнительная миграция `AddKind` (поле `Kind`).