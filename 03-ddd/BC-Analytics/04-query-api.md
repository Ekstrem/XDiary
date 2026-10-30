# Query API: Аналитика (Analytics)

> Read-only контекст: читает ReadModel соседних сервисов, ничего не пишет.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Статус |
|---|---|---|---|
| Сводка | `GET /api/v1/analytics/summary?period=30\|90\|all` | SummaryCards | ⬜ |
| Физиопрофиль | `GET /api/v1/analytics/physio/{recordId}` | PhysioProfile | ⬜ |
| Топ-списки | `GET /api/v1/analytics/tops` | TopLists | ⬜ |
| Распределение | `GET /api/v1/analytics/distribution` | Distribution | ⬜ |
| Пороги | `GET /api/v1/analytics/thresholds` | Thresholds | ⬜ |
| Тренды | `GET /api/v1/analytics/trends` | TrendsView | ✅ |
| Корреляции | `GET /api/v1/analytics/correlations` | CorrelationsView | ✅ |

## Response DTO
- SummaryCards: сессии, суммарное время, средняя длительность, суммарные оргазмы (по участникам), средняя оценка, стрик.
- CorrelationsView: пары (длительность↔пульс, оргазмы↔пульс, BB↔длительность, сон↔частота) с пояснением и n.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | экран «Аналитика» |
