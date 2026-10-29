# Materialized Views: Приватность и доступ

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `meta` | key-value (keyPath `key`) | hasPin, verifyPin |

## Store: meta (запись «pin»)
| Поле | Тип | Источник |
|---|---|---|
| salt | base64 | Pin.setPin |
| hash | hex | PBKDF2(pin, salt) |
| iterations | int | 100000 |

## Проекционные правила
| Событие | Действие |
|---|---|
| PinSet | PUT (key «pin») |
| PinRemoved | DELETE (key «pin») |
