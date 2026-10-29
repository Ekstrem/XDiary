# Aggregate Design Canvas: проекции Аналитики

> Bounded Context: Аналитика
> Контекст read-only: нет агрегатов записи, только вычисляемые проекции над Журналом и Соматикой.

## 1. Name (Имя)
Проекции (read models): SummaryCards, PhysioProfile, TopLists, Distribution, Thresholds, TrendSeries, CorrelationResult.

## 2. Description (Описание)
Контекст вычисляет аналитику на лету. Ничего не хранит постоянно.

**Value Objects:**
- **SummaryCards**: sessions (int), totalMinutes (int), avgDuration (int), orgasms (int), avgRating (float), streak (int), period (30/90/all)
- **PhysioProfile**: hrPeak (int), bbStart (int), bbDrop (float), profile (до/во время/после)
- **TopLists**: по категориям (позы/игрушки/костюмы/фантазии/места/партнёры) — {name, count}
- **Distribution**: weekday[] (int), timeOfDay[] (int)
- **Thresholds**: typicalHrPeak, typicalBbStart, anomalies[]
- **TrendSeries**: metric, points[] {date, value}
- **CorrelationResult**: label, coefficient, n

## 3. State Transitions
```
(нет) — проекции пересчитываются при открытии «Аналитики».
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| MinSample | корреляция при n ≥ 3 |
| CoefficientRange | pearson ∈ [-1, 1] |
| PeriodEnum | period ∈ {30, 90, all} |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| RefreshOnOpen | открытие вкладки | пересчитать все проекции |

## 6. Handled Commands
- Нет (read-only). Вход — события Журнала и Соматики.

## 7. Created Events
- Нет (не пишет).

## 8. Throughput
- Чтение по требованию, низкая частота.

## 9. Size
- Не хранит данные — вычисляет на лету (кэш только в UI-состоянии).

## Статус реализации
- ✅ TrendSeries (тренды) + CorrelationResult (длительность↔метрики) — реализовано.
- ⬜ SummaryCards, PhysioProfile, TopLists, Distribution, Thresholds — ST-01, ST-05..ST-08 (следующие инкременты).
