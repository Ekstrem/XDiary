# DDD-артефакты (03) — правила моделирования агрегата

> Владелец: этот агент. Методология: **EDM (.NET) / DigiTFactory (DF)**.
> Эти правила обязательны для разработчика при написании доменного кода.

## Главное правило

Агрегат в DF моделируется так (см. статью «Применение DDD. Тактические паттерны»):

| Элемент | Что это | Где в канвасе |
|---|---|---|
| **Сущность (Entity)** | совокупность признаков, однозначно идентифицирующих запись — **БЕЗ технического id** | секция 2 |
| **Идентификатор агрегата** | составной ключ `<guid, long>` — технический, для **быстрого поиска**; НЕ входит в сущность | секция 2 |
| **Корень (Root)** | `I{Context}Root : IAggregateRoot<I{Context}>` (Id/Version даёт фреймворк) | секция 2 |
| **Состояние (AnemicModel)** | контейнер доменных VO (иммутабельные, фабричный метод `Create`) | секция 2 |
| **Методы** | именованные, отражают доменное действие/экран CJM (НЕ CRUD) | секция 6 |
| **Фабрики** | для разных источников создания (напр. ручная запись vs импорт Garmin) | секция 6 |
| **События** | каждый метод порождает доменное событие, **соразмерное названию метода** | секция 7 |

## Анти-паттерны (чего НЕ делать)

| ❌ Неправильно | ✅ Правильно |
|---|---|
| `id` как доменное поле сущности | id даёт фреймворк: `IAggregateRoot` (`<guid, long>`) |
| CRUD-методы `Create/Update/Delete` | именованные: `Register`, `SpecifyWhen`, `Complete` |
| одно общее событие `RecordUpdated` | каждый метод → своё событие: `WhenSpecified`, `PartnerChosen`, `RecordCompleted` |
| фабрика без учёта источника | две фабрики: `StartManualEntry()` / `StartFromGarmin(activity)` |
| корень без явного описания | явно: `I{Context}Root : IAggregateRoot<I{Context}>` |

## Эталонный пример — Record (Журнал)

**Идентификатор агрегата (DF):** `<guid, long>` — технический, для быстрого поиска.

**Сущность:** совокупность признаков, идентифицирующих запись (**кто и во сколько**):
- `partners[]` — «кто» (партнёры; 0..N, 0 = соло)
- `startedAt` — «во сколько»

**VO:** Duration, Place, Positions, Accessories, Contraception, Orgasms, Origin.

**Методы → события (каждый метод — своё событие):**
```
StartManualEntry()      → ManualEntryStarted
StartFromGarmin(activity) → GarminEntryStarted
SpecifyWhen(startedAt)  → WhenSpecified
AddPartner(partnerId)    → PartnerAdded
RemovePartner(partnerId) → PartnerRemoved
SpecifyPlace(place)     → PlaceSpecified
AddPositions(...)       → PositionsAdded
AddAccessories(...)     → AccessoriesAdded
RecordContraception(...) → ContraceptionRecorded
SpecifyDuration(...)    → DurationSpecified
RecordOrgasms(...)      → OrgasmsRecorded
Complete()              → RecordCompleted
Delete()                → RecordDeleted
```

## Терминология
- «Клиент» = «Партнёр»; в домене используем **«партнёр»**. Отдельный BC «Клиент» (комплаенс/безопасность) — позже.

## Статус
Все 9 агрегатов записи приведены к шаблону (Record, Partner, CatalogItem, SomaticPoint, DailyMetrics, GarminSync, AppleImport, PinCredential, CsvDocument). Аналитика — read-only, без агрегата.
