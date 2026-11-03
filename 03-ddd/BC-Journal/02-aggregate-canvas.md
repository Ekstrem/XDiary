# Aggregate Design Canvas: Record (Запись / Акт)

> Bounded Context: Журнал интимной активности (Journal)
> Агрегат: Record
> Профиль: Single-writer (пара, локальная PWA)

## 1. Name (Имя)
- **Имя:** Record (Запись / Акт).
- **Создаётся когда:** пользователь начинает фиксацию акта — **вручную** (визард) или **из импортированного занятия Garmin**.
- **Завершается когда:** удаление записи или полная очистка данных.

## 2. Description (Описание)
Агрегат защищает целостность одной фиксации акта. Хранится в IndexedDB `records`.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — `guid` идентифицирует агрегат, `long` — его версию. Технический, для **быстрого поиска**. **НЕ входит в сущность.**

**Сущность (Entity — корень агрегата):**
- **Record** — совокупность признаков, идентифицирующих запись (**кто и во сколько**):
  - `clientId` (uuid) — **владелец записи** (клиент-автор; берётся из JWT `sub`, НЕ из тела запроса)
  - `partners[]` (uuid[]) — «кто» (партнёры-участники; 0 = соло, N = несколько)
  - `startedAt` («YYYY-MM-DDTHH:mm») — «во сколько»

> **Владение и участие — разные вещи.** Владелец записи — **клиент-автор** (`clientId`); участники акта —
> «я» (владелец, неявный) + `partners[]`. **Партнёр — участник, а не владелец:** он не получает доступ к записи
> и не становится её автором (ADR-024; `03-ddd/01-entity-map.md`).
>
> **`clientId` обязателен для новых записей.** `NULL` допустим **только для легаси-строк**, созданных до BE-053:
> они не отдаются никому (`GET /journal` фильтрует по `sub`) и не «прилипают» к первому клиенту. Это состояние
> миграции, а не элемент модели — новые записи без владельца невозможны.

**Объект-значения (Value Objects):**
- **Duration**: durationMin (int) — длительность
- **Place**: place (string)
- **Positions**: positions[] (string)
- **Accessories**: costumes[], toys[], fantasies[], tags[]
- **Contraception**: contraceptives[] (string), contraceptiveCount (int)
- **Orgasms**: orgasms[] — по участнику: { participant («me» | partnerId), count (int), types[] (string), rating (int 0–5) }
- **Origin**: garmin (bool), garminId (string), createdAt, updatedAt

### Кардинальность полей (из US/CJM)
| Поле | Кардинальность | Источник |
|---|---|---|
| startedAt | 1 (обязательно) | US-01 |
| partners[] | 0..N (0 = соло) | US-02 «если есть» + «участников» |
| place | 0..1 (можно пропустить) | US-03 |
| positions[] | 0..N («одну или несколько») | US-04 |
| tags[] | 0..N | US-05 |
| durationMin | 1 | US-06 |
| orgasms[] | 0..N участников («кто и сколько») | US-07 |
| costumes[] | 0..N | KS-02 |
| toys[] | 0..N | IT-02 |
| fantasies[] | 0..N | FZ-03 |
| contraceptives[] + count | 0..N | CP-01 |

> **«Клиент» ≠ «Партнёр»** (ADR-024): клиент — аккаунт приложения (владелец записи, PIN, соматика), партнёр —
> участник акта (другой человек). Раньше эти понятия смешивались («партнёр-«я»») — это отменено: «я» в акте
> неявный автор, а не запись партнёра.

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
