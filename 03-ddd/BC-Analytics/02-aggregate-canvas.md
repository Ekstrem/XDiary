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

**2.2. Состояние — применимо иначе (AR-017).** У Analytics **нет агрегата**: контекст проекционный, он не принимает
команд и не хранит состояния домена. `6-msa/BC-Analytics/src/Analytics.Domain` содержит только расчётные модели
(`AnalyticsInput`, `CorrelationResult`, `SomaticViews`) — это **вход/выход вычисления**, а не `AnemicModel`.

**2.2.2. Почему это правильный ответ, а не пробел:** ADR-004 (MV) требует, чтобы аналитика была **проекцией** событий;
агрегат здесь означал бы, что Analytics хранит собственное состояние и принимает решения — этого нет ни в коде, ни в
намерении. Однако следствие надо назвать: у Analytics **нет инвариантов**, поэтому любая проверка «достаточно ли данных
для корреляции» живёт в расчёте и возвращает «мало данных» вместо отказа — это и есть контракт MV (пустая проекция
легальна).

**2.2.3. Что проверяется вместо атрибутов:** (а) вход — события из других BC (схема события, `clientId`, `version`);
(б) выход — поля MV (`sessions`, `totalMinutes`, `orgasms`, коэффициенты корреляции); (в) отсутствие собственного
состояния — `AddEventStorePostgres` для Analytics **не должен** появляться без ADR (иначе получим второй источник правды).

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
