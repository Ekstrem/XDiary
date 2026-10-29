# Aggregate Design Canvas: Partner (Партнёр)

> Bounded Context: Партнёры
> Агрегат: Partner
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** Partner (Партнёр)
- **Создаётся когда:** пользователь добавляет партнёра (savePartnerEdit, новый)
- **Завершается когда:** удаление партнёра (deletePartnerEdit)

## 2. Description (Описание)
Агрегат Partner хранит профиль партнёра. Поставляет выбор партнёра и его пол в Журнал.

**Value Objects:**
- **Root** (PartnerRoot): id (uuid), name (string)
- **Photo**: photo (dataURL, 512px)
- **Gender**: gender («male» | «female»)
- **Notes**: notes (string)

## 3. State Transitions
```
(нет) → [CreatePartner] → Active
Active → [UpdatePartner] → Active
Active → [DeletePartner] → Deleted
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| NameRequired | name обязателен (savePartnerEdit) |
| GenderEnum | gender ∈ {male, female} |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSync | PartnerUpdated/Deleted | Журнал читает актуальное имя/пол при следующем выборе |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| CreatePartner | savePartnerEdit() | PartnerDraft | UI |
| UpdatePartner | savePartnerEdit() (editingId) | PartnerDraft + id | UI |
| DeletePartner | deletePartnerEdit() | id | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| PartnerCreated | все | CreatePartner |
| PartnerUpdated | все | UpdatePartner |
| PartnerDeleted | — | DeletePartner |

## 8. Throughput
- Частота: единицы (редкое редактирование профилей).
- Конкурентность: нет.

## 9. Size
- Событий за жизнь: 1..несколько.
- Снапшоты: не нужны.
