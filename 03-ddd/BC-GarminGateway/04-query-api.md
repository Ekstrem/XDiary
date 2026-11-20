# Query API: Шлюз Garmin (GarminGateway)

> Read-сторона (CEQRS).
> **Единый UI-контракт Connect/Sync/State, коды и истечение:** `06-connect-contract.md` (AR-163).

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Список занятий | `GET /api/v1/garmin/activities` | Activities | Garmin ACL |

## Response DTO
- Activities: список занятий (start, end, name, hr) для выбора кандидата в акт.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал (автоперенос) | Activities | «Из Garmin» — выбор занятия |

## Маршрут, отсутствовавший в этом файле (добавлено 2026-11-10, @AR)

| Запрос | Эндпоинт | Ответ | Адрес |
|---|---|---|---|
| Состояние синхронизации | `GET /api/v1/garmin/state` | состояние подключения/синхронизации | `GarminGatewayController.cs:35-39` |

