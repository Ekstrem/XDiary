# Materialized Views: Аналитика (Analytics)

## Проекции (MV — прекомпьют от событий соседних сервисов)
Постоянные MV — обновляются инкрементально от событий; Query API читает **готовое** (не считает на лету и не на клиенте).

| MV | Источник событий | Статус |
|---|---|---|
| `analytics_summary` | `RecordCompleted` (Journal) | ⬜ |
| `analytics_physio` | `RecordCompleted` + `SomaticPointRecorded` | ⬜ |
| `analytics_tops` | `RecordCompleted` | ⬜ |
| `analytics_distribution` | `RecordCompleted` | ⬜ |
| `analytics_thresholds` | `SomaticPointRecorded` | ⬜ |
| `analytics_trends` | `StressRecorded`/`SleepRecorded` (DailyMetrics) | ✅ |
| `analytics_correlations` | `RecordCompleted` × DailyMetrics | ✅ |

## Правила проекции (событие → MV)
| MV | Правило |
|---|---|
| `analytics_summary` | count, sum(durationMin), avg, **sum(оргазмы по участникам)**, avg(rating), streak |
| `analytics_physio` | усреднённый профиль пульса/BB ±30 мин вокруг startedAt |
| `analytics_tops` | group by поле (позы/игрушки/костюмы/фантазии/места/партнёры) → count |
| `analytics_distribution` | group by день недели и час startedAt |
| `analytics_correlations` | pearson(…) при n ≥ 3 |

## Перспектива
- ST-01 (сводка), ST-05..ST-08 — не реализованы.
