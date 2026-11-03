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

**2.2.1. Как атрибут попадает в агрегат через API (AR-016: ссылки на `operationId`, без пересказа контракта).**
Пути, коды ответов, тела запросов здесь **не дублируются** — единственный источник: `04-architecture/api-index.yaml`
(правило: канвас описывает **модель**, контракт описывает **API**; расхождение лечится ссылкой, а не вторым текстом).

| Атрибут | `operationId` (запись) | Примечание |
|---|---|---|
| `state` | `Journal.Complete`, `Journal.Delete`; создание — `Journal.StartManualEntry` / `Journal.StartFromGarmin` | переходы см. §3 |
| `clientId` | — (не поле тела) | из JWT `sub` во всех командах/запросах, ADR-024 |
| `partners[]` | `Journal.AddPartner`, `Journal.RemovePartner` | участник ≠ владелец (§2.5) |
| `startedAt` | `Journal.SpecifyWhen`, `Journal.StartFromGarmin` | |
| `durationMin` | `Journal.SpecifyDuration` | |
| `place` | `Journal.SpecifyPlace` | |
| `positions[]` | `Journal.AddPositions` | |
| `tags[]`, `costumes[]`, `toys[]`, `fantasies[]` | `Journal.AddAccessories` | одна команда на все четыре набора |
| `contraceptives[]`, `contraceptiveCount` | `Journal.RecordContraception` | |
| `orgasms[]` | `Journal.RecordOrgasms` | |
| `garmin`, `garminId` | `Journal.StartFromGarmin` | |
| `createdAt`, `updatedAt` | — | ставит фреймворк, не API |
| `deletedAt` | `Journal.Delete` | вместе с `state=Deleted` |

**Чтение:** `Journal.List` (список), `Journal.Get` (по id), `Journal.Recent` (последняя запись),
`Journal.Places`, `Journal.Fantasies` (подсказки), `Journal.ListAll` (внутренний контур для Analytics).
Владение при чтении и записи: чужой `id` → 403, не найден → 404, без токена → 401 (код проверен, см. `verification`
у операций в индексе).

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

**2.6. Легаси-строки без владельца — ✅ ВЫПОЛНЕНО: зачищено (слово владельца).** Решение владельца
(2026-11-03 18:28 «историю можно смело чистить», 18:42 «зачищай!»): **бэкфилл не делаем, легаси-строки удаляем**.

**Факт исполнения на проде (@DO, 2026-11-11 12:10, задача `DO-057`):**
1. **Бэкап:** `pg_dump -Fc` → `journal-20261103T154616Z.dump`, **4 427 байт**, `sha256[:16]` зафиксирован,
   дамп проверен `pg_restore -l` (TABLE DATA `records`);
2. **До:** всего **7** строк, из них без владельца **6** (все уже `Deleted = true`), с владельцем **1**;
3. **Удаление:** `DELETE FROM records WHERE "ClientId" IS NULL` → **DELETE 6** (удалённых и безвладельцев — только они);
4. **После:** всего **2**, без владельца **0**, с владельцем **2**; новая запись видна только владельцу
   (список 1, своя → **200**, аноним → **401**, чужая → **403**).

> **Уточнение по масштабу (важно, чтобы не пугать себя цифрой):** **2394 строки** без владельца — это **dev**,
> тестовый контур; отдельного решения на них не нужно. На **проде** история была 7 строк, потеря — 6 **уже удалённых**.
> То есть «журнал опустеет» на проде означало бы «пропали 6 удалённых строк», а не «вся история».

> Это тот же класс, что G1 в `04-architecture/edm-debt-plan.md` («переход без миграции данных оставляет историю
> за стеклом»), но решение обратное: там историю переносили, здесь — **сознательно начинаем с чистого листа**.
> Важно, что это **записанное решение владельца**, а не побочный эффект включения приватности.

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
