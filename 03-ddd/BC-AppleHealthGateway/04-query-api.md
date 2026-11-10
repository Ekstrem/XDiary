# Query API: Шлюз Apple Health (AppleHealthGateway)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Статус импорта | `GET /api/v1/apple-health/status` | ImportStatus |

## Response DTO
- ImportStatus: lastImport, processedFiles.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | ImportStatus | показать состояние импорта |

## Маршрут, отсутствовавший в этом файле (добавлено 2026-11-10, @AR)

| Запрос | Эндпоинт | Ответ | Адрес |
|---|---|---|---|
| Состояние синхронизации | `GET /api/v1/apple-health/state` | состояние импорта/конфигурации | `AppleHealthGatewayController.cs:30-34` |

