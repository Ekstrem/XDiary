# Aggregate Design Canvas: CsvDocument (документ переноса)

> Bounded Context: Переносимость данных
> Агрегат: CsvDocument (сервис-контекст, агрегат-документ)

## 1. Name (Имя)
- **Имя:** CsvDocument (CSV-документ переноса)
- **Создаётся когда:** экспорт (downloadCsv) или импорт (importCsvFile)

## 2. Description (Описание)
Контекст сериализует/десериализует записи и соматику в CSV. Это «опубликованный язык» (Published Language) для обмена с внешним миром.

**Value Objects:**
- **Headers** (CSV_HEADERS): фиксированный набор колонок.
- **Row**: строка записи (поля, списки через `;`).

## 3. State Transitions
```
(нет) — документ формируется/читается разово.
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| HeaderContract | колонки фиксированы (CSV_HEADERS) |
| ListSeparator | списки полей через `;` |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| MergeById | импорт | upsert записей по id (не дублировать) |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| ExportRecords | downloadCsv() | записи | UI |
| ImportRecords | importCsvFile() | CSV | UI |
| ImportSomatic | handleImportAnyFile() | CSV (соматика/сон/…) | UI |

## 7. Created Events
- Нет (пишет в другие контексты через их API).

## 8. Throughput
- Редкие ручные операции.

## 9. Size
- Объём файлов: весь журнал/соматика.
