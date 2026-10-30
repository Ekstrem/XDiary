# Materialized Views: Приватность и доступ (Privacy)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `meta` | key-value (keyPath `key`) | hasPin, verify |

## Store: meta (запись «pin»)
| Поле | Тип | Источник |
|---|---|---|
| salt | base64 | VO |
| hash | hex | VO |
| iterations | int | VO |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `PinSet` | PUT (key «pin») |
| `PinChanged` | PUT (key «pin») |
| `PinRemoved` | DELETE (key «pin») |

> `Locked` / `Unlocked` — рантайм-состояние, в ReadModel не проецируются.
