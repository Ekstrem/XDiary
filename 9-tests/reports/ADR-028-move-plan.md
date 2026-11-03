# ADR-028: карта переноса тестов из `9-tests/` в `6-msa/`

**Кто:** @TE (TestEngineer) · **Дата:** 2026-11-13 · **Ревизия проверки:** `ec7e52c9` (`origin/main`)
**Основание:** ADR-028 (Предложено, задание владельца: «тесты микросервисов без фронта — в папке с микросервисами»), задание @AR `2026-11-13 14:45`.
**Статус:** **план готов, перенос НЕ выполнен** — упирается в границы зон (см. §4). Все числа ниже посчитаны на `ec7e52c9`.

---

## 1. Классификация: что действительно «без фронта»

Правило ADR-028: критерий ровно один — **участвует ли фронт/браузер**. Проверил по содержимому проектов, а не по названию:

| Проект `9-tests/` | Тестов | Чем является | Куда |
|---|---|---|---|
| `Contract.ClientJournal` | `[Fact]` 1 + `[StandFact]` 13 | C# против API, браузера нет | `6-msa/Contract.Client_Journal` |
| `Partners.Catalog` | `[Fact]` 15 + `[Theory]` 2 | C# против API | `6-msa/Contract.Catalog_Partners` |
| `Somatic.Daily` | `[Fact]` 6 + `[Theory]` 3 | C# против API | `6-msa/Contract.DailyMetrics_SomaticSeries` |
| `Portability.Gateway` | `[Fact]` 10 | C# против API | `6-msa/Contract.Gateway_Portability` |
| `Analytics.Privacy` | `[Fact]` 12 + `[Theory]` 1 | C# против API | `6-msa/Contract.Analytics_Privacy` |
| `Gateways` | `[Fact]` 5 + `[Theory]` 1 | C# против API-шлюза | `6-msa/Gateways` |
| `Gateways.Api` | `[Fact]` 1 + `[StandFact]` 7 | C# против API-шлюза | `6-msa/Gateways` (слить с предыдущим) |
| `Journal` | `[Fact]` 7 + `[Theory]` 2 | инварианты/жизненный цикл записи (`RecordInvariantTests`, `RecordLifecycleTests`) — **это unit/интеграционные тесты агрегата**, а не контракт пары | `6-msa/BC-Journal/src/Journal.IntegrationTests` |
| `E2E` | `[E2EFact]` — свой атрибут, xunit-стек | C# E2E против **API** (не браузер): `AuthE2ETests`, `JournalE2ETests`, `SomaticAnalyticsE2ETests` | `6-msa/E2E` |
| `RnD/front-e2e` | Playwright (node) | **единственный, кто реально требует браузера** | **остаётся в `9-tests/`** |

**Проверка «браузера нет»:** поиск `playwright|puppeteer|selenium` по `9-tests` даёт совпадения **только** в `9-tests/RnD/front-e2e/**` и `9-tests/README.md`. Ни один C#-проект браузерных пакетов не подключает.

**Отдельно про `Journal`:** @AR оставил выбор «`BC-Journal/src/…` или `6-msa/E2E` — реши по тому, что он реально проверяет». Проверил: `RecordInvariantTests`/`RecordLifecycleTests` проверяют **инварианты агрегата** (жизненный цикл записи), а не связку двух сервисов → место — `6-msa/BC-Journal/src/Journal.IntegrationTests`.

**Отдельно про `E2E`:** несмотря на имя, это **не** браузерные тесты — xunit + HTTP. Значит по ADR-028 уезжает в `6-msa/E2E`. Имена файлов (`*E2ETests`) сохраняю, чтобы не терять историю ссылок.

---

## 2. Точные команды переноса (механические, код не переписывается)

```bash
# контракты пары — в 6-msa/Contract.<A>_<B> по алфавиту (правило @AR: иначе «виноват» тот, чья папка)
git mv 9-tests/Contract.ClientJournal    6-msa/Contract.Client_Journal
git mv 9-tests/Partners.Catalog          6-msa/Contract.Catalog_Partners
git mv 9-tests/Somatic.Daily             6-msa/Contract.DailyMetrics_SomaticSeries
git mv 9-tests/Portability.Gateway       6-msa/Contract.Gateway_Portability
git mv 9-tests/Analytics.Privacy         6-msa/Contract.Analytics_Privacy

# шлюз — один каталог на два проекта
mkdir -p 6-msa/Gateways
git mv 9-tests/Gateways      6-msa/Gateways/Gateways.IntegrationTests
git mv 9-tests/Gateways.Api  6-msa/Gateways/GatewaysApi.IntegrationTests

# агрегатные тесты — к своему BC
git mv 9-tests/Journal       6-msa/BC-Journal/src/Journal.IntegrationTests

# API-E2E (не браузерные)
git mv 9-tests/E2E           6-msa/E2E
```

Что **обязательно** поправить после `git mv` (иначе сборка зелёная, а тесты не находятся):

1. **`Directory.Build.props` / `*.sln`** — если в корне есть солюшен или общий props с путями к `9-tests`, ссылки станут битыми.
2. **`9-tests/9-tests.sln`** (если есть) — удалить перенесённые проекты из солюшена.
3. **`CI` (`ci-tests.yml`)**: список проектов/путей для `dotnet test` — сейчас смотрит в `9-tests`; после переноса должен смотреть в `6-msa`. Это зона @DO (см. §4).
4. **`9-tests/README.md`** — переписать: теперь здесь только фронтовые E2E.

---

## 3. Чем проверять после переноса (критерий приёмки переноса)

```bash
# 1) сборка каждого перенесённого проекта
dotnet build 6-msa/Contract.Client_Journal/ContractClientJournal.IntegrationTests/*.csproj
# ... и так по каждому из 8 путей

# 2) прогон (в CI; локально в песочнице блокируется testhost-пайпами)
dotnet test 6-msa/Contract.Client_Journal/... --no-build

# 3) в 9-tests не осталось C#-проектов
git ls-tree -r --name-only origin/main -- 9-tests | grep -c '\.csproj$'    # ожидаем 0

# 4) фронтовые E2E на месте
ls 9-tests/RnD/front-e2e/playwright.config.js
```

**Ожидаемые числа тестов после переноса** (сверять с выводом `dotnet test`, чтобы убедиться, что набор не потерялся при переносе):
`Contract.Client_Journal` 14 (`1 Fact + 13 StandFact`) · `Contract.Catalog_Partners` 17 · `Contract.DailyMetrics_SomaticSeries` 9 · `Contract.Gateway_Portability` 10 · `Contract.Analytics_Privacy` 13 · `Gateways` 14 (6+8) · `BC-Journal` 9 · `E2E` — по числу `[E2EFact]`.

Скрипт проверки после переноса (после переноса — в `9-tests/scripts/check-adr028-move.sh`): печатает `PASS`/`FAIL` по 4 пунктам выше + число `.csproj` в `9-tests` (должно быть 0) и наличие `front-e2e`.

---

## 4. Блокер: границы зон (почему не выполнено сейчас)

`loop.config.json` → `zones.te.core = ["9-tests/*"]`; `zones.be.core = ["6-msa/*"]`.
`guard-paths.yml` прогоняет `tools/check_paths.py --range` на каждом пуше.

**Проверил фактом** (временный worktree, коммит не пушился): коммит с субъектом `te: …`, трогающий `6-msa/zz-probe-zone.txt`, даёт

```
guard: проверено коммитов 1, пропущено без роли 0, нарушений 1
rc=1
Роль вышла за свою папку. Что делать:
  1) перенести правку в зону владельца (или оформить задачу через chat);
  2) если выход согласован — коммитит тот, кому принадлежит зона, с префиксом своей роли;
  3) карта зон: python tools/check_paths.py --zones
```

То есть `git mv` в `6-msa/` **под моим префиксом** гарантированно покраснит guard. Правильный путь — ровно тот, что предлагает сам гейт: **коммитит владелец зоны (`be:`)**, либо сначала меняются зоны (`AGENTS.md §1` + `loop.config.json` — зона @LM/@TL, о чём @TL уже написал в `14:45`).

**Что нужно от @TL (одна строка):** либо «@BE выполняет перенос по карте из §2», либо «@TE выполняет перенос, зона расширена (или commit делается с `be:`-префиксом по договорённости)». До этого артефакт остаётся планом, а `9-tests` не меняется — иначе я создам ровно ту проблему, которую гейт и ловит.

---

## 5. Что станет с зоной @TE (важно для приёмки продукта)

После ADR-028 `9-tests/` перестаёт быть «всеми тестами» и становится «фронтовыми E2E» (+ `9-tests/reports` с квитанциями). Это **сужает** мою зону: контрактные тесты сервисов уходят владельцу сервиса. В `AGENTS.md §1` это ещё не отражено — @TL в `14:45` попросил @LM внести строку. Пока строка не внесена, формально я не имею права вести контрактные тесты в `6-msa`, а @BE не должен вести фронтовые — то есть **обе стороны сейчас в подвешенном состоянии**, и это стоит закрыть в тот же день, что и перенос.
