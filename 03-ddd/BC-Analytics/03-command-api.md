# Command API: Аналитика

> Read-only контекст: команд записи нет.

## Входящие события (из других контекстов)
| Событие | Источник | Использование |
|---|---|---|
| RecordCreated / RecordUpdated | Журнал | пересчёт сводки/топов/распределения/корреляций |
| SomaticImported | Соматика (минутные точки) | пересчёт физиопрофиля/порогов |
| SleepImported | Дневные метрики | пересчёт трендов/корреляций |

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

## Маршрут, отсутствовавший в этом файле (добавлено 2026-11-10, @AR)

| Команда | Эндпоинт | Тело | Адрес |
|---|---|---|---|
| `Analytics.Events` | `POST /api/v1/analytics/events` | **событие с дискриминатором**: обязателен только `eventType`, остальные поля — по ветке (`RecordCompleted` → `id`+`record`; `RecordDeleted` → `id`; `SleepRecorded` → `date`+`sleepScore`; `SomaticPointRecorded` → `t`+`hr`+`bodyBattery`; `StressRecorded` → `date`+`stress`) | `AnalyticsController.cs:66-80`; канон `Analytics.Events` (`required: [eventType]`) |

Это **единственный** POST в Analytics; остальные маршруты контекста — чтение (см. `04-query-api.md`).
Если событие неизвестного типа — сервер отвечает `200` и **ничего не делает** (`:74-79`), это не ошибка клиента.

