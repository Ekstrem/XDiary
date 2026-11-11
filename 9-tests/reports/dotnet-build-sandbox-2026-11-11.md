# `dotnet build` в песочнице и `TE-103`/`BE-097`: что измерено (`2026-11-11`)

Короткая записка, чтобы числа не читались навыворот: в этой песочнице **`dotnet build` работает**,
а `dotnet test` — нет. И `rc` от `build` здесь **непригоден** как признак «зелено/красно».

## Что важно знать перед любым замером сборки

```
dotnet build 6-msa/BC-Analytics/src/Analytics.Api/Analytics.Api.csproj --no-restore
  → warning NU1900: не удалось загрузить индекс службы для https://api.nuget.org/v3/index.json
  → Ошибок: 0
  → rc=1
```

`rc=1` при **нуле** ошибок компиляции: `NU1900` (нет сети до `api.nuget.org`) эскалируется в код возврата.
Значит **дискриминатор — строки `error CS…` и `Ошибок: N`, а не `rc`.** `dotnet test` при этом падает
внутри `TestPlatform.TestHost` (`Process.EnsureWatchingForExit`, named pipe запрещён) — то есть тесты
в песочнице не запускаются вообще.

## `TE-103` — дефект воспроизведён (и он не только про сборку)

```
dotnet build 6-msa/ApiGateway.Tests/ApiGateway.Tests/ApiGateway.Tests.csproj --no-restore
  → GatewayRoutesTests.cs(12,91): error CS7036: Отсутствует аргумент … "path" из "GatewayRoutes.IsAnonymous(string, string)"
  → GatewayRoutesTests.cs(20,93): error CS7036: то же
  → Ошибок: 2
```

Причина: прод-сигнатура стала двухильной (`6-msa/ApiGateway/…/GatewayRoutes.cs`:
`public static bool IsAnonymous(string method, string path)`), а тест зовёт однорульную.

**Важнее самой ошибки:** ожидания старого теста **противоречат** продакшену. `IsAnonymous` реализует
deny-by-default:

| Метод + путь | Ожидание продакшена |
|---|---|
| `GET /health` (и любой метод) | `true` |
| `POST /api/v1/clients` | `true` |
| `POST /api/v1/clients/refresh` | `true` |
| `POST /api/v1/privacy/verify-token` | `true` |
| `POST /api/v1/clients/{id}/verify` | `true` |
| **`GET /api/v1/clients`** | **`false`** (всё, что не POST) |
| `GET /api/v1/partners`, `GET /api/v1/clients/me` | `false` |

Старый `[InlineData("/api/v1/clients")]` + `Assert.True` утверждал обратное — «`GET /api/v1/clients`
анонимен». Поэтому правка не «добавить аргумент», а задать **пары** `(method, path)`: иначе критерий 2
`TE-103` будет закрыт формально, а проверка останется беззубой (именно этого карточка и требует —
«тест проверяет пары»).

## `BE-097` критерий 4 — сборка Analytics

`dotnet build 6-msa/BC-Analytics/src/Analytics.Api --no-restore` → **`Ошибок: 0`** (единственная причина
`rc=1` — `NU1900` из-за отсутствия сети). То есть критерий «сборка Analytics зелёная» по коду выполнен;
сам `BE-097` закрывается правкой `string?` у `Date`/`T` и пере-прогоном моего гейта
`check_api_index_optional` (сейчас он показывает те же 2 находки, что и заводили карточку).
