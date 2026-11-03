# ADR-028: карта переноса тестов из `9-tests/` в `6-msa/` (редакция 2)

**Кто:** @TE (TestEngineer) · **Дата:** 2026-11-13 · **Проверено на:** `b24b4a9b` (`origin/main`)
**Основание:** ADR-028 в **новой редакции** (`04-architecture/ADL/adr-028.md`, «Предложено», @AR `2026-11-13 16:05`): «**изоляция жизненного цикла, а не расположение папок**».
**Статус:** план обновлён; перенос выполняет **@BE** (владелец `6-msa`, коммит `be:`), принимаю я по `9-tests/scripts/check-adr028-move.sh`.

> **Что изменилось относительно редакции 1 (сохранено здесь, чтобы не было двух планов):**
> папка `6-msa/Contract.<A>_<B>` **запрещена** — «она не собирается без обоих сервисов и превращается в общий тест ни за кем». Контракт теперь = **ожидание потребителя** (тест у потребителя + HTTP-двойник провайдера) плюс **проверка провайдера** у провайдера. Межсервисные black-box сценарии идут в **`6-msa/E2E`**, у которого свой lifecycle.

---

## 1. Куда что едет (по таблице ADR-028)

| Проект `9-tests/` | Тестов (на `b24b4a9b`) | Куда | Почему |
|---|---|---|---|
| `E2E` | `[E2EFact]` 17 (включая **4 моих soft-delete**, см. §5) | `6-msa/E2E` | black-box нескольких сервисов, без фронта |
| `Contract.ClientJournal` | `[Fact]` 1 + `[StandFact]` 13 | `6-msa/E2E/Contract.Client_Journal` → **далее разделить**: ожидания в `BC-Client`, проверку в `BC-Journal` | цепочка идёт через оба сервиса |
| `Partners.Catalog` | `[Fact]` 15 + `[Theory]` 2 | `6-msa/E2E/Partners_Catalog` | межсервисная цепочка (`<pair>`) |
| `Somatic.Daily` | `[Fact]` 6 + `[Theory]` 3 | `6-msa/E2E/DailyMetrics_SomaticSeries` | то же |
| `Portability.Gateway` | `[Fact]` 10 | `6-msa/E2E/Gateway_Portability` | то же |
| `Analytics.Privacy` | `[Fact]` 12 + `[Theory]` 1 | `6-msa/E2E/Analytics_Privacy` | то же |
| `Gateways` + `Gateways.Api` | `[Fact]` 6 + `[StandFact]` 8 | `6-msa/ApiGateway.Tests` | проверяют шлюз (если понадобится контур — `6-msa/E2E/Gateways`) |
| `Journal` | `[Fact]` 7 + `[Theory]` 2 | `6-msa/BC-Journal/src/Journal.IntegrationTests` | поднимается **один** сервис: `RecordInvariantTests`/`RecordLifecycleTests` проверяют инварианты агрегата, а не связку |
| `RnD/front-e2e` | Playwright (node) | **остаётся** в `9-tests` | единственный с браузером; получит `front-e2e.md` |

**Проверка «браузера нет»:** `playwright|puppeteer|selenium` по `9-tests` совпадает **только** в `RnD/front-e2e/**` и `README.md` — ни один C#-проект браузерных пакетов не подключает.

---

## 2. Точные команды (выполняет @BE, коммит `be:`)

```bash
# 1) межсервисные black-box — в 6-msa/E2E
git mv 9-tests/E2E                    6-msa/E2E
git mv 9-tests/Contract.ClientJournal 6-msa/E2E/Contract.Client_Journal
git mv 9-tests/Partners.Catalog       6-msa/E2E/Partners_Catalog
git mv 9-tests/Somatic.Daily          6-msa/E2E/DailyMetrics_SomaticSeries
git mv 9-tests/Portability.Gateway    6-msa/E2E/Gateway_Portability
git mv 9-tests/Analytics.Privacy      6-msa/E2E/Analytics_Privacy

# 2) шлюз — свой набор
git mv 9-tests/Gateways      6-msa/ApiGateway.Tests/Gateways.IntegrationTests
git mv 9-tests/Gateways.Api  6-msa/ApiGateway.Tests/GatewaysApi.IntegrationTests

# 3) один сервис — внутрь его папки
git mv 9-tests/Journal       6-msa/BC-Journal/src/Journal.IntegrationTests
```

**Почему порядок важен:** `9-tests/E2E` должен переехать **первым**, иначе `6-msa/E2E/` не существует и последующие `git mv` в него упадут. Либо создайте каталог заранее (`mkdir -p 6-msa/E2E`).

Что поправить после `git mv` (иначе сборка зелёная, а тесты не находятся):

1. **`*.sln` / `Directory.Build.props`** — ссылки на `9-tests` станут битыми.
2. **`.github/workflows/ci-tests.yml`** — путь `dotnet test` смотрит в `9-tests` (зона @DO).
3. **`9-tests/README.md`** — переписать: здесь только фронтовые E2E.

---

## 3. Приёмка переноса (квитанция, а не слова)

```bash
bash 9-tests/scripts/check-adr028-move.sh          # по origin/main
REF=HEAD bash 9-tests/scripts/check-adr028-move.sh # до пуша
```

Пять проверок скрипта (обновлены под редакцию 2):

| # | Что | Ожидание |
|---|---|---|
| 1 | `.csproj` в `9-tests` | **0** |
| 2 | 8 целевых путей в `6-msa` (`E2E`, `E2E/Contract.Client_Journal`, 4 `<pair>`, `ApiGateway.Tests`, `BC-Journal/src/Journal.IntegrationTests`) | все есть |
| 3 | `9-tests/RnD/front-e2e/playwright.config.js` | на месте |
| 4 | старые каталоги в `9-tests` | удалены (иначе проект существует дважды) |
| 5 | атрибуты тестов — **сумма по репозиторию** `6-msa` + `9-tests` | `Fact ≥ 128`, `StandFact ≥ 20`, `Theory ≥ 11`, `E2EFact ≥ 17` |

Baseline посчитан на `b24b4a9b`: `6-msa` 71 Fact + 2 Theory; `9-tests` 57 Fact + 20 StandFact + 9 Theory + 17 E2EFact. Сумма не зависит от переноса — падает только при **потере** файлов.

Дополнительно (руками, один раз): `dotnet build` каждого перенесённого проекта и `dotnet test` в CI.

---

## 4. Блокер, который остаётся: кто коммитит

`loop.config.json`: `zones.te.core = ["9-tests/*"]`, `zones.be.core = ["6-msa/*"]`. Коммит `te:`, трогающий `6-msa/`, даёт `check_paths` **rc=1** («Роль вышла за свою папку… если выход согласован — коммитит тот, кому принадлежит зона»). Проверено фактом на временном worktree (коммит не пушился).

Поэтому: **перенос выполняет @BE** (`be:`-коммит), я — принимаю. @BE подтвердил готовность `2026-11-13 15:46` («сделаю по карте §2, коммит `be:`»), ждёт подтверждения @TL.

---

## 5. Что это значит для моих тестов soft-delete (`PartnerSoftDeleteTests.cs`)

Файл написан под `9-tests/E2E` (`29b9d95d`, 4 `[E2EFact]`, сборка чистая). По новой редакции ADR-028 он **едет вместе с `E2E`** в `6-msa/E2E`, но по правилу «контракт = ожидание потребителя + проверка провайдера» его стоит **разделить**:

| Тест | Куда по правилу |
|---|---|
| `Remove_PartnerDisappearsFromList` | `BC-Partners/src/Partners.IntegrationTests` — проверка провайдера (Partners сам) |
| `Remove_IsIdempotent_SecondCallIsNotServerError` | туда же |
| `Remove_ThenGet_ReturnsNotFoundOrForbidden_NotOk` | туда же |
| `Remove_PartnerInExistingJournalRecord_StillResolvable` | **`6-msa/E2E`** — трогает Partners **и** Journal, значит black-box сценарий |

Разделение — следующая работа, не блокирует перенос: `git mv` переносит проект целиком, а сплит делается отдельным коммитом @BE (его зона).

---

## 6. Что нужно от ролей (одной строкой каждому)

- **@TL** — подтвердить @BE старт переноса (он готов с `15:46`); принять ADR-028 в новой редакции.
- **@DO** — CI-пути после переноса + пять гейтов из комплаенса ADR-028 (в т.ч. «`6-msa/E2E` не упоминается в `ProjectReference` ни одного сервиса»).
- **@BE** — выполнить §2, прогнать §3, прислать вывод.
- **@LM** — `AGENTS.md §1`: тесты сервисов — зона владельца сервиса, фронтовые E2E — @TE (иначе зоны не совпадают с ADR-028).
