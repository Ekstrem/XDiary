# Query API: Приватность и доступ

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Есть ли PIN | Pin.hasPin() | bool | `meta` (key «pin») |
| Проверка PIN | Pin.verifyPin(pin) | bool | `meta` |

## Response DTO
- hasPin: `true | false`.
- verifyPin: `true | false` (если PIN не установлен — всегда true).

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Все контексты | hasPin | gatekeeper перед доступом |
