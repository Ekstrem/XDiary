# Архитектура (04)

Архитектурные артефакты XDiary. Владелец: этот агент.

## Разделение папок (координация агентов)
- `01-user-stories` — US (другой агент, только чтение).
- `02-cjm` — CJM (другой агент, только чтение).
- `03-ddd` — DDD-артефакты (этот агент).
- `04-architecture` — архитектура (этот агент).

## Содержимое
- `ADL/` — реестр ADR (14 принятых решений).
- `c4-containers.md` — C4-диаграмма контейнеров (10 микросервисов, профиль Single).
- `vm-sizing.md` — расчёт ресурсов ВМ для MVP (podman + PostgreSQL).
- `vps-setup.md` — памятка DevOps: настройка VPS (Ubuntu 22.04) и безопасность.

## Методология
EDM (Event-Driven Microservices) на .NET: DDD, Event Sourcing, CEQRS, фреймворк `DigiTFactory.Libraries`.
