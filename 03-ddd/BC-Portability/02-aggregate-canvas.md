# Aggregate Design Canvas: CsvDocument (документ переноса)

> Bounded Context: Переносимость данных (Portability)
> Агрегат: CsvDocument (документ-агрегат, транзитный)

## 1. Name (Имя)
- **Имя:** CsvDocument (CSV-документ переноса).
- **Создаётся когда:** экспорт или импорт CSV.

## 2. Description (Описание)
Контекст сериализует/десериализует записи и соматику в CSV — «опубликованный язык» (Published Language) для обмена.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. Документ транзитный (формируется/читается разово).

**Сущность (Entity — корень агрегата):**
- **CsvDocument** — транзитный документ; идентичность неявная (экспорт/импорт-батч).

**Объект-значения (Value Objects):**
- **Headers**: фиксированный набор колонок (CSV_HEADERS)
- **Row**: строка записи (поля, списки через `;`)

## 3. State Transitions
```
(нет) → [Export] → (файл) → (нет)
(нет) → [Import] → (другие контексты) → (нет)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| HeaderContract | колонки фиксированы (CSV_HEADERS) |
| ListSeparator | списки полей через `;` |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| MergeById | импорт записей | upsert по id (не дублировать) |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `ExportRecords(records)` | Row[] |
| `ImportRecords(csv)` | Row[] |
| `ImportSomatic(csv)` | Row[] |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `ExportRecords` | `RecordsExported` |
| `ImportRecords` | `RecordsImported` |
| `ImportSomatic` | `SomaticImported` |

## 8. Throughput
- Редкие ручные операции.

## 9. Size
- Объём: весь журнал/соматика за раз.
