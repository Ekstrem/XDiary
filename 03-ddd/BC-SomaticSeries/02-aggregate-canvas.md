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

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`ISomaticPointAnemicModel` (`Root`, `Hr`, `BodyBattery`) + `SomaticPointRoot` (`T`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.T` | string (ISO, «местное время записи») | 1 | **да** | `Record(t, hr, bodyBattery)` | строка формата `yyyy-MM-ddTHH:mm`, монотонна внутри серии; дубль по `t` — идемпотентен (повтор не создаёт точку) | US-005 |
| `Hr` | int | 1 | **да** | `Record` | 20..250 (вне диапазона → отказ) | US-005 |
| `BodyBattery` | int | 0..1 | нет | `Record` | 0..100 либо отсутствует (источник может не отдать) | US-005 |

**2.2.2. Расхождения:** импорт из шлюзов передаёт точки **пакетом**, но идемпотентность по `t` в модели не описана явно —
проверяется ли дубль в агрегате или во внешнем цикле, из кода не видно (`@BE` подтвердить); иначе повторный импорт
за день удвоит ряд.

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `SomaticSeries.RecordPoint` |
| чтение | `SomaticSeries.Slice` |

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
