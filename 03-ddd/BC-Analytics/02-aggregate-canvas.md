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

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `Analytics.Events` |
| чтение | `Analytics.Correlations` |
| чтение | `Analytics.Distribution` |
| чтение | `Analytics.Physio` |
| чтение | `Analytics.Summary` |
| чтение | `Analytics.Thresholds` |
| чтение | `Analytics.Tops` |
| чтение | `Analytics.Trends` |

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

**2.2.4. Атрибуты проекции (заполнено 2026-11-10, `AR-017`) — «нет корня» не значит «нет состояния».** Корня-агрегата нет,
но у проекции есть **своё состояние**, и его состав надо было записать, иначе сверка «модель ↔ документ» по этому BC
пропускалась. Источник — `AnalyticsSnapshot.ClientSnapshot` (не `AnemicModel`):

| Атрибут | Тип | Что хранит | Адрес |
|---|---|---|---|
| `summary30` / `summary90` / `summaryAll` | `SummaryCards` (с `Period` = `30`/`90`/`all`) | три среза сводки по периодам | `AnalyticsSnapshot.cs:38-40` |
| `correlations` | `List<CorrelationResult>` | коэффициенты + «мало данных» | `:41` |
| `tops` | `TopLists` | топ-списки (позы/фантазии/места) | `:42` |
| `distribution` | `Distribution` | распределения по времени/дню | `:43` |
| `thresholds` | `Thresholds` | пороговые срезы | `:44` |
| `trends` | `TrendsView` | тренды (серии по датам) | `:45` |
| `physio` | `PhysioProfile` | физиология (HR/HRV/сон) | `:46` |
| `updatedAt` | `DateTime` | когда проекция последний раз пересчитана | `:47` |

**Ключ проекции — `clientId`** (`GetOrCreate(clientId)`, `AnalyticsSnapshot.cs:18-22`), то есть у каждого клиента **свой** снимок;
граница владения та же, что в остальных BC (`sub` из JWT → `clientId`).
**Как обновляется:** только событиями/пересборкой — `ApplyCompleted` / `ApplyDeleted` / `ApplySleep` / `ApplySomatic` / `ApplyStress` → `RecomputeLocked` (`:52-57`);
вход — `JournalRecord` (`JournalRecord.cs:4-12`) и метрики дня (`DailyMetricDto.cs:4-9`).
**Где живёт:** **в памяти процесса** (`AnalyticsSnapshot` — синглтон, `Precompute/AnalyticsSnapshot.cs:8`), не в БД.
Отсюда следствие, которое надо держать в голове при проверке «аналитика верна»: после рестарта проекция **пуста**, а не «неверна»;
и это тот же класс, что предусловие пустого справочника (`rc=2` «не измерено», а не дефект расчёта).
