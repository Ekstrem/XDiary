# Aggregate Design Canvas: CatalogItem (Элемент каталога)

> Bounded Context: Каталог и инвентарь (Catalog)
> Агрегат: CatalogItem
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** CatalogItem (Элемент каталога).
- **Создаётся когда:** пользователь добавляет элемент каталога.
- **Завершается когда:** удаление элемента.

## 2. Description (Описание)
Агрегат CatalogItem хранит элемент одного вида (costume/toy/fantasy/contraception). Поставляет выбор в Журнал.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **CatalogItem** — совокупность признаков, идентифицирующих элемент:
  - `kind` («costume» | «toy» | «fantasy» | «contraception») — вид
  - `name` (string) — имя

**Объект-значения (Value Objects):**
- **Photo**: photo (dataURL)
- **Notes**: notes (string)
- **Brand** (только toy): brand (string)
- **Archived** (только toy): archived (bool)
- **FantasyPartner** (только fantasy): partner (string)

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`ICatalogItemAnemicModel` (`Root`, `Photo`, `Notes`, `Brand`, `Archived`, `FantasyPartner`) + `CatalogItemRoot` (`Kind`, `Name`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.Kind` | string | 1 | **да** | `Register(kind, name)` | словарь видов (оргазм/позиция/фантазия/место/контрацептив/таг) — неизвестный `kind` → 400 | US-004 |
| `Root.Name` | string | 1 | **да** | `Register`, `Rename` | непустое; в рамках `kind` без дублей (иначе выбор в визарде неоднозначен) | US-004 |
| `Photo` | string | 0..1 | нет | `Update` | `null`/пусто = «нет фото» | US-004 |
| `Notes` | string | 0..1 | нет | `Update` | свободный текст | US-004 |
| `Brand` | string | 0..1 | нет | `Update` | только для `kind=контрацептив`; для остальных бессмысленно (не валидируется — расхождение §2.2.2) | US-004 |
| `FantasyPartner` | string | 0..1 | нет | `Update` | требуемый для «фантазии» со вторым участником; словарь не задан (расхождение §2.2.2) | US-004 |
| `Archived` | bool | 1 | да (`false`) | `Archive`/`Restore` | архивный **не удаляется** и **не предлагается** в визарде, но остаётся разрешимым в старых записях | US-004 |

**2.2.2. Расхождения:** `Brand`/`FantasyPartner` не ограничены по `Kind` — можно записать бренд позиции; словарь видов нигде не
зафиксирован машинно (только в UI) → неизвестный `kind` проходит до хранилища.

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `Catalog.Add` |
| запись (команда) | `Catalog.Archive` |
| запись (команда) | `Catalog.UpdateDetails` |
| запись (команда) | `Catalog.Rename` |
| запись (команда) | `Catalog.Remove` |
| запись (команда) | `Catalog.Restore` |
| чтение | `Catalog.List` |
| чтение | `Catalog.Get` |

## 3. State Transitions
```
(нет) → [AddItem] → Active
Active → [Rename / UpdateDetails] → Active
Active → [Archive] → Archived
Archived → [Restore] → Active
Active/Archived → [Remove] → Removed
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| NameRequired | name обязателен |
| KindEnum | kind ∈ {costume, toy, fantasy, contraception} |
| BrandToyOnly | brand заполняется только для toy |
| ArchiveToyOnly | archived применяется только для toy |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSync | ItemRenamed / ItemRemoved | Журнал читает актуальные имена |
| ArchiveBottom | ItemArchived | список показывает элемент внизу, полупрозрачным |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `AddItem(kind, name)` | kind, name |
| `Rename(name)` | name |
| `UpdateDetails(photo, notes, brand, fantasyPartner)` | Photo, Notes, Brand, FantasyPartner |
| `Archive()` | Archived |
| `Restore()` | Archived |
| `Remove()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `AddItem` | `ItemAdded` |
| `Rename` | `ItemRenamed` |
| `UpdateDetails` | `ItemDetailsUpdated` |
| `Archive` | `ItemArchived` |
| `Restore` | `ItemRestored` |
| `Remove` | `ItemRemoved` |

## 8. Throughput
- Частота: единицы (редкое пополнение каталога).

## 9. Size
- Событий за жизнь: 1..несколько. Снапшоты не нужны.
