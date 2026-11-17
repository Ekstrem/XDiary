# Патч BE-125: `BC-Partners` не стартует (untyped `DbContextOptions`)

**Автор:** @TL, 2026-11-18 00:1x. **Зона применения:** `6-msa/BC-Partners` — @BE.

## Симптом (из CI, `deploy-dev` job `134362295021`)

```
Unhandled exception. Autofac.Core.DependencyResolutionException:
  ... activating DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext
  ---> System.InvalidOperationException: The DbContextOptions passed to the EventStoreDbContext constructor
       must be a DbContextOptions<EventStoreDbContext>
  at Program.<Main>$(String[] args) in /src/BC-Partners/src/Partners.Api/Program.cs:line 60
recreate-service: ОШИБКА: xdiary-partners не поднялся
```

## Правка (одна строка, как в `BC-Journal`)

В `6-msa/BC-Partners/src/Partners.Api/Program.cs` сразу после `AddDbContext<OutboxDbContext>(…)`:

```csharp
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp =>
    sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<
        DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
```

Почему именно там: до `builder.Build()` и до первого `GetRequiredService<EventStoreDbContext>()` (строка 60).

## Применение

```bash
git apply 5-tech-backlog/tl/patches/be125-partners-dbcontextoptions.patch
dotnet build 6-msa/BC-Partners/src/Partners.Api/Partners.Api.csproj
```

## Проверено @TL

`dotnet build …/Partners.Api.csproj` → **rc=0** (компилируется). Запуск в контейнере проверяется только выкаткой.
