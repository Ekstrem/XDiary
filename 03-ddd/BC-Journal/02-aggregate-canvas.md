# Aggregate Design Canvas: Record (Запись / Акт)

> Bounded Context: Журнал интимной активности (Journal)
> Агрегат: Record
> Профиль: Single-writer (пара, локальная PWA)

## 1. Name (Имя)
- **Имя:** Record (Запись / Акт).
- **Создаётся когда:** пользователь начинает фиксацию акта — **вручную** (визард) или **из импортированного занятия Garmin**.
- **Завершается когда:** удаление записи или полная очистка данных.

## 2. Description (Описание)
Агрегат защищает целостность одной фиксации акта. Read-model: IndexedDB `records` (после E1 — MV).

**2.1. Идентификатор и корень (DF):**
- Составной ключ `<guid, long>`: `guid` идентифицирует агрегат, `long` — версия. Даёт фреймворк
  (`IAggregateRoot`), **доменным полем не является** — поэтому в таблице ниже его нет.
- Корень: `IRecordRoot : IAggregateRoot<IRecord>`. Состояние — `AnemicModel` (контейнер VO, фабричный `Create`).

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата.** Это и есть ответ на «какие поля у агрегата»:

| Атрибут | Тип | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `state` | enum `Draft` \| `Recorded` \| `Deleted` | 1 | да | фабрики → `Complete()` → `Delete()` | см. §3 (переходы) | — |
| `clientId` | uuid | 1 (новые) | да | из JWT `sub`, **не из тела** | RecordOwnedByClient, AuthorFromToken | ADR-024 |
| `partners[]` | uuid[] | 0..N | нет (0 = соло) | `AddPartner` / `RemovePartner` | PartnersCardinality, PartnersAreParticipants | US-02 |
| `startedAt` | datetime (`YYYY-MM-DDTHH:mm`) | 1 | да (до `Complete`) | `SpecifyWhen` / `StartFromGarmin` | WhenRequired | US-01 |
| `durationMin` | int | 1 | да (до `Complete`) | `SpecifyDuration` | DurationPositive (≥ 1) | US-06 |
| `place` | string? | 0..1 | нет | `SpecifyPlace` | — | US-03 |
| `positions[]` | string[] | 0..N | нет | `AddPositions` | — | US-04 |
| `tags[]` | string[] | 0..N | нет | `AddAccessories` | — | US-05 |
| `costumes[]` | string[] | 0..N | нет | `AddAccessories` | — | KS-02 |
| `toys[]` | string[] | 0..N | нет | `AddAccessories` | — | IT-02 |
| `fantasies[]` | string[] | 0..N | нет | `AddAccessories` | — | FZ-03 |
| `contraceptives[]` | string[] | 0..N | нет | `RecordContraception` | — | CP-01 |
| `contraceptiveCount` | int | 1 (если контрацепция указана) | нет | `RecordContraception` | ≥ 0 | CP-01 |
| `orgasms[]` | `{participant, count, types[], rating}` | 0..N | нет | `RecordOrgasms` | RatingRange (0..5), participant уникален | US-07 |
| `garmin` | bool | 1 | да (default `false`) | `StartFromGarmin` | GarminPrefilled | инк. 3 |
| `garminId` | string? | 0..1 | нет | `StartFromGarmin` | GarminPrefilled | инк. 3 |
| `createdAt` | datetime | 1 | да | фреймворк (при создании) | — | — |
| `updatedAt` | datetime | 1 | да | фреймворк (каждая запись) | — | — |
| `deletedAt` | datetime? | 0..1 | нет | `Delete()` | ставится вместе с `state=Deleted` | — |

**2.3. Объекты-значения (VO):** атрибуты выше — поля `AnemicModel`; VO группируют их для методов и валидации
(каждый — иммутабельный, фабрика `Create`):
`Duration(durationMin)` · `Place(place)` · `Positions(positions[])` · `Accessories(costumes[], toys[], fantasies[], tags[])` ·
`Contraception(contraceptives[], contraceptiveCount)` · `Orgasms(orgasms[])` · `When(startedAt)` · `Origin(garmin, garminId)`.

**2.4. Что НЕ хранится в агрегате (и почему):**
- **Имя/пол партнёра** — живут в `Partner` (иначе одно имя в двух местах и расходится, ADR-024);
- **Список участников отдельным полем** — он **производный**: «я» (неявно) + `partners[]`; дублировать нельзя;
- **Автор отдельным полем** — автор и есть `clientId`;
- **Аналитика (сводки/тренды)** — MV в `BC-Analytics`, не состояние записи;
- **Соматика** — `SomaticSeries`/`DailyMetrics`, ссылки по времени, не встраиваются.

**2.5. Владение и участие.** Владелец записи — **клиент-автор** (`clientId`); участники акта — «я» (неявный)
+ `partners[]`. **Партнёр — участник, не владелец:** доступа к записи не получает (ADR-024; `01-entity-map.md`).

**2.6. Легаси-строки без владельца — обязательный бэкфилл до релиза.** Замер @DO (2026-11-11, dev):
**2391 запись, у всех `ClientId = NULL`** (из них **1199 `Deleted = true`**). Это не «безобидные старые строки»:
после включения скоупа по `clientId` **владелец увидит пустой журнал** и **403 на свою же старую запись** — то есть
история исчезнет не физически, а из выдачи. `NULL` не «не отдаётся никому» как решение, а **состояние до бэкфилла**.

**Требование (до выкатки):**
1. Одноразовый бэкфилл `ClientId` для существующих строк с **предохранителем**: выполнять только если в БД
   **ровно один клиент** (иначе — стоп и решение владельца, кому отдавать историю);
2. `Deleted = true` (1199) — **не воскрешать**: они остаются невидимыми в любом случае;
3. **Dry-run + бэкап** перед `UPDATE`, затем сверка: «записей с `ClientId` = 2391 − 0» и «владелец видит свои»;
4. Если бэкфилл не делаем — это **решение владельца данных**, записанное, а не молчаливый побочный эффект.

> Это тот же класс, что G1 в `04-architecture/edm-debt-plan.md`: переход на новый скоуп без миграции данных
> оставляет историю «за стеклом». Отличие: там — E1 (события), здесь — скоуп по владельцу.

## 3. State Transitions (Переходы состояний)
```
(нет) → [StartManualEntry] → Draft ─┐
(нет) → [StartFromGarmin] → Draft ─┤→ [шаги визарда] → Draft → [Complete] → Recorded
Recorded → [Delete] → Deleted
```

## 4. Enforced Invariants (Обязательные инварианты)
| Инвариант | Описание |
|---|---|
| RecordOwnedByClient | у новой записи `clientId` = клиент-автор из JWT `sub`; `NULL` — только легаси-строки (не отдаются) |
| **ClientIdMustBePersisted** | **фабрика обязана присвоить `ClientId`**: сигнатура `StartManualEntry(clientId)` / `StartFromGarmin(…, clientId)` без присваивания = параметр, который «приняли и выбросили» (дефект 2026-11-10: модель, маппер и фильтр чтения готовы, а запись создавалась с `ClientId = null` → пустой журнал и 403 на свою запись). **Тест агрегата обязан проверять `Model().ClientId == clientId`**, а не только успех вызова — иначе такой разрыв проходит сквозь юнит-тесты |
| AuthorFromToken | `clientId` берётся из токена; попытка задать владельца телом запроса игнорируется/отклоняется |
| PartnersAreParticipants | `partners[]` — участники акта; владение записью они не получают |
| PartnersCardinality | partners[] — 0..N (0 = соло, без партнёра) |
| WhenRequired | startedAt обязателен до `Complete` |
| DurationPositive | durationMin ≥ 1 |
| RatingRange | rating ∈ 0..5 |
| GarminPrefilled | `StartFromGarmin` предзаполняет startedAt / durationMin / garminId |

## 5. Corrective Policies (Компенсирующие действия)
| Политика | Триггер | Действие |
|---|---|---|
| JournalRefresh | любое событие записи | перерисовать журнал |
| AnalyticsNotify | RecordCompleted | событие «акт записан» → Аналитика |

## 6. Handled Commands (методы агрегата — по экранам визарда/CJM)
| Метод (код) | Экран (CJM) | Входные VO |
|---|---|---|
| `StartManualEntry()` | фабрика — ручная запись | — |
| `StartFromGarmin(activity)` | фабрика — импорт занятия Garmin | GarminActivity |
| `SpecifyWhen(startedAt)` | «Дата» | When |
| `AddPartner(partnerId)` | «Партнёр» | partnerId |
| `RemovePartner(partnerId)` | «Партнёр» | partnerId |
| `SpecifyPlace(place)` | «Место» | Place |
| `AddPositions(positions)` | «Позы» | Positions |
| `AddAccessories(costumes, toys, fantasies, tags)` | «Аксессуары» | Accessories |
| `RecordContraception(contraceptives, count)` | «Контрацепция» | Contraception |
| `SpecifyDuration(durationMin)` | «Длительность» | Duration |
| `RecordOrgasms(orgasms[])` | «Оргазмы» | Orgasms |
| `Complete()` | «Итог» | — |
| `Delete()` | — | — |

## 7. Created Events (Порождаемые события)
Каждый метод порождает доменное событие, **соразмерное названию метода** (через нотификатор доменных сообщений DF):

| Метод | Событие |
|---|---|
| `StartManualEntry` | `ManualEntryStarted` |
| `StartFromGarmin` | `GarminEntryStarted` |
| `SpecifyWhen` | `WhenSpecified` |
| `AddPartner` | `PartnerAdded` |
| `RemovePartner` | `PartnerRemoved` |
| `SpecifyPlace` | `PlaceSpecified` |
| `AddPositions` | `PositionsAdded` |
| `AddAccessories` | `AccessoriesAdded` |
| `RecordContraception` | `ContraceptionRecorded` |
| `SpecifyDuration` | `DurationSpecified` |
| `RecordOrgasms` | `OrgasmsRecorded` |
| `Complete` | `RecordCompleted` |
| `Delete` | `RecordDeleted` |

## 8. Throughput (Пропускная способность)
- Частота: единицы в день (одна пара).

## 9. Size (Размер)
- Событий за жизнь: 1..N (по одному на каждый вызванный метод).
- Снапшоты: не нужны.
- **Где текущее состояние:** сейчас — таблица `records` (state-персистентность); после перехода на Event Sourcing
  (E1 из `04-architecture/edm-debt-plan.md`) состояние восстанавливается **из событий**, а `records` станет read-model.
