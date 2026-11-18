# Патч-кандидат BE-128: перечитывать агрегат и при пустом `ClientId` в диффе

**Автор:** @TL, 2026-11-18 11:36. **Зона применения:** `6-msa/BC-Partners` — @BE.
**Статус:** кандидат — применять **только** если `psql` подтвердит ветвь «строка в `partners_read` есть, `ClientId` пуст».

## Почему

`PartnerEventConsumer.cs`, ветка создания read-модели: `clientId` берётся из **диффа проекции** (стр. 105), а полное состояние из event store подгружается только когда пусто **имя**:

```csharp
104|  var name = read.Name;
105|  var clientId = read.ClientId;
107|  if (read.Name is null)          // ← агрегат читаем только при пустом имени
109|      var agg = (await _events.GetById(domainEvent.Id, default)).LastOrDefault();
114|          clientId = agg.ClientId;
```

Для `Register` имя приходит ⇒ строка 107 ложна ⇒ `clientId` остаётся диффовым (может быть `null`), хотя в агрегате он есть (`Aggregate.Register` его проставляет, и `POST /partners` возвращает `clientId` в теле). Эффект: партнёр невидим в `GET /partners` (фильтр по `clientId`) и недоступен по owner-guard (`link-client` → 404).

## Правка (одно условие)

```csharp
if (read.Name is null || read.ClientId is null)
```

## Проверено @TL

`dotnet build 6-msa/BC-Partners/src/Partners.Api/Partners.Api.csproj` → **rc=0**.

## Когда применять

1. `SELECT "Id","ClientId" FROM partners_read ORDER BY "Id" DESC LIMIT 5;` → строки **есть**, `ClientId` **пуст** ⇒ патч по делу;
2. если `partners_read` **пуст** ⇒ патч не поможет: там «консьюмер не пишет», нужна отдельная диагностика (`DomainEvents`, логи `[handle] entered`);
3. критерий: `GET /partners` **содержит** созданного, `link-client` → **200**, спека `partner-client-link` зелёная, `smoke-api`/`smoke-front` зелёные.

**Регламент:** если @BE не берёт за 1 час игрового времени — применяет @TL с записью в карточке.
