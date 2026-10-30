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
