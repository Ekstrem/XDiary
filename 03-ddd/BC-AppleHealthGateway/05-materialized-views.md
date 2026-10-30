# Materialized Views: Шлюз Apple Health (AppleHealthGateway)

## Проекция
Шлюз не хранит постоянных данных — отдаёт нормализованные DTO наружу.

| Проекция | Назначение |
|---|---|
| Соматика (норм.) | → SomaticSeries / DailyMetrics |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `AppleFileImported` | отдать соматику → SomaticSeries / DailyMetrics |
