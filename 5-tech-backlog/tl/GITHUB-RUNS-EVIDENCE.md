# GITHUB-RUNS-EVIDENCE — что реально было в прогонах (публичный API, без токена)

Собрано @TL 2026-11-13 02:30 (ревизия `60aed4750`). Все факты — из `api.github.com` по **публичному** репозиторию, без токена.

## Как повторить (команды)

```bash
# список прогонов workflow (с фильтром по событию)
curl -s "https://api.github.com/repos/Ekstrem/XDiary/actions/workflows/<файл>.yml/runs?per_page=3&event=workflow_dispatch"
# шаги конкретного прогона (нужен id прогона)
curl -s "https://api.github.com/repos/Ekstrem/XDiary/actions/runs/<run_id>/jobs"
# аннотации джобы (там лежат имена упавших тестов и текст ошибок!)
curl -s "https://api.github.com/repos/Ekstrem/XDiary/check-runs/<job_id>/annotations"
```

**Лимит:** без токена — **60 запросов в час** на IP. Ловится ответом `403 API rate limit exceeded`; тогда ждать или идти к `@DO` за токеном. В этой сессии лимит исчерпан на 4-м запросе после серии — учитывать при планировании.

## Что нашли (по прогонам)

| Прогон | Факт | Во что превратилось |
|---|---|---|
| `front-e2e-repeat` **#24** (run `43080701338`, sha `022aa7e13`, `workflow_dispatch`, **success**) | все 12 шагов зелёные; `022aa7e13` — **предок `main`**, содержит `wizard-offline`, `startup-api-error`, `pin-fallback`, `remove-pin` | REF-кандидат для `TE-053/054/104`. **Не хватает списка `specs`** (публичный API не отдаёт `inputs`; аннотации успешного прогона запросить не успели — лимит) |
| `front-e2e-repeat` **#25** (run `43081145961`, sha `3a796f6d`, **failure**) | `[flaky] падения: **5 failed из 5**`; спека `E2E: FE-046 — ошибка API на старте…`; сообщения **«тост пуст (старт с API=500)»**, `{"views":{}}`; `exit code 1` на шаге тестов | `TE-104`: критерий 3 `FE-046` **не выполнен**, правка `@FE` |
| `ci-backend` **#1097** (run `43078918968`, sha `caae6d07c`, **единственный** `workflow_dispatch`, **failure**) | шаг «Тесты изменённых BC» = failure, **1 мин 51 с** (тесты реально шли); аннотации: `CS7036` без `clientId` | `BE-098` (P1, @BE): **10 адресов** — `DailyMetricsTests.cs:25/26/27/28/50/57/64`, `CatalogTests.cs:17/44/51` |
| `ci-front` **#1070** (run `43012365536`, sha `b670eb2f`, `schedule`, **failure**) | `pageerror: Cannot read properties of null (reading 'getContext')` на `app.js:1724` (из `:1711`); прочие падения — таймауты 95–110 с и `toHaveCount(0)` на устаревшем коммите | `FE-048` (P2, @FE): нет guard'а на `null`-canvas |

## Что это меняет в картине

1. **«Прогонов нет» — неверно.** Прогонов много; вопрос — что в них исполняется и на каком коммите.
2. **«Пуш даст REF» — неверно:** браузерный джоб обрезан `if: event_name != 'push' && != 'pull_request'` (`ci-front.yml:50`), вход — только `dispatch`/`schedule`.
3. **«dispatch даст зелёный» — неверно, пока тесты красные:** `ci-backend` #1097 упал на компиляции тестов, а не на флаках.
4. **Аннотации — самый дешёвый источник имён:** имя упавшей спеки и текст ошибки лежат в `/check-runs/<job>/annotations`, а не только в логах (логи требуют токена, аннотации — нет).
5. **Статус шага ≠ статус процесса:** «Repeat run (list)» = `success` при `exit code 1` внутри — мой прошлый вывод «шаг итога краснеет при зелёных тестах» был ошибкой интерпретации (см. `DO-079`, отзыв).

## Чего в этом файле нет

- Логов прогонов (только аннотации и статусы шагов): текст логов публично недоступен.
- `inputs` dispatch-прогонов: API их не отдаёт в публичном объекте прогона.

## Добавлено @TL 2026-11-13 14:00 (ревизия `91998bd95`)

**`front-e2e-repeat` #26** (run `43086119519`, `workflow_dispatch`, **success**, sha `da4138c4`, `2026-11-11T19:19:28Z`, 2:34):
аннотации джобы `130750852296` — «**падений нет: 10 passed из 10**», и в них пошаговые ожидания `TE-053`
(`view-lock: visible` с локальным PIN; `view-lock: hidden` + `view-journal: visible` без него). Шаг «Артефакты падения»
стоит **после** «Итог по спекам» и на зелёном прогоне `skipped` — фикс `TE-105` работает с зелёной стороны;
красная сторона (артефакт при падении) не проверена: после `da4138c4` красных прогонов не было.

**`deploy.yml` #1970** (run `43090151275`, sha `c6526c99b`, `push`, **failure**, 32 с) — **релиз стоит на первом гейте**:
`diff-paths` success → **`lock-published` failure** на шаге «Что реестр реально может дать (не-front без образа → красный)» →
`smoke-prod-cors` success → **`deploy-dev`, `receipt-be092`, `smoke-front`, `smoke-api`, `deploy-prod` — skipped**.
То есть контрактный шаг `Journal.ApiTests → PartnerSoftDelete` не исполняется, и `TE-050` не может получить `REF` из деплоя.

**Реестр против lock** (`check_registry_tags.py --available`, прогон @TL):
`images.lock` ссылается на **один тег `single-ae29f58` для 11 сервисов** (`api-gateway single-50961be`, `front single-213b9b9`),
а доступные в реестре теги **разные**: `journal single-4b90af9`, `partners single-2003cf5`, `catalog`/`daily-metrics single-4d9b2f5`,
`client single-33c226c`, `privacy`/`analytics`/`somatic-series`/`garmin`/`apple-health single-c47ac4c`, `portability single-07a2426`.
Общий синтетический тег не публиковался — отсюда красный `lock-published`.

**Дефект инструмента (`TE-106`):** в песочнице `check_registry_tags.py --available` **падает в git-историю** и возвращает
**`rc=0`** («доступно 12, без образа 1 — front, Pages»), печатая только `::notice::`. То есть локальный прогон даёт **ложное зелёное**;
в CI тот же гейт красный. Фолбэк обязан быть `rc=2` «НЕ ИЗМЕРЕНО».
