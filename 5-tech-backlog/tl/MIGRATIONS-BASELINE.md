# BASELINE для `AddReadStores` — как убрать `42P07` без потери данных (@TL, 2026-11-05 23:4x)

**Применяет @DO** (зона инфраструктуры). Здесь — только проверки и одна вставка истории; ни одного `DROP`.

## Что установлено (по фактам этой сессии)

1. `deploy.sh:29-30`: `ConnectionStrings__CommandDb` и `ConnectionStrings__ReadDb` указывают на **одну и ту же базу**
   (`partners` → `partners`, `journal` → `journal`). Значит оба контекста пишут в **одну** `__EFMigrationsHistory`.
2. В истории базы `partners` **нет** `20261105170447_AddReadStores`, а таблица `partners_read` **есть** (её читает сервис, 298 строк).
3. Та же картина у `journal`: миграция `20261105170525_AddReadStores` создаёт `journal_defaults`, `journal_read`, `partners_directory`,
   а `journal_read` уже существует (52 строки).
4. Миграция делает `CreateTable("partners_read")` (`ReadStore/20261105170447_AddReadStores.cs:14-29`) ⇒ `CREATE TABLE` на существующую
   таблицу ⇒ **`42P07 duplicate_table`** ⇒ crash-loop новых образов.

## Шаг 1 — ПРОВЕРКИ (только чтение)

```sql
-- partners
SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%AddReadStores%';   -- ожидаем 0 строк
SELECT to_regclass('public.partners_read') IS NOT NULL AS partners_read_exists;                -- ожидаем t
SELECT column_name, data_type, is_nullable
  FROM information_schema.columns WHERE table_name = 'partners_read' ORDER BY ordinal_position;

-- journal
SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%AddReadStores%';
SELECT to_regclass('public.journal_read')   IS NOT NULL AS journal_read_exists,
       to_regclass('public.journal_defaults') IS NOT NULL AS journal_defaults_exists,
       to_regclass('public.partners_directory') IS NOT NULL AS partners_directory_exists;
```

## Шаг 2 — сверка схемы (должно совпасть)

`partners_read`: `Id uuid NOT NULL PK`, `Name text NOT NULL`, `Photo text NOT NULL`, `Gender text NOT NULL`,
`Notes text NOT NULL`, `ClientId uuid NULL`, `DeletedAt timestamptz NULL`.

**Если совпадает** → шаг 3. **Если нет** (тип/`nullable` расходятся) → сначала `ALTER TABLE …` до соответствия, **потом** шаг 3.

## Шаг 3 — baseline (одна вставка, идемпотентная формулировка)

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20261105170447_AddReadStores', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105170447_AddReadStores');

-- journal, только если все три таблицы существуют
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20261105170525_AddReadStores', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105170525_AddReadStores');
```

## Шаг 4 — доказательство

1. поднять **новый** образ partners/journal на dev;
2. `podman logs` — **нет** `42P07`, `[migrate]` печатает непустой список;
3. `partners_read`/`journal_read` по числу строк **не уменьшились**;
4. `/health` у всех 12 сервисов → 200 (после `BE-081` у `analytics` тоже 200).

## Чего здесь НЕТ и почему

Нет `DROP`/пересоздания баз: таблицы уже соответствуют миграции, поэтому разрушать данные незачем.
Пересоздание остаётся вариантом **только** если сверка схемы покажет расхождение, которое не чинится `ALTER`.
