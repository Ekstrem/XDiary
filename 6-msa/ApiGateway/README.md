# ApiGateway — микросервис `api-gateway`

Единая точка входа (YARP reverse proxy) + BFF-агрегация (ADR-006, ADR-007).

## Назначение

- Единая точка входа `/api/v1/...` для PWA.
- Маршрутизация на 10 микросервисов (YARP).
- JWT-проверка (stateless, HS256 — тот же signing key, что у privacy).
- BFF-агрегация составных экранов (сводка аналитики: journal + daily-metrics).
- CORS + HTTPS.

## Маршруты (ReverseProxy)

| Префикс | Сервис |
|---|---|
| `/api/v1/journal` | journal |
| `/api/v1/partners` | partners |
| `/api/v1/catalog` | catalog |
| `/api/v1/somatic-series` | somatic-series |
| `/api/v1/daily-metrics` | daily-metrics |
| `/api/v1/garmin-gateway` | garmin-gateway |
| `/api/v1/apple-health-gateway` | apple-health-gateway |
| `/api/v1/analytics` | analytics |
| `/api/v1/privacy` | privacy |
| `/api/v1/portability` | portability |

## JWT (stateless)

Запросы без валидного `Authorization: Bearer <JWT>` отклоняются (401).
Исключения (анонимные): `/health` и `/api/v1/privacy/*` (установка PIN, выпуск/проверка токена).

## BFF

`GET /api/v1/bff/analytics-summary?period=30` — агрегирует данные из journal + daily-metrics.

## Сборка

```bash
dotnet restore ApiGateway.csproj
dotnet build ApiGateway.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-api-gateway:single` (и `:latest`).

## Связь с артефактами

- ADR: `04-architecture/ADL/adr-006.md`, `adr-007.md`, `adr-008.md`
- Бэклог: `5-tech-backlog/back-end/BE-012.md`
