# Aggregate Design Canvas: AppleImport

> Bounded Context: Шлюз Apple Health (AppleHealthGateway)
> Агрегат: AppleImport
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **AppleImport** — состояние импорта из Apple Health (файлы, последний импорт).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием импорта Apple Health.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **AppleImport** — совокупность признаков, идентифицирующих источник у конкретного клиента:
  - `clientId` (uuid) — чей Apple Health (клиент, ADR-021)
  - `sourceId` (string) — «apple»

**Объект-значения (Value Objects):**
- **ImportConfig**: metrics[]
- **LastImport**: timestamp
- **ProcessedFiles**: names[]

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`IAppleImportAnemicModel` (`Root`, `ImportConfig`, `LastImport`, `ProcessedFiles`) + `AppleImportRoot` (`ClientId`, `SourceId`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.ClientId` | Guid | 1 | **да** | `Connect(clientId)` | скоуп владельца | ADR-008 |
| `Root.SourceId` | string | 1 | **да** | `Connect` | идентификатор источника (устройство/экспорт) | US-008 |
| `ImportConfig` | `IImportConfig` (VO) | 1 | да | `Configure` | какие типы данных импортировать | US-008 |
| `LastImport` | string (timestamp) | 0..1 | нет | `Import` | курсор инкремента, монотонный | US-008 |
| `ProcessedFiles` | `IReadOnlyList<string>` | 0..* | нет | `Import` | список уже обработанных файлов — **основа идемпотентности**: повторный импорт того же файла не должен дублировать точки | US-008 |

**2.2.2. Расхождения:** `ProcessedFiles` растёт **без ограничения** (`IReadOnlyList`, ни обрезки, ни TTL) — на длинной
истории это неограниченный рост состояния агрегата и снапшотов; нужен лимит/вытеснение (иначе через год импорт будет
тянуть мегабайты истории файлов).

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `AppleHealthGateway.Configure` |
| запись (команда) | `AppleHealthGateway.ImportFile` |
| чтение | `AppleHealthGateway.State` |

## 3. State Transitions
```
(нет) → [Configure] → Configured
Configured → [ImportFile] → Configured (обновляет LastImport)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| SingleSourcePerClient | один экземпляр на (клиент, источник) |
| IdempotentFile | файл не импортируется дважды (по имени) |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| SkipDuplicate | файл уже импортирован | пропустить |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Configure(metrics)` | ImportConfig |
| `ImportFile(file)` | файл |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Configure` | `AppleImportConfigured` |
| `ImportFile` | `AppleFileImported` |

## 8. Throughput
- Редкий ручной импорт файла.

## 9. Size
- Один агрегат на источник. Снапшоты не нужны.
