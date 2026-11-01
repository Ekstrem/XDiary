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

## Кардинальность полей
Перед моделированием агрегата — выпиши кардинальность каждого поля из US/CJM: **1 (обязательно) / 0..1 (опционально) / 0..N (список)**. Ошибки случаются именно там, где «может не быть» или «может быть несколько» (партнёры 0..N, игрушки 0..N, оргазмы по участникам). Пример — кардинальность Record в `BC-Journal/02-aggregate-canvas.md`.

## Command API / Query API (HTTP)
- **Команды — только POST**: `POST /api/v1/{context}` (создание/старт), `POST /api/v1/{context}/{id}/{action}` (действие).
- **Без PUT/DELETE** (идемпотентные операции; статья «Тактические паттерны» запрещает PUT/DELETE для модификации агрегата). «Удаление» — `POST .../delete` / `.../remove`.
- **Query API — только GET** (read-only, из ReadModel).

## MV vs клиент (граница вычислений)
- **MV (сервер, прекомпьют от событий):** бизнес-агрегаты по всему датасету — summary, trends, correlations, tops, distribution, recent-дефолты, distinct-подсказки.
- **Клиент (display):** чистое форматирование **уже полученных** данных — avg/max/min подписи над графиком по точкам среза, масштаб оси. НЕ агрегация по датасету, НЕ скан записей.

## Анти-паттерны (чего НЕ делать)

| ❌ Неправильно | ✅ Правильно |
|---|---|
| `id` как доменное поле сущности | id даёт фреймворк: `IAggregateRoot` (`<guid, long>`) |
| CRUD-методы `Create/Update/Delete` | именованные: `Register`, `SpecifyWhen`, `Complete` |
| одно общее событие `RecordUpdated` | каждый метод → своё событие: `WhenSpecified`, `PartnerChosen`, `RecordCompleted` |
| фабрика без учёта источника | две фабрики: `StartManualEntry()` / `StartFromGarmin(activity)` |
| корень без явного описания | явно: `I{Context}Root : IAggregateRoot<I{Context}>` |
| `PUT`/`DELETE` в Command API | только `POST` (действие в URL); `DELETE` → `POST .../delete` |

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
