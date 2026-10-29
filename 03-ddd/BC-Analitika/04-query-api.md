# Query API: Аналитика

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник | Статус |
|---|---|---|---|---|
| Сводка | renderSummary() | SummaryCards | `records` | ⬜ |
| Физиопрофиль | renderPhysioProfile() | PhysioProfile | `somatic` (срезы) | ⬜ |
| Топ-списки | renderTops() | TopLists | `records` | ⬜ |
| Распределение | renderDistribution() | Distribution | `records` | ⬜ |
| Пороги | renderThresholds() | Thresholds | `somatic` | ⬜ |
| Тренды | renderTrends() | TrendsView | `weekly` + `sleep` | ✅ |
| Корреляции | renderCorrelations() | CorrelationsView | `records` + `weekly`/`sleep` | ✅ |

## Response DTO
- SummaryCards: сессии, суммарное время, средняя длительность, оргазмы, средняя оценка, стрик.
- PhysioProfile: график «до/во время/после» + инсайты.
- TopLists: топ по категориям с периодами.
- CorrelationsView: пары (длительность↔пульс, оргазмы↔пульс, BB↔длительность, сон↔частота) с пояснением и n.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI пользователя | все | экран «Аналитика» |
