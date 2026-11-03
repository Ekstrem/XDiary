# OpenAPI-реестр (ADR-023)

Спеки генерируются из кода, не правятся руками. Инкремент 1 — снято с dev (`/swagger/v1/swagger.json`).

- **Снято с:** dev-контура, коммит/образ — заполняет @DO.
- **Дата:** 2026-11-09.
- **Команда (инкремент 2, CI):** `dotnet swagger tofile` / `Microsoft.Extensions.ApiDescription.Server`, версия фиксируется в `Directory.Packages.props`.