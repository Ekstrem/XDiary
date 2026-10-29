# Materialized Views: Соматика — минутные точки

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `somatic` | минутные точки (keyPath `t`) | SomaticSlice |

## Store: somatic
| Поле | Тип | Источник |
|---|---|---|
| t | string PK | minute_of(ms) |
| hr | int | heartRateValues |
| body_battery | int | bodyBatteryValuesArray |

## Проекционные правила
| Событие | Действие |
|---|---|
| SomaticImported | PUT (upsert по t) |
