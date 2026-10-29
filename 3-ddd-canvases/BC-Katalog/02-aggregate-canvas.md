# Aggregate Design Canvas: CatalogItem (Элемент каталога)

> Bounded Context: Каталог и инвентарь
> Агрегат: CatalogItem
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** CatalogItem (Элемент каталога)
- **Создаётся когда:** пользователь добавляет элемент (saveCatalogEdit, новый)
- **Завершается когда:** удаление элемента (deleteCatalogEdit)

## 2. Description (Описание)
Агрегат CatalogItem хранит элемент одного вида (costume/toy/fantasy/contraception). Поставляет выбор в Журнал.

**Value Objects:**
- **Root** (ItemRoot): id (uuid), kind («costume»|«toy»|«fantasy»|«contraception»), name (string)
- **Photo**: photo (dataURL)
- **Notes**: notes (string)
- **Brand** (только toy): brand (string)
- **Archive** (только toy): archived (bool)
- **FantasyPartner** (только fantasy): partner (string)

## 3. State Transitions
```
(нет) → [CreateItem] → Active
Active → [UpdateItem] → Active
Active → [ArchiveItem] → Archived
Archived → [RestoreItem] → Active
Active/Archived → [DeleteItem] → Deleted
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| NameRequired | name обязателен (saveCatalogEdit) |
| KindEnum | kind ∈ {costume, toy, fantasy, contraception} |
| BrandToyOnly | brand заполняется только для toy |
| ArchiveToyOnly | archived применяется только для toy |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSync | ItemUpdated/Deleted | Журнал читает актуальные имена при следующем выборе |
| ArchiveBottom | archived=true | список показывает элемент внизу, полупрозрачным |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| CreateItem | saveCatalogEdit() | ItemDraft | UI |
| UpdateItem | saveCatalogEdit() (editingCatalogId) | ItemDraft + id | UI |
| DeleteItem | deleteCatalogEdit() | id | UI |
| ArchiveItem | ce-archive toggle → saveCatalogEdit() | archived | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| ItemCreated | все | CreateItem |
| ItemUpdated | все | UpdateItem |
| ItemDeleted | — | DeleteItem |

## 8. Throughput
- Частота: единицы (редкое пополнение каталога).
- Конкурентность: нет.

## 9. Size
- Событий за жизнь: 1..несколько.
- Снапшоты: не нужны.
