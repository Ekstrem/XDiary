# Materialized Views: Аналитика (Analytics)

## Проекции (MV — прекомпьют)
Постоянные MV — обновляются **событийно** (целевое) / schedule (заглушка AR-006); Query API читает **готовое** (не считает на лету и не на клиенте).

| MV | Источник (pull) | Статус |
|---|---|---|
| `analytics_summary` | read-model `records` (Journal) | ⬜ |
| `analytics_physio` | `records` + `somatic` | ⬜ |
| `analytics_tops` | `records` | ⬜ |
| `analytics_distribution` | `records` | ⬜ |
| `analytics_thresholds` | `somatic` | ⬜ |
| `analytics_trends` | `weekly` + `sleep` (DailyMetrics) | ✅ |
| `analytics_correlations` | `records` × `somatic` × DailyMetrics | ✅ |

## Объём (фазы, PO 2026-11-03)
- **0. Простые просмотры** — журнал + партнёры (list/detail) работают.
- **1. Аналитика Journal** — `analytics_summary`/`tops`/`distribution` (`RecordCompleted`).
- **Отложено (соматика):** `physio`/`thresholds`/`correlations`/`trends` — после идентификации/аутентификации + проверки загрузки Garmin.

## Обновление MV
**Целевое — event-driven** (директива PO): MV обновляется инкрементально от событий `RecordCompleted`/`RecordDeleted`/`SomaticPointRecorded`/`StressRecorded`/`SleepRecorded` — событие = источник истины (Event Store), доставка соседям через **Outbound Handler (Saga/HTTP)**, затем подписчик → MV.

**Текущая заглушка (техдолг AR-006):** в Single-профиле межсервисной шины нет (EventBus InMemory — внутри процесса), поэтому пока — `HostedService` по расписанию (HTTP pull, 2 мин) как **fallback**. Расписание — временная заглушка, не основной механизм; переход к event-driven — AR-006.

## Правила проекции
| MV | Правило |
|---|---|
| `analytics_summary` | count, sum(durationMin), avg, **sum(оргазмы по участникам)**, avg(rating), streak |
| `analytics_physio` | усреднённый профиль пульса/BB ±30 мин вокруг startedAt |
| `analytics_tops` | group by поле (позы/игрушки/костюмы/фантазии/места/партнёры) → count |
| `analytics_distribution` | group by день недели и час startedAt |
| `analytics_correlations` | pearson: длительность↔hr, оргазмы↔hr, BB↔длительность, сон↔частота (n ≥ 3); **пульс = Somatic `hr` (во время сессии), НЕ `RestingHr`** |

## Перспектива
- ST-01 (сводка), ST-05..ST-08 — не реализованы.
