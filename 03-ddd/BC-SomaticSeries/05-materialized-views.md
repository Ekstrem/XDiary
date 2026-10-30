# Materialized Views: Соматика — минутные точки (SomaticSeries)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `somatic` | ReadModel (keyPath `t`) | SomaticSlice |

## Store: somatic
| Поле | Тип | Источник |
|---|---|---|
| t | string PK | сущность (минута) |
| hr | int | VO |
| body_battery | int | VO |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `SomaticPointRecorded` | PUT (upsert по t) |

## Индексы
- `t` — диапазон для среза.
