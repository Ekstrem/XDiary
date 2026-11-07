# CI/CD по слоям — отчёт на 2026-11-07 12:10

Источник: `origin` = `19232425` (после моих правок), локальные прогоны гейтов + последние завершённые
прогоны Actions. Правило: слой закрыт, только когда **все** его гейты зелёные; «красный не блокирует» —
не основание считать слой закрытым.

## Сводка

| слой | состояние | красное (владелец) |
|---|---|---|
| **L0 · процесс** | 🟡 **2/4** | `ci-guards` — `AWAIT.md` (@TL) + `check_readstore_ddl` (@AR/@LM); `guard-tools` — манифест копий (@LM); локально `check_secrets` **rc=1** — 1 находка `chat/be.md:233` (@BE) |
| **L1 · контракт** | 🟡 **1/2** | `ci-api-gateway` #20 — «Тесты ApiGateway (в папке сервиса)» (@BE) |
| **L2 · тесты сервисов** | 🟡 **10/12** | `ci-journal` #130 и `ci-backend` #920 — тесты (@BE) |
| **L4 · API/APIM** | 🟢 **2/2** | — |
| **L5 · деплой/смоуки** | 🟡 **5/6** | `deploy` #1779: `lock-published` ✅, `deploy-dev` ✅, `smoke-api` ✅, `receipt-be092` ✅, `smoke-prod-cors` ✅; ❌ **`smoke-front`** (визард, @FE); `deploy-prod` — skip, корректно |
| **L6 · E2E фронта** | 🟡 **1/2** | `front-e2e-repeat` — исторический `cancelled` |

**Гейты локально:** 16 из 18 — `rc=0`. Красные: `check_secrets` — `rc=1` (1 находка, `chat/be.md:231`,
владелец @BE), `check_openapi` — `rc=2` («не измерено», нужен снимок от @BE).

## L0 · процессные гейты (быстрые, детерминированные)

| гейт | статус | что делает |
|---|---|---|
| `check_nul` | 🟢 | маркеры конфликта в `.md` (445+ файлов) |
| `check_canon` | 🟢 | единственный canon-маркер; **в CI вызывается** (добавлено 2026-11-07) |
| `check_docs_size` | 🟢 | бюджет инструкций (38 128 / 65 536; `AGENTS.md` 32 699 / 32 768) |
| `check_rule_docs` | 🟢 | личные пути/токены в доках (ошибок 0, остались `warn` про `$HOME`) |
| **`check_secrets`** | 🔴 **rc=1** | 1 находка: `5-tech-backlog/chat/be.md:231` — `assign:password`, len 8 (@BE; зачистка + ротация) |
| `status_lint --strict` | 🟢 | статусы и обязательные поля карточек |
| `chat_lint --strict-times` | 🟢 | формат/время меток чата |
| `stats --check` | 🟢 | таблица в `README.md` соответствует карточкам |
| `check_paths` | 🟢 | зоны ролей |
| `check_adr` / `_status` / `_refs` | 🟢 | ADR: шаблон, словарь статусов, ссылки |
| `guard-tools` (CI) | 🔴 | манифест копий `tools/` (@LM) |
| `ci-guards: AWAIT.md` (CI) | 🔴 | регистр выданных решений (@TL) |

## L1 · статические каноны контракта

| гейт | статус |
|---|---|
| `check_api_index` | 🟢 `82` операции / `82` маршрута, `--selftest` **8/8** (2026-11-07 исправлены два дефекта: `parse_index` не видел `request`, `parse_code` не считал анонимным маршрут при длинном наборе атрибутов) |
| `check_ddd_refs` (внутри `check_api_index`) | 🟢 пространства имён (`Partners.Storage`) больше не считаются ссылками |
| `gen_images_lock --check` / `--verify-registry` | 🟢 `rc=0` (12 опубликовано, 1 `unverified` — `front`, он идёт через Pages) |
| `check_ports`, `check_readstore_mapping` | 🟢 |
| `openapi-registry` (CI) | 🟢 #194 |
| `check_openapi` | 🟡 `rc=2` — «не измерено», нужен свежий снимок спек (@BE) |
| `ci-api-gateway` (CI) | 🔴 #20 — тесты в папке сервиса (@BE) |
| `check_readstore_ddl` (CI) | 🔴 «ни одной модели с DDL — фильтр сломан» (@AR/@LM, `DO-109`) |

## L2 · тесты сервисов (unit/integration) — 10/12

🟢 partners #120, catalog #39, somatic-series #41, daily-metrics #44, analytics #41, privacy #37,
portability #39, garmin #28, apple-health #28, client #62.

🔴 **journal #130** («Тесты Journal в папке сервиса») и **backend #920** («Тесты изменённых BC,
изоляция ADR-028») — оба на `d69c9253`, владелец @BE.

## L4 · API/APIM — закрыт

`apim-auth` #57 (канон входа `74/74 + 4/4`, `via=gravitee`), `apim-drift` #2 (12 API = 12 спек).

## L5 · деплой и смоуки

| шаг | статус |
|---|---|
| `lock-published` | 🔴 в #1774 → **исправлено**: `lock-sync` писал lock **из git** (`single-2f8c167` — таких тегов нет), из-за чего 12 «отсутствующих». Теперь режим один: `--from-registry --write` + `--verify-registry`; lock пересобран, `rc=0` |
| `deploy-dev` | 🟢 в #1765; проверит dev-оверлей E2E (`7-deploy/env/dev.e2e.env`) — флаг тестового OTP ставится деплоем, а не руками |
| `smoke-api` | 🟢 в #1765 |
| `receipt-be092` | 🟢 в #1765 (обёртка @TE `--mode test-otp`, `0/1/2`) |
| `smoke-prod-cors` | 🟢 |
| `smoke-front` | 🔴 в #1765 — визард (`timedOut`) и `FE-014`; на Pages уже `xdiary-v103`/`0.103`, нужен новый прогон (@FE/@TE) |
| `lock-sync`, `health`, `public-probe`, `backup-check` | 🟢 |

## L6 · E2E фронта

`ci-front` #924 — 🟢 (33 spec-гейта). `front-e2e-repeat` — `cancelled` от 2026-11-05, исторический.

## Что закрыто за последние сутки (проверено, не «обещано»)

1. **L1**: `check_api_index` — два дефекта гейта, `rc=0`/`8/8`, подтверждено в чистом worktree `99045715`.
2. **L5**: `deploy-dev` — `podman pull` и `image inspect` переведены на `sudo -n`; `say` → `echo`;
   доставка `images.lock`/скриптов/dev-оверлея в шаг деплоя (раньше «зелёный деплой при старом образе»).
3. **L5**: `deploy-prod` — добавлен отсутствовавший `actions/checkout` (иначе `awk: cannot open file`).
4. **L5**: `lock-sync` — git-режим удалён, lock только из реестра.
5. **`DO-111`**: пароли БД — один источник (`${DBPW_<SERVICE>}` + 11 ключей в `secrets/.env`), dev 12/12, `28P01` = 0.
6. **Тестовый OTP на dev** — причина в коде (`TestOtpPolicy.cs:8`: `Single` требует
   `Clients__AllowTestOtpOutsideDev=true`), теперь ставится деплоем.

## Остаток — по владельцам

| пункт | владелец |
|---|---|
| `chat/be.md:231` — секрет в чате: зачистить + ротация | **@BE** |
| `ci-journal` #130, `ci-backend` #920 — тесты | **@BE** |
| `ci-api-gateway` #20 — тесты сервиса | **@BE** |
| `check_readstore_ddl` — читать EF-миграции или снять гейт (`DO-109`) | **@AR** / **@LM** |
| `guard-tools` — манифест копий | **@LM** |
| `AWAIT.md` — регистр решений | **@TL** |
| `smoke-front` — визард + `FE-014` | **@FE** / **@TE** |
| `check_openapi` `rc=2` — снимки спек | **@BE** |

## Обновление: `deploy` #1779 (`b3c2eabd`) — L5 почти закрыт

| джоба | результат |
|---|---|
| `lock-published` | ✅ (после правки: lock только из реестра) |
| `deploy-dev` | ✅ |
| `smoke-api` | ✅ |
| `receipt-be092` | ✅ |
| `smoke-prod-cors` | ✅ |
| **`smoke-front`** | ❌ `исполнено 4, passed 1, failed 3, skipped 1` |
| `deploy-prod` | ⏭ skipped (гейт по `smoke-front` — корректно) |

**Единственный красный L5 — визард. Разбор по кейсам (из лога прогона):**

| кейс | статус | факт |
|---|---|---|
| `add-partner` | ✅ passed 8.6 s | приложение и вход работают |
| `wizard-record` (запись через визард) | ❌ `timedOut` 1.3 m | — |
| `wizard-record` (FE-014: автоподстановка) | ❌ assertion | `#w-partner-grid .partner-card.selected` → **0 элементов** за 20 с («последний партнёр должен быть выбран по умолчанию») |
| `wizard-record` (FE-016: не грузить полный журнал) | ❌ assertion | в запросах визарда есть `GET /api/v1/journal/records`; ожидались только `recent` + `places` |

**Pages в момент прогона:** `sw.js` → `xdiary-v103`, `js/app.js` → `APP_VERSION = "0.103"` (119 979 Б) —
то есть фикс таймаутов выкачен, а пункты `FE-014`/`FE-016` в этой версии **ещё не работают**.

**Куда смотреть (код фронта, зона @FE):** `8-front/js/app.js:813` — преселект строится как
`p.name === draft.partner` (при пустом `draft.partner` ни одна карточка не получает `selected`);
`8-front/js/api.js:152` — `list()` = `GET /journal/records`, и визард его вызывает.

**Итог по слоям:** L1/L2/L4 — как в таблице выше; L5 — 5 из 6 зелёные, красный один и он
продуктовый (визард), а не инфраструктурный; `deploy-prod` ждёт только его.
