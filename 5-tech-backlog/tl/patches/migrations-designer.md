# Патч: атрибуты `[DbContext]`/`[Migration]` для двух миграций read-стора

**Автор:** @TL, 2026-11-18 09:04. **Зона применения:** `6-msa` — @BE.

## Проблема (корень `BE-126` и `BE-127`)

Миграции `20261117210000_AddVersionToPartnersRead` и `20261117210000_AddVersionToJournalRead` созданы **руками**: в них нет атрибутов `[DbContext]`/`[Migration]` и нет `.Designer.cs` (у остальных миграций есть). EF Core ищет миграции **по атрибутам** ⇒ `Database.Migrate()` их **не видит** ⇒ колонки `Version` в `partners_read`/`journal_read` не создаются ⇒ запросы чтения падают с `42703` ⇒ **500** на `GET /partners` и `/journal/records`.

Именно поэтому на dev пришлось делать ручной `ALTER` + `INSERT` в `__EFMigrationsHistory` — и именно поэтому на проде это повторится при чистом развёртывании.

## Патч

Добавляет в оба класса миграций по две строки (полные имена, чтобы не конфликтовать с базовым классом `Migration`):

```csharp
[Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(<BC>.Storage.ReadDbContext))]
[Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute("<id>")]
```

Файлы:
- `5-tech-backlog/tl/patches/migrations-designer-AddVersionToPartnersRead.patch` (932 байта)
- `5-tech-backlog/tl/patches/migrations-designer-AddVersionToJournalRead.patch` (916 байт)

## Проверено @TL

```
dotnet build 6-msa/BC-Partners/src/Partners.Storage/Partners.Storage.csproj  -> rc=0, Ошибок: 0
dotnet build 6-msa/BC-Journal/src/Journal.Storage/Journal.Storage.csproj    -> rc=0, Ошибок: 0
```

Локальные правки после сборки **откачены** — в репозиторий идут только патчи (код `6-msa` — зона @BE).

## Применение

```bash
git apply 5-tech-backlog/tl/patches/migrations-designer-AddVersionToPartnersRead.patch
git apply 5-tech-backlog/tl/patches/migrations-designer-AddVersionToJournalRead.patch
dotnet build 6-msa/BC-Partners/src/Partners.Storage/Partners.Storage.csproj
dotnet build 6-msa/BC-Journal/src/Journal.Storage/Journal.Storage.csproj
```

## Критерий (обязательно на ПУСТОЙ БД)

Поднять чистый Postgres → `Database.Migrate()` → `GET /api/v1/partners` и `GET /api/v1/journal/records` → **200**. Это же должно поймать `DatabaseCreationTests` в CI (`BE-118`, критерий 2).

**Регламент:** патч передан @BE в 09:04. Если не применён за 1 час игрового времени — @TL применяет сам с пометкой.
