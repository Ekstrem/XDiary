# Aggregate Design Canvas: SomaticPoint

> Bounded Context: Соматика — минутные точки
> Агрегат: SomaticPoint
> Профиль: Append-only (импорт), Single-writer

## 1. Name (Имя)
- **SomaticPoint** — одно значение за минуту (t = «YYYY-MM-DDTHH:mm»).
- **Создаётся когда:** импорт соматики (синк/CSV).
- **Завершается когда:** полная очистка данных.

## 2. Description (Описание)
Минутная точка физиологии. Хранится в IndexedDB `somatic`.

**Value Objects:**
- **Root**: t (string, минута)
- **HeartRate**: hr (int)
- **BodyBattery**: body_battery (int)

## 3. State Transitions
```
(нет) → [ImportSomatic] → Stored
Stored → [ReimportSomatic] → Stored (upsert по t)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| MinuteGranularity | t имеет формат «YYYY-MM-DDTHH:mm» |
| NonNegative | hr / body_battery ≥ 0 |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSliceRefresh | новые точки | Журнал пересчитывает срез вокруг акта |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| SyncSomatic | garminSync() → DB.putSomaticBatch | точки (t, hr, bb) | UI/автосинк |
| ImportCsv | handleImportAnyFile() | CSV (somatic) | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| SomaticImported | SomaticPoint | SyncSomatic/ImportCsv |

## 8. Throughput
- Частота: пакетная (тысячи точек за синк), редкие вызовы.

## 9. Size
- Объём: месяцы данных (~23k точек/месяц).
- Снапшоты: не нужны (append-only в IndexedDB).
