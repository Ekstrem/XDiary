# Aggregate Design Canvas: SomaticPoint

> Bounded Context: Соматика — минутные точки (SomaticSeries)
> Агрегат: SomaticPoint
> Профиль: Append-only (импорт), Single-writer

## 1. Name (Имя)
- **SomaticPoint** — одно значение за минуту.
- **Создаётся когда:** импорт соматики (синк/CSV).
- **Завершается когда:** полная очистка данных.

## 2. Description (Описание)
Минутная точка физиологии. Хранится в IndexedDB `somatic`.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **SomaticPoint** — совокупность признаков, идентифицирующих точку:
  - `t` (string) — минута «YYYY-MM-DDTHH:mm» («во сколько»)

**Объект-значения (Value Objects):**
- **HeartRate**: hr (int)
- **BodyBattery**: body_battery (int)

## 3. State Transitions
```
(нет) → [RecordPoint] → Stored
Stored → [RecordPoint] → Stored (upsert по t)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| MinuteGranularity | t — «YYYY-MM-DDTHH:mm» |
| NonNegative | hr / body_battery ≥ 0 |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSliceRefresh | SomaticPointRecorded | Журнал пересчитывает срез вокруг акта |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `RecordPoint(t, hr, body_battery)` | t, HeartRate, BodyBattery |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `RecordPoint` | `SomaticPointRecorded` |

## 8. Throughput
- Пакетная (тысячи точек за синк), редкие вызовы.

## 9. Size
- Объём: месяцы данных (~23k точек/месяц). Снапшоты не нужны.
