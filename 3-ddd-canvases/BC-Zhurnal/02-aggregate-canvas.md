# Aggregate Design Canvas: Record (Запись)

> Bounded Context: Журнал интимной активности
> Агрегат: Record
> Профиль: Single-writer (локальная PWA, одна вкладка)

## 1. Name (Имя)
- **Имя:** Record (Запись / Акт)
- **Создаётся когда:** пользователь проходит визард и жмёт «Сохранить» (saveWizard)
- **Завершается когда:** удаление записи (deleteFromWizard) или полная очистка (wipeAll)

## 2. Description (Описание)
Агрегат Record защищает целостность одной фиксации акта. Единственный агрегат контекста; хранится в IndexedDB `records`.

**Value Objects:**
- **Root** (RecordRoot): id (uuid), startedAt («YYYY-MM-DDTHH:mm»), durationMin (int)
- **PartnerRef**: partner (string — имя)
- **Place**: place (string)
- **Positions**: positions[] (string)
- **Accessories**: costumes[], toys[], fantasies[], tags[]
- **Contraception**: contraceptives[] (string), contraceptiveCount (int)
- **Orgasms**: orgasmsMe, orgasmsPartner (int); orgasmMeType[], orgasmPartnerType[] (string); orgasmMeRating, orgasmPartnerRating (int 0–5)
- **Origin**: garmin (bool), garminId (string), createdAt, updatedAt

## 3. State Transitions (Переходы состояний)
```
(нет) → [CreateRecord] → Created
Created → [UpdateRecord] → Created
Created → [DeleteRecord] → Deleted
(нет) → [ImportRecord] → Created (upsert по id)
```

## 4. Enforced Invariants (Обязательные инварианты)
| Инвариант | Описание | Источник |
|---|---|---|
| StartedAtRequired | startedAt обязателен | saveWizard (wizardStep=0) |
| DurationPositive | durationMin ≥ 1 | saveWizard (wizardStep=6) |
| RatingRange | orgasm*Rating ∈ 0..5 | UI (звёзды) |
| PartnerTouched | partnerTouched не даёт перезаписать партнёра авто-подстановкой | stepPartner |

## 5. Corrective Policies (Компенсирующие действия)
| Политика | Триггер | Действие |
|---|---|---|
| JournalRefresh | RecordCreated/Updated/Deleted | renderJournal() перерисовывает журнал |
| AnalyticsNotify | RecordCreated/Updated | событие «акт записан» → Аналитика (корреляции) |

## 6. Handled Commands (Обрабатываемые команды)
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| CreateRecord | saveWizard() | RecordDraft | UI |
| UpdateRecord | saveWizard() (editingId) | RecordDraft + id | UI |
| DeleteRecord | deleteFromWizard() | id | UI |
| ImportRecord | csvToRecords() → DB.putRecord | CSV-строка | UI |

## 7. Created Events (Порождаемые события)
| Событие | Изменённые VO | Когда |
|---|---|---|
| RecordCreated | все VO | CreateRecord |
| RecordUpdated | все VO | UpdateRecord |
| RecordDeleted | — | DeleteRecord |
| RecordImported | все VO | ImportRecord |

## 8. Throughput (Пропускная способность)
- Частота команд: единицы в день (одна пара).
- Конкурентность: нет (одна вкладка, IndexedDB).

## 9. Size (Размер)
- Событий за жизнь: 1..несколько (создание + редкие правки).
- Снапшоты: не нужны — текущее состояние хранится в `records`.
