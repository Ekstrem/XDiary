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

## Соматика → аналитика (Фаза 4, контракт)
### Корреляции (pearson, n ≥ 3)
| Пара | X | Y | Источник X | Источник Y |
|---|---|---|---|---|
| длительность↔пульс | `durationMin` | avg(`hr`) в окне ±30 мин | Record | SomaticPoint |
| оргазмы↔пульс | sum(`orgasms.count`) | avg(`hr`) | Record.orgasms | SomaticPoint |
| BB↔длительность | BB Δ (first−last) | `durationMin` | SomaticPoint.body_battery | Record |
| сон↔частота | `sleep.score` | частота (records/день) | DailyMetrics.sleep | Record.startedAt |

- **Окно сессии:** ±30 мин вокруг `startedAt` (срез `somatic`).
- **Пульс в корреляциях** = Somatic `hr` (во время сессии), НЕ `RestingHr`.

### Физиопрофиль (physio)
- усреднённый профиль пульса/BB ±30 мин вокруг `startedAt`.

### Пороги (thresholds)
- max `hr` / min `body_battery` за период.

### Тренды (trends)
- стресс/сон по дням (DailyMetrics).

## Физические таблицы: **нет ни одной** (проверено 2026-11-11, REF `cc8edd566`, @AR)

**Факт:** в `6-msa` нет ни одной миграции с таблицей `analytics_*` (`git grep "CreateTable(… name: \"analytics"` → 0),
и у `BC-Analytics` **нет Storage-проекта** — только `Analytics.Api`. Проекция живёт **в памяти процесса**:
`Precompute/AnalyticsSnapshot.cs:11` — `Dictionary<Guid, ClientSnapshot>`, зарегистрирован синглтоном.

**Что это значит для чтения таблицы выше:** `✅` в столбце «Статус» означает «правило проекции реализовано»,
а **не** «есть таблица в БД». Ни одна из семи MV не персистентна.

**Следствие (не дефект, а форма):** рестарт `analytics` = **проекция пуста**, а не «считает неверно»; до первого
пересчёта `GET /api/v1/analytics/*` отдаёт значения, посчитанные **в этом процессе**. Это тот же класс, что «ноль без
окна — не ноль», и он должен быть назван здесь, иначе персистентность проекции подразумевается молча.

**Когда появится персистентность** — это меняет `05` по существу: появится миграция `analytics_*`, и тогда
таблица выше дополнится блоком «Физические таблицы» с колонками (как в `BC-Journal/05` и `BC-Partners/05`).

## Перспектива
- ST-01 (сводка), ST-05..ST-08 — не реализованы.
