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

**Aggregate Root (корень агрегата):**
- **`Record`** — корень агрегата: `IJournalRoot : IAggregateRoot<IJournal>`.
- **Id / Version** предоставляет фреймворк EDM (`IAggregateRoot`); в Value Objects домена их **не** описываем.

**State (AnemicModel — Value Objects домена):**
- **When**: startedAt («YYYY-MM-DDTHH:mm»)
- **PartnerRef**: partner (string — имя)
- **Place**: place (string)
- **Positions**: positions[] (string)
- **Accessories**: costumes[], toys[], fantasies[], tags[]
- **Contraception**: contraceptives[] (string), contraceptiveCount (int)
- **Duration**: durationMin (int)
- **Orgasms**: orgasmsMe, orgasmsPartner (int); orgasmMeType[], orgasmPartnerType[] (string); orgasmMeRating, orgasmPartnerRating (int 0–5)
- **Origin**: garmin (bool), garminId (string), createdAt, updatedAt

## 3. State Transitions (Переходы состояний)
```
(нет) → [StartManualEntry] → Draft ─┐
(нет) → [StartFromGarmin] → Draft ─┤→ [шаги визарда] → Draft → [Complete] → Recorded
Recorded → [Delete] → Deleted
```

## 4. Enforced Invariants (Обязательные инварианты)
| Инвариант | Описание |
|---|---|
| WhenRequired | startedAt обязателен до `Complete` |
| DurationPositive | durationMin ≥ 1 |
| RatingRange | rating ∈ 0..5 |
| GarminPrefilled | `StartFromGarmin` предзаполняет startedAt / durationMin / garminId |

## 5. Corrective Policies (Компенсирующие действия)
| Политика | Триггер | Действие |
|---|---|---|
| JournalRefresh | RecordRecorded / RecordDeleted | перерисовать журнал |
| AnalyticsNotify | RecordRecorded | событие «акт записан» → Аналитика |

## 6. Handled Commands (методы агрегата — по экранам визарда/CJM)
| Метод (код) | Экран (CJM) | Входные VO |
|---|---|---|
| `StartManualEntry()` | фабрика — ручная запись | — |
| `StartFromGarmin(activity)` | фабрика — импорт занятия Garmin | GarminActivity |
| `SpecifyWhen(startedAt)` | «Дата» | When |
| `ChoosePartner(partner)` | «Партнёр» | PartnerRef |
| `SpecifyPlace(place)` | «Место» | Place |
| `AddPositions(positions)` | «Позы» | Positions |
| `AddAccessories(costumes, toys, fantasies, tags)` | «Аксессуары» | Accessories |
| `RecordContraception(contraceptives, count)` | «Контрацепция» | Contraception |
| `SpecifyDuration(durationMin)` | «Длительность» | Duration |
| `RecordOrgasms(me, partner, meTypes, partnerTypes, meRating, partnerRating)` | «Оргазмы» | Orgasms |
| `Complete()` | «Итог» | — |
| `Delete()` | — | — |

## 7. Created Events (Порождаемые события)
| Событие | Когда |
|---|---|
| `RecordDraftStarted` | StartManualEntry / StartFromGarmin |
| `RecordRecorded` | Complete |
| `RecordDeleted` | Delete |

## 8. Throughput (Пропускная способность)
- Частота: единицы в день (одна пара).

## 9. Size (Размер)
- Событий за жизнь: несколько (старт + финализация + редкие правки).
- Снапшоты: не нужны — текущее состояние в `records`.
