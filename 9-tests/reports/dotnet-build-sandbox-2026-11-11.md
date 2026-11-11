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

---

## Проверка `TE-103` после отклонения (`2026-11-12`, круг 10)

@TL отклонил `TE-103` («премиса снята: `CS7036` устранён, сборка `Ошибок: 0`, ревизия `d406f9bd8`»).
Проверил независимо на свежем `origin`:

| Что | Квитанция |
|---|---|
| тесты используют пары `(method, path)` | `GatewayRoutesTests.cs:7-12` — `[InlineData("GET", "/health")]`, `("POST", "/api/v1/clients")`, `("POST", "/api/v1/clients/refresh")`, `("POST", "/api/v1/clients/{id}/verify")`, `("POST", "/api/v1/privacy/verify-token")`; защитные — `:15-18` `("GET", "/api/v1/clients")`, `("GET", "/api/v1/partners")`, `("GET", "/api/v1/clients/me")`, `("POST", "/api/v1/privacy/pin/set")` |
| ошибок компиляции нет | `error CS` — **0** |
| прочий `rc` | в свежем worktree `dotnet build --no-restore` даёт `error NETSDK1004` (нет `project.assets.json`) — это артефакт `--no-restore`, а не код: после restore ошибка уходит |

Вывод: отклонение `TE-103` **корректно**, моя прежняя находка (`CS7036` ×2) закрыта правкой тестов, и тест
теперь проверяет именно пары, а не «однорукий» вызов — то есть критерий карточки выполнен по существу.

## `BE-097`: премиса снята правкой **канона**, не кода (`2026-11-12`, круг 10)

- канон `04-architecture/api-index.yaml`, `POST /api/v1/analytics/events`:
  `required: [eventType, date, t]`, `optional: [id, stress, sleepScore, hr, bodyBattery, record]`
  — `date`/`t` больше **не** обещаны как необязательные;
- код: `Analytics.Api/Precompute/JournalRecord.cs:24` `public string Date …`, `:28` `public string T …` —
  по-прежнему non-nullable, и теперь это **согласовано** с каноном (оба требуют);
- мой гейт: `check_api_index_optional.py` → `операций с optional 6 | полей проверено 10 | нарушений 0`,
  `rc=0` (было 2 нарушения).

То есть критерий `BE-097` «`0` нарушений по Analytics» **выполнен**, а «если правится канон — согласовать
с @AR» выполнено самим @AR (правка его зоны). Закрывает владелец.
