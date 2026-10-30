# Materialized Views: Шлюз Garmin (GarminGateway)

## Проекция
Шлюз не хранит постоянных данных — отдаёт нормализованные DTO наружу.

| Проекция | Назначение |
|---|---|
| Соматика (норм.) | → SomaticSeries / DailyMetrics |
| Занятия (норм.) | → Журнал (автоперенос) |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `GarminSynced` | отдать соматику → SomaticSeries / DailyMetrics |
| `GarminActivitiesFetched` | отдать занятия → Журнал |
