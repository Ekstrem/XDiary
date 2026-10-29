# 1. Bounded Context Canvas — Журнал интимной активности

Канонический канвас: [`03-ddd/BC-Journal/01-context-canvas.md`](../../../03-ddd/BC-Journal/01-context-canvas.md)

| Секция | Значение |
|---|---|
| Имя | Журнал интимной активности (Activity Journal) |
| Классификация | Core Domain |
| Роль | Execution |
| Микросервис | `journal` (ADR-001) |

## Адаптация к CEQRS

Исходный канвас описывает локальную PWA (IndexedDB). В микросервисе:

- Inbound: UI (визард) → **Command API** (`POST/PUT/DELETE /api/v1/journal`).
- Outbound: выбор партнёра/каталога → `partners`/`catalog` (через шлюз, HTTP); срез соматики → `somatic-series`; событие «акт записан» → `analytics`; экспорт → `portability` (CSV).
- Хранение: `records` в PostgreSQL (Read Store), события — в Command Store.
