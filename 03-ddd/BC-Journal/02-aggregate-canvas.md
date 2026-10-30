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
  - `partners[]` (uuid[]) — «кто» (партнёры; 0 = соло, N = несколько)
  - `startedAt` («YYYY-MM-DDTHH:mm») — «во сколько»

> Участники акта = «я» (владелец, неявный) + `partners[]`.

**Объект-значения (Value Objects):**
- **Duration**: durationMin (int) — длительность
- **Place**: place (string)
- **Positions**: positions[] (string)
- **Accessories**: costumes[], toys[], fantasies[], tags[]
- **Contraception**: contraceptives[] (string), contraceptiveCount (int)
- **Orgasms**: orgasms[] — по участнику: { participant («me» | partnerId), count (int), types[] (string), rating (int 0–5) }
- **Origin**: garmin (bool), garminId (string), createdAt, updatedAt

> «Клиент» = «Партнёр»; в домене используем **«партнёр»**. Отдельный BC «Клиент» (комплаенс/безопасность) — позже.

## 3. State Transitions (Переходы состояний)
```
(нет) → [StartManualEntry] → Draft ─┐
(нет) → [StartFromGarmin] → Draft ─┤→ [шаги визарда] → Draft → [Complete] → Recorded
Recorded → [Delete] → Deleted
```

## 4. Enforced Invariants (Обязательные инварианты)
| Инвариант | Описание |
|---|---|
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
- Снапшоты: не нужны — текущее состояние в `records`.
