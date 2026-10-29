# Command API: Аналитика и статистика

> Read-only контекст: команд записи нет.

## Входящие события (из других контекстов)
| Событие | Источник | Использование |
|---|---|---|
| RecordCreated / RecordUpdated | Журнал | пересчёт корреляций (длительность) |
| SomaticImported / SleepImported | Соматика | пересчёт трендов |

## Обработчики (пересчёт)
| Обработчик | Функция (код) | Результат |
|---|---|---|
| Открыть тренды | openTrends() / renderTrends() | графики |
| Открыть корреляции | renderCorrelations() | таблица + коэффициенты |
| Коэффициент Пирсона | pearson(xs, ys) | число |

## Цепочка корреляции имён
```
openTrends() → getWeekly() → renderTrends()/drawWeeklyChart()
renderCorrelations() → DB.getAllRecords() + getWeekly() → pearson() → таблица
```
