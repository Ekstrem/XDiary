# Aggregate Design Canvas: TrendSeries + CorrelationResult (проекции)

> Bounded Context: Аналитика и статистика
> Контекст read-only: нет агрегатов записи, только проекции над Журналом и Соматикой.

## 1. Name (Имя)
- **TrendSeries** — дневной ряд метрики (стресс/пульс покоя/сон/HRV).
- **CorrelationResult** — коэффициент Пирсона между длительностью и метрикой.

## 2. Description (Описание)
Контекст вычисляет аналитику на лету из событий и данных других контекстов. Ничего не хранит постоянно (кроме кэша в UI).

**Value Objects:**
- **TrendSeries**: metric (string), points[] {date, value}
- **CorrelationResult**: label (string), coefficient (float), n (int)

## 3. State Transitions
```
(нет) — проекции пересчитываются при каждом открытии «Тренды».
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| MinSample | корреляция считается при n ≥ 3 |
| CoefficientRange | pearson ∈ [-1, 1] |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| RefreshOnOpen | открытие вкладки | пересчитать тренды/корреляции |

## 6. Handled Commands
- Нет (read-only). Данные поступают как события из Журнала и Соматики.

## 7. Created Events
- Нет (не пишет).

## 8. Throughput
- Чтение по требованию, низкая частота.

## 9. Size
- Не хранит данные — вычисляет на лету.
