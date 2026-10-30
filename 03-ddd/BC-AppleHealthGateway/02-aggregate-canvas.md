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
- **AppleImport** — совокупность признаков, идентифицирующих источник:
  - `sourceId` (string) — «apple»

**Объект-значения (Value Objects):**
- **ImportConfig**: metrics[]
- **LastImport**: timestamp
- **ProcessedFiles**: names[]

## 3. State Transitions
```
(нет) → [Configure] → Configured
Configured → [ImportFile] → Configured (обновляет LastImport)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| SingleSource | один экземпляр на источник |
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
