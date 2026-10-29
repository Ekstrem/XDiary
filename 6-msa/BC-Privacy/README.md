# BC-Privacy — микросервис `privacy`

Приватность и доступ (Core Domain, роль Execution). Один ограниченный контекст `Privacy`
и один агрегат **PinCredential**.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002).

## Агрегат PinCredential

Хранит только **хеш PIN (PBKDF2-SHA256, 100000 итераций) с солью** — открытый PIN не сохраняется.

| Поле | Тип |
|---|---|
| userId | string |
| salt | base64 |
| hash | hex (PBKDF2-SHA256) |
| iterations | int (100000) |

## Command / Query API (`/api/v1/privacy`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/privacy/pin` | SetPin (PBKDF2-SHA256) |
| DELETE | `/api/v1/privacy/pin?userId=…` | RemovePin |
| GET | `/api/v1/privacy/has-pin?userId=…` | HasPin |
| POST | `/api/v1/privacy/verify-pin` | VerifyPin |
| POST | `/api/v1/privacy/token` | Выпуск access/refresh JWT (после PIN) |
| POST | `/api/v1/privacy/verify-token` | Проверка JWT (stateless, для шлюза) |

JWT — HS256, issuer `xdiary`, краткоживущий access (15 мин) + refresh (30 дней). Signing key из `Jwt:SigningKey`.

## Сборка и тесты

```bash
cd src
dotnet restore Privacy.sln
dotnet build Privacy.sln --configuration Release
dotnet test Privacy.Domain.Tests/Privacy.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-privacy:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Privacy/01..05-*.md`
- ADR: `04-architecture/ADL/adr-008.md`
- Бэклог: `5-tech-backlog/back-end/BE-010.md`
