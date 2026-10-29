# Bounded Context Map: XDiary

## Контексты

| # | Контекст | Классификация | Роль |
|---|---|---|---|
| 1 | Журнал интимной активности (Activity Journal) | Core Domain | Execution |
| 2 | Партнёры (Partners) | Supporting | Execution |
| 3 | Каталог и инвентарь (Catalog & Inventory) | Supporting | Execution |
| 4 | Соматика — минутные точки (SomaticSeries) | Supporting | Execution |
| 5 | Дневные метрики (DailyMetrics) | Supporting | Execution |
| 6 | Шлюз Garmin (GarminGateway) | Supporting | Gateway |
| 7 | Шлюз Apple Health (AppleHealthGateway) | Supporting | Gateway |
| 8 | Аналитика (Analytics) | Supporting | Analysis |
| 9 | Приватность и доступ (Privacy & Access) | Generic | Execution |
| 10 | Переносимость данных (Data Portability) | Generic | Execution |

## Диаграмма

```mermaid
flowchart TD
    PR["Приватность и доступ<br/>(PIN, блокировка)"] -->|gatekeeper| J["Журнал интимной активности"]
    P["Партнёры"] -->|Customer/Supplier| J
    C["Каталог и инвентарь"] -->|Customer/Supplier| J
    J -->|Customer/Supplier| SS["Соматика (минутные точки)"]
    J -->|событие «акт записан»| A["Аналитика"]
    DM["Дневные метрики"] -->|Customer/Supplier| A
    GGW["Шлюз Garmin"] -->|Customer/Supplier| SS
    GGW -->|Customer/Supplier| DM
    GGW -->|занятия (автоперенос)| J
    AGW["Шлюз Apple Health"] -->|Customer/Supplier| SS
    AGW -->|Customer/Supplier| DM
    GGW -->|ACL| G["Garmin<br/>(внешний)"]
    AGW -->|ACL| AH["Apple Health<br/>(внешний)"]
    DP["Переносимость данных"] -->|CSV| J
    DP -->|CSV| SS
    DP -->|CSV| DM
    DP -->|CSV| C
```

## Связи

| Downstream (потребитель) | Upstream (поставщик) | Тип отношения | Механизм |
|---|---|---|---|
| Журнал | Партнёры | Customer/Supplier | IndexedDB `partners` |
| Журнал | Каталог | Customer/Supplier | IndexedDB `catalog` |
| Журнал | Соматика (минутные точки) | Customer/Supplier | срез соматики вокруг `startedAt` |
| Журнал | Шлюз Garmin | Customer/Supplier | занятия (кандидаты в акты) |
| Аналитика | Журнал | Customer/Supplier | событие «акт записан» (записи) |
| Аналитика | Дневные метрики | Customer/Supplier | дневные тренды (стресс/пульс/сон/HRV) |
| Соматика (минутные точки) | Шлюз Garmin | Customer/Supplier | нормализованные точки |
| Соматика (минутные точки) | Шлюз Apple Health | Customer/Supplier | нормализованные точки |
| Дневные метрики | Шлюз Garmin | Customer/Supplier | нормализованные дневные сводки |
| Дневные метрики | Шлюз Apple Health | Customer/Supplier | нормализованные дневные сводки |
| Шлюз Garmin | Garmin (внешний) | Anti-Corruption Layer | Personal API (прокси/CSV) |
| Шлюз Apple Health | Apple Health (внешний) | Anti-Corruption Layer | ручной экспорт `export.xml`/CSV |
| Перенос данных | Журнал / Соматика / Дневные метрики / Каталог | Published Language | CSV (общий формат) |
| Все контексты | Приватность и доступ | Gatekeeper | PIN (сквозной) |

## Shared Kernel (общее ядро)

- Идентичность акта: `record.id`, `record.startedAt` — общее для Журнала, Соматики, Дневных метрик, Шлюза Garmin и Аналитики.
- Единый формат соматики (точка `t`, дневная сводка `date`) — общее для шлюзов и контекстов соматики.
- Справочник пола (`male`/`female`) и типов оргазмов — общий для Журнала, Партнёров и Каталога.

## Легенда

- **Customer/Supplier** — поставщик задаёт формат, потребитель подстраивается.
- **Anti-Corruption Layer** — изоляция внешней модели производителя (Garmin без CORS; Apple Health через ручной экспорт).
- **Published Language** — публикуемый формат обмена (CSV).
- **Gatekeeper** — сквозной контроль доступа перед входом в приложение.

## Принцип «1 агрегат = 1 контекст»
Каждый контекст владеет ровно одним агрегатом записи: Record, Partner, CatalogItem, SomaticPoint, DailyMetrics, GarminSync, AppleImport, PinCredential, CsvDocument. Аналитика — read-only проекционный контекст (без агрегата записи).
