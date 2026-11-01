# Materialized Views: Аналитика (Analytics)

## Проекции (MV — прекомпьют)
Постоянные MV — обновляются **по расписанию/pull** (не событийно, см. ниже); Query API читает **готовое** (не считает на лету и не на клиенте).

| MV | Источник (pull) | Статус |
|---|---|---|
| `analytics_summary` | read-model `records` (Journal) | ⬜ |
| `analytics_physio` | `records` + `somatic` | ⬜ |
| `analytics_tops` | `records` | ⬜ |
| `analytics_distribution` | `records` | ⬜ |
| `analytics_thresholds` | `somatic` | ⬜ |
| `analytics_trends` | `weekly` + `sleep` (DailyMetrics) | ✅ |
| `analytics_correlations` | `records` × DailyMetrics | ✅ |

## Обновление MV (Single-профиль)
Межсервисной событийной шины в Single-профиле нет (EventBus InMemory — только внутри процесса). Поэтому Analytics MV обновляется **по расписанию/pull** (precompute из read-model соседних сервисов), а не событийно. Событийное обновление — при эволюции профиля (Cqrs/Ceqrs).

## Правила проекции
| MV | Правило |
|---|---|
| `analytics_summary` | count, sum(durationMin), avg, **sum(оргазмы по участникам)**, avg(rating), streak |
| `analytics_physio` | усреднённый профиль пульса/BB ±30 мин вокруг startedAt |
| `analytics_tops` | group by поле (позы/игрушки/костюмы/фантазии/места/партнёры) → count |
| `analytics_distribution` | group by день недели и час startedAt |
| `analytics_correlations` | pearson(…) при n ≥ 3 |

## Перспектива
- ST-01 (сводка), ST-05..ST-08 — не реализованы.
