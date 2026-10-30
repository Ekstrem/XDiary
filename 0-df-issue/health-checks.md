# DF Issue: хелфчеки (поверхностные/глубокие) не покрыты скиллом EDM

## Суть
Скилл EDM упоминает `/health` только в `references/kubernetes.md`, причём:
- пример кода на **Go**, а не .NET (несоответствие платформе);
- нет .NET-реализации (`AddHealthChecks`, `IHealthCheck`);
- нет **глубоких** хелфчеков (проверка подключения к БД/зависимостям).

## Где в скилле
- `references/kubernetes.md` → секция «Health Probes»:
  - Liveness: `GET /health` (initialDelay 5s), Readiness: `GET /health` (initialDelay 3s).
  - «Если `/health` эндпоинт не реализован — добавь в router» + пример на Go.

## Текущее состояние (6-msa)
- В 9 микросервисах + analytics хелфчеков **нет** (ни `/health`, ни `AddHealthChecks`).
- Только ApiGateway имеет поверхностный `/health` (`MapGet("/health", () => Results.Ok("ok"))`).
- Глубоких проверок БД нет нигде.

## Рекомендация
1. Поверхностный: `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")` в каждом сервисе.
2. Глубокий: `AddNpgSql(connectionString)` (или кастомный `IHealthCheck` с `CanConnectAsync()`) для проверки PostgreSQL.
3. В шлюзе (YARP) пробросить `/health` или добавить свои пробы.
## ✅ Реализовано (2026-10-31)
- `AddHealthChecks()` + `MapHealthChecks("/health")` во всех 9 сервисах.
- Кастомный `DbHealthCheck` (`CanConnectAsync()`) — глубокий, без нового NuGet-пакета.