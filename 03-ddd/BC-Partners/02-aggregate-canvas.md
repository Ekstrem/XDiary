# Aggregate Design Canvas: Partner (Партнёр)

> Bounded Context: Партнёры (Partners)
> Агрегат: Partner
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** Partner (Партнёр).
- **Создаётся когда:** пользователь регистрирует партнёра.
- **Завершается когда:** удаление партнёра.

## 2. Description (Описание)
Агрегат Partner хранит профиль партнёра. Поставляет выбор партнёра и его пол в Журнал.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **Partner** — совокупность признаков, идентифицирующих партнёра:
  - `name` (string) — имя («кто»)

**Объект-значения (Value Objects):**
- **Photo**: photo (dataURL, 512px)
- **Gender**: gender («male» | «female»)
- **Notes**: notes (string)

## 3. State Transitions
```
(нет) → [Register] → Active
Active → [Rename / UpdateProfile] → Active
Active → [Remove] → Removed
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| NameRequired | name обязателен |
| GenderEnum | gender ∈ {male, female} |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSync | PartnerRenamed / PartnerRemoved | Журнал читает актуальное имя/пол |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Register(name)` | name |
| `Rename(name)` | name |
| `UpdateProfile(photo, gender, notes)` | Photo, Gender, Notes |
| `Remove()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Register` | `PartnerRegistered` |
| `Rename` | `PartnerRenamed` |
| `UpdateProfile` | `PartnerProfileUpdated` |
| `Remove` | `PartnerRemoved` |

## 8. Throughput
- Частота: единицы (редкое редактирование профилей).

## 9. Size
- Событий за жизнь: 1..несколько. Снапшоты не нужны.
