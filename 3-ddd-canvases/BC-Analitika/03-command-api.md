# Command API: Аналитика

> Read-only контекст: команд записи нет.

## Входящие события (из других контекстов)
| Событие | Источник | Использование |
|---|---|---|
| RecordCreated / RecordUpdated | Журнал | пересчёт сводки/топов/распределения/корреляций |
| SomaticImported / SleepImported | Соматика | пересчёт физиопрофиля/порогов/трендов |

## Обработчики (пересчёт проекций)
| Обработчик | Функция (код) | Результат | Статус |
|---|---|---|---|
| Сводка за период | renderSummary() (новый) | SummaryCards | ⬜ ST-01 |
| Физиопрофиль | renderPhysioProfile() (новый) | PhysioProfile | ⬜ ST-05 |
| Топ-списки | renderTops() (новый) | TopLists | ⬜ ST-06 |
| Распределение | renderDistribution() (новый) | Distribution | ⬜ ST-07 |
| Пороги/аномалии | renderThresholds() (новый) | Thresholds | ⬜ ST-08 |
| Тренды | renderTrends() | TrendSeries | ✅ |
| Корреляции | renderCorrelations() | CorrelationResult | ✅ (расширить пары) |
| Коэффициент | pearson(xs, ys) | число | ✅ |

## Цепочка корреляции имён
```
openTrends()/renderSummary()/… → getWeekly() + DB.getAllRecords() → проекции → UI
renderCorrelations() → records × weekly/sleep → pearson() → таблица
```
