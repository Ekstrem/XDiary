# CI/CD по слоям — свежий отчёт

**Собран:** 2026-11-07 12:51 локального (09:51 UTC) · **`origin/main`** = `d0ce5f1d`
**Как измерялось:** 18 гейтов запущены локально на этом `origin`; прогоны Actions — последний
**завершённый** прогон каждого workflow (не «в процессе»); dev — `podman inspect` + `/health`.

Счёт слоя = «зелёные / всего». Правило прежнее: слой закрыт, только когда зелёные **все**.

## Сводка

| слой | счёт | что красное (владелец) |
|---|---|---|
| **L0 · процесс** | 🟡 **3/4** | `ci-guards` #3674: `check-chat` — `AWAIT.md` (@TL) + канон read-модель ⊆ DDL (@AR/@LM) |
| **L1 · контракт** | 🟡 **1/2** | `ci-api-gateway` #20 — тесты ApiGateway (@BE) |
| **L2 · тесты сервисов** | 🟡 **11/12** | `ci-journal` #131 — 5 тестов журнала (@BE) |
| **L4 · API/APIM** | 🟢 **2/2** | — |
| **L5 · деплой/смоуки** | 🟡 **6/7** | `smoke-front` — визард: 3 кейса из 4 (@FE) |
| **L6 · E2E фронта** | 🟡 **1/2** | `front-e2e-repeat` #18 — `csv` 0/5, `partner-edit` 0/5 (@TE/@BE) |
| **гейты локально** | 🟡 **16/18** | `check_secrets` — 1 находка (@BE); `check_adr_refs` — активные ADR ссылаются на отменённые (@AR) |

## L0 · процессные гейты

| гейт / workflow | статус | факт |
|---|---|---|
| `check_nul` | 🟢 | 477 md-файлов, маркеров конфликта нет |
| `check_canon` | 🟢 | `canon:receipt-v1` в `adr-026.md` |
| `check_docs_size` | 🟢 | в пределах лимита |
| `check_rule_docs` | 🟢 | 655 файлов, ошибок 0, предупреждений **0** |
| **`check_secrets`** | 🔴 `rc=1` | 1 находка: `5-tech-backlog/chat/be.md:233`, `assign:password`, len 8 → зачистка + ротация (**@BE**) |
| `status_lint --strict` | 🟢 | 371 файл, 8 статусов |
| `chat_lint --strict-times` | 🟢 | нераспознанных строк нет |
| `stats --check` | 🟢 | `README` актуален |
| `check_paths` | 🟢 | нарушений 0 |
| `guard-paths` (CI) | 🟢 #5064 | |
| `guard-tools` (CI) | 🟢 #241 | **впервые зелёный** (был красный манифест копий) |
| `docs-budget` (CI) | 🟢 #726 | |
| `ci-guards` (CI) | 🔴 #3674 | `guard-secrets` ✅; `check-chat` ❌ на двух пунктах: регистр `AWAIT.md` (@TL) и канон «поля read-модели ⊆ колонки DDL» (@AR/@LM) |

## L1 · статические каноны контракта

| гейт | статус | факт |
|---|---|---|
| `check_api_index` | 🟢 | auth-стек **6/6** с fallback; без auth по ADR-008 — 5 |
| `check_adr` | 🟢 | проверено 6, нарушений 0 (29 старых пропущено) |
| `check_adr_status` | 🟢 | нарушений 0 |
| **`check_adr_refs --include-arch`** | 🔴 `rc=1` | 17 ссылок, нарушений по формату 0, но **связность**: активные `ADR-001`, `ADR-005`, `ADR-006` ссылаются на `ADR-007` (**Отменён**) и `ADR-029` (**Отклонено**) — 5 ошибок, каталоги `04-architecture` (**@AR**) |
| `check_ports` | 🟢 | нарушений 0 |
| `check_readstore_mapping` | 🟢 | 4 регистрации, немаршенных моделей 0 |
| `gen_images_lock --check` | 🟢 | «собран из реестра, все теги опубликованы» |
| `gen_images_lock --verify-registry` | 🟢 | 12 опубликовано, 0 отсутствует, 1 `unverified` (`front` — 403 на анонимный pull-token, едет через Pages) |
| `check_openapi --selftest` | 🟢 | самопроверка пройдена (**сам breaking-diff локально не меряется** — нужен `--generated/--base`) |
| `openapi-registry` (CI) | 🟢 #196 | |
| `ci-api-gateway` (CI) | 🔴 #20 | тесты в папке сервиса (**@BE**) |

## L2 · тесты сервисов — 11/12

🟢 `ci-backend` **#931** (был красный — стало зелёным), `ci-partners` #121, `ci-catalog` #39,
`ci-somatic-series` #41, `ci-daily-metrics` #44, `ci-analytics` #42, `ci-privacy` #37,
`ci-portability` #39, `ci-garmin-gateway` #28, `ci-apple-health-gateway` #28, `ci-client` #62.

🔴 **`ci-journal` #131** (`9e228c4c`) — падают 5 тестов:
`JournalIntegrationTest.Command_Then_Query_ReturnsRecord`,
`RecordLifecycleTests.StartFromGarmin_Then_Complete_PreservesOrigin`,
`JournalProjectionTests.Project_FromCompletedEvent_BuildsReadModel`,
`RecordLifecycleTests.Query_ReadModel_DoesNotTouch_EventStore`,
`RecordLifecycleTests.Full_Lifecycle_Command_Then_Query_ReturnsAllFields`. Владелец **@BE**.
Практическое следствие: образ `journal` с нормализацией `startedAt` **не собирается** —
`docker`-джоба зависит от тестов, поэтому «не выкачено» здесь = «не собрано из-за красных тестов».

## L4 · API/APIM — закрыт

`apim-auth` **#62** ✅ (канон входа), `apim-drift` #2 ✅ (12 API = 12 спек).

## L5 · деплой и смоуки (`deploy` #1785, `d0ce5f1d`)

| джоба | статус |
|---|---|
| `diff-paths` | 🟢 |
| `lock-published` | 🟢 (lock только из реестра — правка `19232425` держит) |
| `deploy-dev` | 🟢 |
| `smoke-api` | 🟢 |
| `receipt-be092` | 🟢 |
| `smoke-prod-cors` | 🟢 |
| **`smoke-front`** | 🔴 `add-partner` ✅ 5.6 с · «запись через визард» `timedOut` 1.3 м · `FE-014` ✘ 27.4 с · `FE-016` ✘ 4.7 с |

**Уточнение по `FE-014` (из `error-context.md` прогона #1786):** `# Page snapshot` показывает визард
на **первом шаге «Когда это было?»** (`textbox 2026-11-10T08:44`, кнопка «Далее»), **сетки `#w-partner-grid`
в снапшоте нет** → `.partner-card.selected` = 0 потому, что визард не дошёл до шага «Партнёр».
То есть это расхождение **контракта «тест × UI»**, а не «сломанный преселект».
По `FE-016` подтверждено фактом: в запросах визарда есть `GET /api/v1/journal/records` (полный журнал).
Заодно в CI добавлен шаг `e2e_triage` по `pw.log` — классы падений печатаются в лог, скачивать артефакт не нужно.
| `deploy-prod` | ⏭ skipped — корректно (гейт по `smoke-front`) |

**Транспорт E2E переделан (моя зона).** Раннер GitHub **не в tailnet**: браузер в CI получал
`Failed to fetch` на `https://xdiary-vps2.taile0b6ee.ts.net/api/v1/clients/me`, и приложение
уходило на lock-экран. Теперь `smoke-front` ходит через **SSH-туннель** к dev:
`XDIARY_E2E_BASE_URL=http://127.0.0.1:8080` + шаг, который проверяет `/health` **через туннель**.
В #1785 шаг туннеля — `success` («туннель поднят, /health отвечает (попытка 1)»).
Вывод: **красный `smoke-front` — реальный дефект визарда, а не сеть** (@FE).

Стенд dev на момент отчёта: **12/12** контейнеров = `images.lock`, `/health` **200**.

## L6 · E2E фронта

`ci-front` #935 🟢 (33 гейта). `front-e2e-repeat` **#18** 🔴, тоже через туннель:

| спек | результат |
|---|---|
| `add-partner` | **5/5** ✅ |
| `settings` | **4/5** ✅ (1 падение) |
| `csv` | **0/5** ❌ |
| `partner-edit` | **0/5** ❌ |

Падения теперь **быстрые** (1.3 с и 7.4 с), а не таймауты 1.7 мин: первая ошибка —
`csv.spec.js:34` (шаг «login по PIN»). То есть сетевой барьер снят, осталась логика
входа/состояния в самих спеках (**@TE**) — и, возможно, общий дефект логина, который надо
разделить с @BE.

## Что изменилось с прошлого отчёта (11:30)

1. `ci-backend` **#920 → #931**: красный → **зелёный** (L2 стало 11/12).
2. `guard-tools` **#238 → #241**: красный → **зелёный** (L0 стало 3/4).
3. `apim-auth` **#57 → #62**: зелёный, новый прогон.
4. Появился **новый красный гейт**: `check_adr_refs` `rc=1` — активные ADR ссылаются на
   отменённые (каталог `04-architecture`, зона @AR).
5. `smoke-front` и `front-e2e-repeat` переведены на SSH-туннель: tailnet больше не нужен,
   оба зелёные по шагу туннеля.
6. `deploy-prod` в #1785 — `skipped` (не «упал»): гейт по `smoke-front` работает как задумано.

## Остаток по владельцам

| пункт | владелец |
|---|---|
| `csv`/`partner-edit` падают на «login по PIN» (`csv.spec.js:34`), 0/5 каждый | **@TE** (и @BE, если дефект в логине) |
| визард: `FE-014` автоподстановка, `FE-016` полный журнал, таймаут «запись через визард» | **@FE** |
| `ci-journal` #131 — 5 тестов; из-за них не собирается образ с нормализацией `startedAt` | **@BE** |
| `ci-api-gateway` #20 — тесты сервиса | **@BE** |
| `chat/be.md:233` — секрет в чате: зачистка + ротация | **@BE** |
| `check_adr_refs` — 5 ссылок активных ADR на `ADR-007` (Отменён) и `ADR-029` (Отклонено) | **@AR** |
| `AWAIT.md` (регистр решений) + канон read-модель ⊆ DDL | **@TL** / **@AR**+**@LM** |
| `check_openapi` — breaking-diff требует `--generated/--base` (снимки спек) | **@BE** |

## Мои открытые пункты (чтобы не выглядело «всё закрыто»)

- `DO-111` — `На проверке`: осталось завести `DBPW_*` на VPS-1 и убрать переходный fallback.
- `DO-113` (digest в lock) — `Идея`: сегодня дважды видели, как тег и lock расходятся
  (`ac3962d` против `6d2d6d0` и наоборот); digest закрывает этот класс.
- `DO-114` — зачистка прод-баз выполнена, бэкап `~/backups/prod-cleanup/20261107-114708/`.
