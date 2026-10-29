# BC-Partners — микросервис `partners`

Профили партнёров (Supporting Domain, роль Execution). Один ограниченный контекст
`Partners` и один агрегат **Partner**.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002).

## Агрегат Partner

| Поле | Тип |
|---|---|
| name | string |
| photo | string (dataURL) |
| gender | string (`male` / `female`) |
| notes | string |

## Command / Query API (`/api/v1/partners`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/partners` | CreatePartner |
| PUT | `/api/v1/partners/{id}` | UpdatePartner |
| DELETE | `/api/v1/partners/{id}` | DeletePartner |
| GET | `/api/v1/partners/{id}` | GetPartnerById |
| GET | `/api/v1/partners` | ListPartners |
| POST | `/api/v1/partners/admin/rebuild-projections` | Rebuild проекций |

## Сборка и тесты

```bash
cd src
dotnet restore Partners.sln
dotnet build Partners.sln --configuration Release
dotnet test Partners.Domain.Tests/Partners.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-partners:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Partners/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-003.md`
