# DF Issue: идемпотентная миграция (CREATE TABLE IF NOT EXISTS) недоступна в EF 8

## Суть
`MigrationBuilder.CreateTable` в EF Core 8.0.11 не имеет параметра `ifNotExists`
(проверено локально: CS1739 «нет параметра с именем ifNotExists»). Для read-store
(`AddReadStores` в `6-msa/BC-Journal` и `6-msa/BC-Partners`) нужна идемпотентность:
повторный `Migrate()` при существующих таблицах, но без записи в `__EFMigrationsHistory`,
падает `42P07` (relation already exists). Обход через
`migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS ...")` запрещён — это DF-отклонение
(`RULES.BackEnd.md`: `git grep 'CREATE TABLE IF NOT EXISTS\|EnsureCreated' -- 6-msa` → 0).

## Где в библиотеке (эталон)
- `AddEventStorePostgres` / `AddReadStorePostgres` — управление схемой read-store;
  идемпотентного DDL у провайдера нет.

## Рекомендация
1. Синхронизировать `__EFMigrationsHistory` со схемой на старте сервиса (зона @DO — БД),
   либо
2. дать идемпотентный `CreateTable` (`ifNotExists`) на уровне провайдера/фреймворка
   (как в EF Core 9).

## Статус
Открыт. Код `AddReadStores` возвращён к `migrationBuilder.CreateTable` (стандарт).
