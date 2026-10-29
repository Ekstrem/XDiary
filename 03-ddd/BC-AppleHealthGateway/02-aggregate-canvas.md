# Aggregate Design Canvas: AppleImport

> Bounded Context: Шлюз Apple Health
> Агрегат: AppleImport
> Профиль: Single-writer (интеграционное состояние)

## 1. Name (Имя)
- **AppleImport** — состояние импорта из Apple Health (файлы, последний импорт).

## 2. Description (Описание)
Агрегат владеет конфигурацией и состоянием импорта Apple Health.

**Value Objects:**
- **Root**: sourceId (string, «apple»)
- **ImportConfig**: metrics[]
- **LastImport**: timestamp
- **ProcessedFiles**: names[]

## 3. State Transitions
```
(нет) → [ImportFile] → Imported (обновляет LastImport)
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

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| ImportFile | handleImportAnyFile() | файл | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| AppleSomaticsImported | (наружу: точки/сводки) | ImportFile |

## 8. Throughput
- Редкий ручной импорт файла.

## 9. Size
- Один агрегат на источник; снапшоты не нужны.
