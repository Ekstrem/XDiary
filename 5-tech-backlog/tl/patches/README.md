

## Статус применения на 2026-11-13 01:10 (читать ПЕРВЫМ)

| Патч | Статус | Куда |
|---|---|---|
| `check-awaits-future.patch` | ✅ **применён** @DO (`role/do` `f5401dd13`) | `tools/check_awaits.py` |
| `check-adr-refs-mention.patch` | ✅ **применён** @DO (`f5401dd13`) | `tools/check_adr_refs.py` |
| `ci-guards-adr-include-arch.patch` | ⚠️ **уже включено @DO напрямую** (`f5401dd13`, `ci-guards.yml:270`) — **отдельное применение НЕ требуется**; повторное применение даст конфликт (патч рассчитан на строку без флага). На `main` он по-прежнему применим — **страховка**, если `role/do` откатят | `.github/workflows/ci-guards.yml` |
| `concurrency-group-per-pr.patch` | ⏳ **ждёт @DO** (`DO-123`, P0: группа на PR) | `.github/workflows/ci-guards.yml`, `ci-backend.yml` |
| `wizard-record-duration.patch` | ✅ **взято в код @TE** (`5dd0efe0c`), прогон `43330703353` на его ветке — `success` | ~~`wizard-record.spec.js`~~ |
| `wizard-record-duration.patch` | ✅ **проверен прогоном `43326449735` (`success`)**, ждёт применения @TE | `9-tests/RnD/front-e2e/tests/wizard-record.spec.js` |
| `fe046-toast-timeout.patch` | ✅ проверен прогоном `43324408782`, **но перекрыт `role/te-toast` (#2176)** — не применять, ждёт применения @TE (`FE-046`: два уровня — `#toast` timeout 1500 в `api.js` + `actionTimeout: 15000` в конфиге) | `9-tests/RnD/front-e2e/` |
| `pr-create-dispatch-checks.patch` | ✅ `yaml`/`check_workflow_shape` ok, ждёт @DO (авто-PR без чеков) | `.github/workflows/pr-create.yml` |
| `check-chat-pyyaml-order.patch` | ✅ проверен локально (`gen_openapi --check` rc=0), ждёт @DO | `.github/workflows/ci-guards.yml` |
| `be104-portability-clientid.patch` | ✅ **проверен прогоном `43336316072`** (сборка 31/31 PASS), ждёт @BE | `6-msa/BC-Portability/.../PortabilityTests.cs` |
| `test-gates-pyyaml.patch` | ✅ **проверен прогоном `43336316072`** (`test-gates` success), ждёт @DO | `.github/workflows/ci-guards.yml` |
| `chat-shrink-guard-main.patch` | ⏳ **ждёт @DO** (маленький PR: гейт усыхания чата в `main`) | `tools/check_chat_shrink.py`, `.github/workflows/ci-guards.yml` |
| `merge-green-workflow.patch` | ⏳ **ждёт решения владельца/@LM** (авто-мерж зелёных PR) | `.github/workflows/merge-green.yml` (новый) |
| `ci-guards-test-gates-fetch.patch` | ⏳ **ждёт @DO** (`DO-099`: `fetch-depth: 0` иначе `origin/main` не резолвится) | `.github/workflows/ci-guards.yml` |
| `guard-paths-pyyaml.patch` | ⏳ **ждёт @DO** (`guard`: `check_workflow_paths` без PyYAML → rc=2) | `.github/workflows/guard-paths.yml` |
| `ci-guards-test-gates.patch` | ✅ **применён @DO 01:37** (`+34` строки, YAML ок, `jobs: 3`); в патче **8** гейтов, не 6 | `.github/workflows/ci-guards.yml` |
| `check-workflow-paths-no-yaml.patch` | ⏳ **ждёт @DO** (норма п.25: без PyYAML → rc=2) | `tools/check_workflow_paths.py` |
| `awaits-fix-future-stamps.patch` | ✅ **применён напрямую @TL 01:44 в `role/tl`** (5 меток → `01:09` + строка «Переиздано»); файл-патч оставлен для `main` — он там применяется по `--check rc=0` | `5-tech-backlog/tl/AWAIT.md` |

**Порядок (если применять с нуля):** `check-adr-refs-mention` → `ci-guards-adr-include-arch` → `check-awaits-future` (независим) → `awaits-fix-future-stamps` (@LM).

## `check-adr-refs-mention.patch` — @TL 2026-11-13 01:05 (→ @DO)

**Что:** `tools/check_adr_refs.py` перестаёт считать **опорой** ссылку, рядом с которой сказано, что решение снято.

**Зачем (дефект-плечо @AR 00:59):** на чистом `main` с `--include-arch` гейт давал **4 ложных** `::error::` — «опора на снятое 4»; все четыре — **упоминания** снятого, а не опора (например `adr-005.md:21` «не зафиксированы…: `ADR-029`», а «**отклонён (2026-11-14)**» — на следующей строке `:22`). Правило: опора только если рядом нет `отклонён|отменён|снят|не действует`.

**Ключевая деталь:** пометка может стоять на **следующей** строке, поэтому смотрится окно «текущая + следующая», а не одна строка.

**Проверено (обе стороны):**
- **норма** (`origin/main`, `--include-arch`) → **`rc=0`**, «опора на снятое **0**», «устаревших 18» (только `warnings`); было `rc=1` и 4 ложных;
- **дефект** — добавил в `adr-005.md` строку «Опираемся на `ADR-029` как на действующее решение» → **`rc=1`**, 1 `::error::` в `adr-005.md:34`;
- `--selftest` → **5/5**.

**Применение:** `git apply --ignore-whitespace 5-tech-backlog/tl/patches/check-adr-refs-mention.patch`. Источник правды — `..\LoopMaker\tools\check_adr_refs.py` (@LM); без переноса копии разойдутся.

### Независимая проверка @AR 2026-11-13 01:02 — патч годен

| Проверка | Результат |
|---|---|
| применение | `git apply --ignore-whitespace` → **rc=0** (чисто) |
| сторона дефекта | **rc=1**, ровно **5** `::error::` с адресами `:21…:25`, смещения **+1029 / +1509 / +968 / +548 / +1688** мин (совпало с его замером) |
| сторона нормы | те же метки сдвинуты в прошлое → ошибок «В БУДУЩЕМ» **0** |
| `--selftest` | **6/6** |

**Цена вопроса (замер @AR):** после сдвига меток `check_awaits --strict` остаётся `rc=1`, но «висит **10**» вместо 15 — то есть 5 строк были неисправимы **по построению**, а 10 — реально неотвеченные решения.

**Срок по правилу передачи (владелец 2026-11-12):** патч передан @DO 2026-11-13 01:00. Если не применён за час игрового времени (до **02:00**), координатор применяет сам с пометкой «по прямому указанию владельца» и записью в карточке.

### Проверка «патчи не ломают набор гейтов» (@TL 2026-11-13 01:05)

Оба патча применены в **чистом** `worktree` на `origin/main` (`git apply --ignore-whitespace` → rc=0 каждый), затем прогнан набор:

| Гейт | rc |
|---|---|
| `check_adr_refs.py` | **0** (было `--include-arch` rc=1 с 4 ложными) |
| `check_adr_refs.py --include-arch` | **0** |
| `check_adr.py` | 0 |
| `check_nul.py` | 0 |
| `status_lint.py --strict` | 0 |
| `stats.py --check` | 0 |
| `check_api_index.py` | 0 |
| `check_secrets.py --config loop.config.json` | 0 |
| `check_workflow_paths.py` | **1** — предсуществующий (в `main` нет `paths` в `pr-create.yml`; фикс в `role/do`, `#2165`) |
| `check_awaits.py --strict` | **1** — ожидаемый (15 висящих `AWAIT`) |

Вывод: **патчи не добавляют ни одного нового красного**; оба оставшихся красных — уже документированные блокеры.

**Долг (не блокирует применение):** в патчах нет `--selftest`-кейсов для новой логики (@AR предлагал 4 кейса для `adr_refs`). Добавить при переносе в `LoopMaker` — иначе самотест не покрывает то, что патч меняет.

## `ci-guards-adr-include-arch.patch` — @TL 2026-11-13 01:08 (→ @DO)

**Что:** `ci-guards.yml` запускает `check_adr_refs.py --include-arch` вместо вызова без флага.

**Зачем (факт @AR 01:03):** `ci-guards.yml:239` вызывал гейт **без** флага, а `accepted_refs_rejected()` работает только при флаге (`:177-180`) ⇒ связность ADR→ADR («опора на снятое») в CI **не проверялась вовсе**: 7 ссылок вместо 19. То есть гейт существовал, а проверки не было.

### ⚠️ Порядок применения обязателен

| # | Патч | Почему первым |
|---|---|---|
| 1 | `check-adr-refs-mention.patch` | иначе `--include-arch` даст **4 ложных** `::error::` (упоминания снятого) |
| 2 | `ci-guards-adr-include-arch.patch` | включает ту проверку, которую чинит первый патч |
| — | `check-awaits-future.patch` | независим, можно в любом порядке |

**Проверено (все три применены):** `check_adr_refs.py --include-arch` → **`rc=0`**, «проверено ссылок **19**, нарушений 0 | связность: опора на снятое **0**, устаревших **18**»; `yaml.safe_load(ci-guards.yml)` → ok.

**Остаётся решением @LM:** 18 `warnings` «ссылается на заменённое» — это не ошибки, но их стоит держать отдельным пунктом, а не в шуме.

## ✅ Применено @DO 2026-11-13 01:05 — до дедлайна (коммит `f5401dd13` в `role/do`)

@DO: «оба патча применены до дедлайна, `git apply --ignore-whitespace` → **rc=0**. Брал их из `origin/role/tl` (в `main` их ещё нет), в свой коммит патч-файлы **не** включал — это твоя зона».

**Проверка обеих сторон (его замер, независимый от моего):**

| Гейт | Результат |
|---|---|
| `check_awaits --selftest` | **6/6** |
| `check_awaits --strict` | **rc=1**, ровно **5** `::error::` «метка выдачи В БУДУЩЕМ» на `:21–:25`, смещения **+1026/+1506/+965/+545/+1685** мин (у меня +1029/… — разница ровно в минутах между замерами) |
| `check_adr_refs --selftest` | **5/5** |
| `check_adr_refs --include-arch` | **rc=0**, «опора на снятое **0**», 18 `warnings` — 4 ложных сняты, настоящая опора по-прежнему краснеет |

**Третий патч (`ci-guards-adr-include-arch`) реализован им напрямую в `ci-guards.yml`** с соблюдением порядка («сначала патч „упоминание ≠ опора“, потом включение — иначе CI стал бы красным навсегда»). Гейты после: `check_yaml_dups` rc=0, `check_workflow_shape` rc=0 (43 workflow), `check_workflow_paths` rc=0.

**Итог:** срок по правилу передачи не понадобился — патчи применены за ~5 минут вместо часа.

## `awaits-fix-future-stamps.patch` — @TL 2026-11-13 01:10 (→ **@LM**, данные его зоны)

**Что:** приводит 5 меток выдачи в `5-tech-backlog/tl/AWAIT.md` к реальному времени (`2026-11-13 01:09`) и добавляет строку «Переиздано…» с причиной.

**Зачем:** эти 5 строк имели метку **в будущем** (18:11, 02:11, 17:10, 10:10, 05:10 при машине 01:09), поэтому подтверждение по ним было невозможно **по построению** — `chat_accepts` ищет ответ **после** метки.

**Проверено (одна сторона — вторая невозможна по природе данных):**
- до правки: `check_awaits --strict` → «висит **15**», из них 5 с `::error::` «метка выдачи В БУДУЩЕМ»;
- после правки: `::error::` «В БУДУЩЕМ» — **0**, «висит **10**» (ровно те 10, что ждут решений по существу).

**Почему патч, а не правка руками:** `AWAIT.md` — файл координатора, но решения в нём **выданы @LM**; менять их молча нельзя. Патч даёт ему действие в одну команду (`git apply --ignore-whitespace`) и сохраняет авторство изменения видимым (строка «Переиздано…, автор правки — координатор, ожидает слова @LM»).

## `check-workflow-paths-no-yaml.patch` — @TL 2026-11-13 01:14 (→ @DO)

**Что:** `check_workflow_paths.py` при отсутствии PyYAML возвращает **`rc=2`** с `::error::`, а не `rc=0` + «пропускаю».

**Зачем (норма @AR п.25 «нет зависимости ⇒ `rc=2`»):** сейчас `:68-70` печатает «PyYAML не установлен — пропускаю» и возвращает **0**. То есть `guard-paths` **зелёный на пустоте** — ровно та же болезнь, что PyYAML у `gen_openapi` (но там громко), и что `--include-arch` у `check_adr_refs` (тихо).

**Проверено обеими сторонами (симуляция отсутствия пакета через `PYTHONPATH`-шим, который роняет `import yaml`):**

| Прогон | До патча | После |
|---|---|---|
| **без PyYAML** | `rc=0`, stderr «пропускаю» | **`rc=2`**, `::error::check_workflow_paths: PyYAML не установлен — НЕ ИЗМЕРЕНО (rc=2)` |
| **с PyYAML** (норма) | `rc=1` — реальная находка `pr-create.yml: on.push без paths` | `rc=1` — **не изменилось** |

**Применение:** `git apply --ignore-whitespace 5-tech-backlog/tl/patches/check-workflow-paths-no-yaml.patch`. После — норма п.25 должна быть применена ко всем гейтам с `except ImportError: return 0` (@LM решает, @DO правит копии).

### Долг «нет `--selftest`-кейсов» закрыт @DO 2026-11-13 01:08 (`694c172ee`)

| Инструмент | Было | Стало | Новые кейсы |
|---|---|---|---|
| `check_adr_refs.py --selftest` | 5/5 | **9/9** | опора без пометки → ошибка; пометка «отклонён» на той же строке → не ошибка; пометка на **следующей** (окно) → не ошибка; «заменён» → `stale`, а не error |
| `check_awaits.py --selftest` | 6/6 | **8/8** | метка в будущем ловится (>1000 мин); прошедшая метка за будущую не принимается |

**Проверено @TL независимо на `origin/role/do`:** `--selftest` → **9/9** и **8/8** (rc=0), `check_workflow_paths.py` → «OK (у всех тяжёлых workflow есть фильтр путей)», `check_adr_refs --include-arch` → rc=0, «проверено 19, нарушений 0». Заявления @DO подтверждены.

## `concurrency-group-per-pr.patch` — @TL 2026-11-13 01:18 (→ @DO, `DO-123` P0)

**Что:** `ci-guards.yml` и `ci-backend.yml` получают группу concurrency **на PR/ветку**:

```yaml
group: ci-guards-${{ github.event.pull_request.number || github.ref }}
group: ci-tests-test-${{ github.event.pull_request.number || github.ref }}
```

**Зачем:** сейчас группы — `ci-guards` и `ci-tests-test` **без привязки к PR**, то есть все открытые PR делят одну очередь. Аннотации: #2169 `test` → «Canceling since a higher priority waiting request for **ci-tests-test** exists»; #2174 `check-chat` → «… for **ci-guards**». При 8 открытых PR чеки отменяются **механически**, и красный PR означает «проиграл гонку», а не «плохой код».

**Почему `cancel-in-progress: false` (DO-122) не помог:** он меняет «отменить running» на «в очередь», но GitHub всё равно **отменяет pending**-прогон при новом запросе в ту же группу.

**Образец уже был в репозитории:** `status-lint.yml:17`, `docs-budget.yml:24`, `guard-tools.yml:18` — там группа с `pull_request.number || github.ref`.

**Проверено:** YAML обоих файлов — `yaml.safe_load` **ok**; `check_workflow_shape` → **rc=0** («workflow 43, сломанных 0»). Настоящая проверка — два пуша в разные ветки подряд: оба прогона должны дойти до конца.

### Полный перечень затронутых workflow (замер @TL 01:19)

Прогон по всем `.github/workflows/*.yml`: группы **без привязки к PR/ветке**, у workflow с триггером `pull_request:` — **ровно три**:

| Workflow | Группа | В патче? | Почему |
|---|---|---|---|
| `ci-guards.yml` | `ci-guards` | **да** | лёгкий гейт, общего состояния не трогает → группу на PR безопасно |
| `ci-backend.yml` | `ci-tests-test` | **да** | то же |
| `ci-front.yml` | `front-e2e-stand` | **НЕТ, осознанно** | эта группа **намеренно** глобальная: браузерные прогоны мутируют **общий dev-стенд** (причина `DO-032`). Группа на PR разрешила бы два прогона по стенду одновременно — вернули бы `DO-032`. Здесь нужен другой механизм сериализации (очередь/лок в шагах), а не смена группы |

Вывод: «распространить практику на остальные 15» — не нужно; дефект ровно в двух, третий **нельзя** лечить так же. Это тот случай, когда одинаковый симптом требует **разного** лечения.

### Проверка свежести всех патчей на `origin/main` (`988e96133`, 2026-11-13 01:20)

`git apply --check --ignore-whitespace` для каждого файла — **rc=0** у всех шести:

| Патч | apply --check |
|---|---|
| `check-awaits-future.patch` | **0** |
| `check-adr-refs-mention.patch` | **0** |
| `ci-guards-adr-include-arch.patch` | **0** |
| `awaits-fix-future-stamps.patch` | **0** |
| `check-workflow-paths-no-yaml.patch` | **0** |
| `concurrency-group-per-pr.patch` | **0** |

То есть ни один патч не устарел, несмотря на десятки коммитов бота в `main` после их создания. Тот, кто берёт патч по реестру, получит `rc=0` на применении, а не конфликт.

### Канонический счёт групп (@TL 01:24, сверено с @AR 01:22)

**Всего групп — 21. Без привязки к PR/ref — 18. Раскладка ровно 18 = 3 + 6 + 9:**

| Класс | Сколько | Файлы | Мерж блокируют? |
|---|---|---|---|
| **PR-триггер** | **3** | `ci-guards:25`, `ci-backend:36`, `ci-front:55` | **да** — создают чеки на PR |
| push только в `main` | **6** | `deploy`, `lock-sync`, `openapi-registry`, `pages`, `stats`, `sync-board` | нет (чеков на PR не создают) |
| без push/PR (расписание) | **9** | `apim-drift`, `backup-check`, `canon-stability`, `front-e2e-repeat`, `health`, `probe-watchdog`, `public-probe`, `read-model-check`, `sweep` | нет |

**Охват шестого патча — 2 из 2 безопасных** (`ci-guards`, `ci-backend`); `ci-front` — намеренно глобальная (`DO-032`), остальные 15 не создают PR-чеков.

> Исправление: в моих сообщениях `01:20–01:21` числа не сходились («13 без привязки», «остальные 10» при перечисленных 15 файлах) — это были три разных счёта. Канон — **18 = 3 + 6 + 9**; на действие это не влияет, но в реестре должно стоять одно число.

## `ci-guards-test-gates.patch` — @TL 2026-11-13 01:29 (→ @DO, `DO-099` P1)

**Что:** добавляет в `ci-guards.yml` job `test-gates` — шесть тестовых гейтов, каждый сначала `--selftest`, затем реальный прогон.

**Зачем:** из 11 гейтов `9-tests/scripts` в CI запускались **3**; остальные 8 жили «на бумаге» (ручная дисциплина). Шесть из них проверены на `origin/main` — `--selftest` **rc=0** и прогон **rc=0**: `check_adr028_move`, `check_read_paths`, `check_front_088`, `check_e2e_selectors`, `check_e2e_traceability`, `check_e2e_no_sleeps`.

**Проверено:** `yaml.safe_load` → ok (`jobs: 3`, `test-gates` есть); `check_workflow_shape` → **rc=0** («workflow 43, сломанных 0»); `check_workflow_paths` → rc=1 по **предсуществующему** `pr-create.yml`, не из патча.

**Не входит:** `check_swagger_perimeter` (нужны `--public-base`/`--internal-base`) и `e2e_receipt` (нужен вывод Playwright) — отдельное решение @DO.

## `merge-green-workflow.patch` — @TL 2026-11-13 01:35 (→ **владелец / @LM**, не @DO)

**Что:** новый `.github/workflows/merge-green.yml` — раз в 10 минут (и по кнопке) сливает **только** те PR, у которых **все** чеки `pass`/`skipping`. Любой `fail`/`pending`/`cancelled` — PR пропускается.

**Зачем:** `pr-create.yml` **создаёт** PR, но шага мержа в репозитории нет: `git grep "pr merge|auto-merge|merge_queue" .github/workflows` → только `lock-sync.yml` (про lock). То есть «мерж обслуживает бот» (PDR-047) кодом **не подтверждается** — мержит человек.

**Почему это не «обход гейтов»:** `gh pr merge --squash` **без** `--admin` — branch protection и merge queue действуют; workflow лишь снимает необходимость ручного клика. Пока `check-chat` красный, ни один PR не сольётся.

**Проверено:** `yaml.safe_load` → ok (`jobs: ['merge']`); `check_workflow_shape` → **rc=0** («workflow 44, сломанных 0»).

**Это изменение процесса**, поэтому адресат — владелец/@LM, а не @DO: появляется автоматический мерж, чего в правилах сейчас нет. Один патч = одно решение.

### Что @DO сделал сверх патча 2026-11-13 01:37

- **`check_otp_leak` подключён в `deploy.yml`** — шаг в `smoke-front` после «Туннель к dev», где `127.0.0.1:8080` уже поднят: `--selftest` (**14/14**) + проба `--base … --contract …`. Это закрывает дыру, которую я нашёл в `DO-099`: гейт утечки OTP не запускался **нигде**.
- **Прогноз `11` проверен прямым замером** (`check_awaits.check()` с искусственным `now`): `01:50` → висит **10** `{lm: 10}`; `02:10` → висит **11** `{be: 1, lm: 10}`. Совпадает с двумя мирами из п.29: 10 — это «после патча данных», 11 — «после + 60-минутное окно».

### Почему `check-workflow-paths-no-yaml.patch` НЕ дубль установки PyYAML

@DO ставит PyYAML шагом в `ci-guards` (это чинит `gen_openapi --check` и `check_yaml_dups`) — **это правильно и остаётся**. Мой патч про другое: `check_workflow_paths.py:68-70` при **отсутствии** yaml печатает «пропускаю» и возвращает **`rc=0`**, то есть гейт **зеленеет на пустоте**. Установка пакета лечит симптом сегодня; `rc=2` делает провал **громким** навсегда (если шаг установки переедет/упадёт — увидим `rc=2`, а не тихий зелёный).

**Оба нужны:** установка (иначе гейт не работает) + `rc=2` (иначе не работает незаметно).

### Правка данных применена в ветке 2026-11-13 01:44 (замер)

Патч-ФАЙЛ на мою ветку уже не ложится (в `AWAIT.md` появились мои пояснения — контекст сместился), поэтому правки применены **напрямую**, те же 5 меток → `2026-11-13 01:09` + строка «Переиздано…».

**Замер после:** `python tools/check_awaits.py --strict` (гейт **старой** версии, без `TOPIC_SINCE`) → «висит **10**», разбивка **`lm` 10**. Значит:
- строки `@be` (ответ `01:36`) и `@te` (ответы `01:15`) зачитываются **даже старым гейтом** — слово принятия после метки есть;
- две строки `@do` тоже не висят (у @DO есть свои строки после `01:09`);
- остаются **10 решений `@lm`** — та же цифра, что дают гейт с `TOPIC_SINCE` и симуляции @DO/@TE.

**Итог:** три независимых способа (старый гейт, новый гейт @DO, симуляции `now`) дают одно число — **10**.

## `fe046-toast-timeout.patch` — @TL 2026-11-13 01:54 (→ @TE, `FE-046` P0)

**Что:** в `9-tests/RnD/front-e2e/tests/api.js` три чтения тоста получают явный таймаут `{ timeout: 1500 }`:
`:752` (`reportStep`), `:790` (`expectScreen`), `:855` (`stepGuard`).

**Зачем (корень найден по трассе прогона `43322907200`):** `locator.innerText()` **без timeout** ждёт **до таймаута теста (90 с)**, если `#toast` не существует. Именно это вешало `startup-api-error`:
- `error-context.md` → `Test timeout of 90000ms exceeded`;
- `1-trace.trace`: `Get inner text` с `locator('#toast')` на `5.6 с`, следующее событие — на `95.5 с` (разрыв 90 с);
- затем `localStorage` → `SecurityError: Access is denied` (документ уже `about:blank`) и `goto … Target page, context or browser has been closed`;
- `.catch(() => '')` **не спасает**: ловит ошибку **после** 90 секунд, а не отменяет ожидание.

**Проверено:** `node --check api.js` → **rc=0**; других безтаймаутных чтений `#toast` в файле нет (`getElementById` внутри `evaluate` — не ожидания).

**Ожидаемый эффект:** прогон **дойдёт до ассертов**, и `FE-046` либо закроется, либо покажет настоящий продуктовый дефект вместо таймаута обвязки.

### Обновление 01:55 — патч `fe046-toast-timeout` расширен до **двух уровней** (по @AR `01:51`)

| Уровень | Что | Файл |
|---|---|---|
| **экземпляр** | три чтения тоста получают `{ timeout: 1500 }` | `9-tests/RnD/front-e2e/tests/api.js` (`:752`, `:790`, `:855`) |
| **класс** | `actionTimeout: Number(process.env.XDIARY_E2E_ACTION_TIMEOUT \|\| 15000)` в блоке `use:` | `9-tests/RnD/front-e2e/playwright.config.js` |

**Почему класс важнее экземпляра (@AR):** в конфиге есть `timeout: 90_000` (тест) и `expect: {timeout: 10000}`, но **`actionTimeout` отсутствует** — значит **любое** действие без явного таймаута (`innerText`, `click`, `fill`…) ждёт 90 секунд. Одна такая правка закрывает не три вызова, а весь класс.

**Проверено:** `node --check` на обоих файлах → **rc=0**; патч 49 строк; остальные безтаймаутные чтения `#toast` в `api.js` отсутствуют.

### Проверен прогоном 43324408782 (2026-11-13 02:02)

Ветка `verify/fe046` (от `main` + этот патч), `front-e2e-repeat` на спеке `tests/startup-api-error.spec.js` → **`success`**, шаг «Repeat run (list)» **`success`**, шаг «Артефакты падения» **`skipped`**. До патча тот же прогон (`43322907200`) — `failure` с `Test timeout of 90000ms exceeded`. То есть патч не «теоретически верный», а **проверенный на стенде**.

## `wizard-record-duration.patch` — @TL 2026-11-13 02:12 (→ @TE, `smoke-front` / `wizard-record`)

**Что:** в `9-tests/RnD/front-e2e/tests/wizard-record.spec.js` в хелпер `next()` добавлена синхронизация шага: если виден `#w-dur-grid` (шаг «Сколько длилось?»), клик «далее» на переходе, отличном от `6 -> 7`, **пропускается** с записью в лог.

**Зачем:** продукт (`8-front/js/app.js:801`) не пускает дальше без `durationMin >= 1` — `toast("Укажите длительность")`. Спека считает, что делает `5 -> 6`, но мастер **уже** на шаге длительности, поэтому клик блокировался тостом, а `fill #w-dur-custom` (следующая строка) до своего шага не доходил.

**Проверено прогонами (без мержа, `gh workflow run --ref`):**

| Ветка | Что | Результат |
|---|---|---|
| `main` | без правок | `failure`: «ШАГ ВИЗАРДА: 5 -> 6 ;; ТОСТЫ: ["Укажите длительность"]» |
| `verify/wizard3` | автозаполнение чипом | `failure` дальше: «fill #w-dur-custom» (шаг уже уехал вперёд) |
| **`verify/wizard4`** | **пропуск лишнего клика** | **`success`** — `run 43326449735` |

`node --check` → rc=0. Это и есть падение `smoke-front` (та же спека, `wizard-record.spec.js:46`).

### Уточнение по «дефект-плечу» (@AR 02:12 — принято; замер 02:20)

@AR прав: мои три вызова обёрнуты `.catch(() => '')`, поэтому **ошибка таймаута превращается в пустую строку** — «дефект-плечо» через этот путь невозможно по построению, и я не имел права выводить из прогона `success` утверждение «сработал уровень 1.5 с, а не 15 с». Моё утверждение снято.

**Что измерено вместо этого** (проба `zz-timeout-probe.spec.js`, ветка `verify/timeout-probe`, run **`43327358779`**, спека `passed`):

```
::notice::probe: msg=locator.click: Timeout 15000ms exceeded. Call listing...
                 waiting for locator('#definitely-absent-element')  ;; dur=15s
```

Действие **без явного таймаута** упало ровно за **15 с** с текстом `Timeout 15000ms` — значит `actionTimeout: 15000` из конфига **применяется** (это и есть «уровень класса»). До правки тот же путь ждал бы 90 с (таймаут теста).

**Уровень экземпляра** (`innerText({timeout: 1500})`) отдельным дефект-плечом **не изолирован** — честная формулировка: его эффект виден как «спека доходит до ассертов вместо 90-секундного висения», а не как отдельное измеренное число.

### Сверка с `role/te-toast` 02:23 — мой `fe046-toast-timeout` **перекрыт, применять не нужно**

Проверил ветку @TE (`origin/role/te-toast`, PR **#2176**): в `9-tests/RnD/front-e2e/tests/api.js` **безтаймаутных** `innerText()` не осталось (**0**; в `main` — **3**), и в `playwright.config.js` есть `actionTimeout: Number(process.env.XDIARY_E2E_ACTION_TIMEOUT || 15000)`. То есть покрытие полное, включая `stepGuard`, и с его же комментариями-пояснениями.

**Вывод:** `fe046-toast-timeout.patch` — **superseded** (оставлен как страховка и как запись проверки прогоном `43324408782`). Дублировать не нужно.

**Но `wizard-record-duration.patch` в `role/te-toast` отсутствует** (`#w-dur-grid` в его ветке — 0 вхождений), поэтому второе падение `smoke-front` пока не закрыто: патч проверен прогоном `43326449735` и ждёт @TE.

### ✅ Оба уровня таймаута измерены (run `43328851817`, 02:27)

Проба `zz-timeout-probe.spec.js` (ветка `verify/timeout-probe`) — два действия **мимо** `.catch`, лог дословно:

```
::notice::probe-class:    locator.click:     Timeout 15000ms exceeded … ;; dur=15s
::notice::probe-instance: locator.innerText: Timeout  1500ms exceeded … ;; dur=1.5s
```

| Уровень | Значение | Откуда | Измерено |
|---|---|---|---|
| класс | **15 000 мс** | `actionTimeout` в `playwright.config.js` | **да** (`dur=15s`) |
| экземпляр | **1 500 мс** | `innerText({ timeout: 1500 })` | **да** (`dur=1.5s`) |

**Почему проба, а не дефект-плечо в спеке (@TE `verify/fe046-defect-strict`, run `43328445809`):** тот прогон **прошёл** (`success`), потому что при зелёном прогоне хелперы путей падения (`reportStep`/`stepGuard`) **не вызываются вовсе** — убирать в них `.catch` бессмысленно. Нужен вызов, который исполняется **всегда**; проба и есть такой вызов.

### Расширено 02:33 — закрыт **четвёртый** безтаймаутный вызов (нашёл @AR 02:24)

@AR провёл инвентарь: в `role/te-toast` безтаймаутным оставался `wizard-record.spec.js:98` — `const stepText = () => page.locator('#view-wizard').innerText();`. Класс «90 с» был закрыт **3 из 4**.

Патч теперь содержит **два** изменения в `wizard-record.spec.js`:

| Что | Строка |
|---|---|
| `stepText()` → `.innerText({ timeout: 1500 })` | `:98` |
| синхронизация шага в `next()` (пропуск лишнего клика, когда виден `#w-dur-grid`) | `next()` |

**Проверено:** `node --check` rc=0; прогон **`43329396963`** (ветка `verify/wizard5`, спека `wizard-record`) → **`success`**. То есть и «класс 4 из 4», и падение `smoke-front` закрываются одним патчем.

## Разбор 4 красных на `#2165` (02:40) — три механические, одна содержательная

`gh pr checks 2165` → `guard-tools`, `guard`, `test-gates`, `check-chat`. Причины, по логам, а не по догадке:

| Чек | Причина (дословно из лога) | Чей | Фикс |
|---|---|---|---|
| **`test-gates`** | `::error:: ADR-028: REF='origin/main' не резолвится в этом репозитории` → rc=2 | **мой патч** (`ci-guards-test-gates`) | **`ci-guards-test-gates-fetch.patch`**: `fetch-depth: 0` в checkout этого job |
| **`guard`** | `check_workflow_paths: PyYAML не установлен — НЕ ИЗМЕРЕНО (rc=2), гейт не проверил ни одного workflow` | зависимость: `guard-paths.yml` **не ставит PyYAML** (раньше гейт молча возвращал 0 — ровно то, что мой патч и вскрыл) | **`guard-paths-pyyaml.patch`**: `setup-python` + `pip install pyyaml` в этом job |
| **`guard-tools`** | `sync_tools: расхождение с манифестом (1): check_workflow_paths.py: правлен без sync` | @DO | `python tools/sync_tools.py` / обновить `tools/.sources.json` |
| **`check-chat`** | `AWAIT.md` (10 решений `@lm`) + норма «статус ↔ строка чата» | @LM | содержательное, не механическое |

**Важно:** `guard` красный **не из-за находки**, а из-за отсутствия PyYAML — это ровно та «зелёная на пустоте» дыра, которую закрывал `check-workflow-paths-no-yaml.patch`. То есть патч работает: он превратил молчаливое `rc=0` в честное `rc=2`, и сразу вылезла незакрытая зависимость.

**Проверено:** оба патча — `yaml.safe_load` ok (`ci-guards: jobs [check-chat, guard-secrets, test-gates]`, `guard-paths: steps 7`).

### ✅ 02:42 — оба патча `FE-046`/`wizard-record` **приняты в код @TE** (`role/te-toast`, `5dd0efe0c`), и `smoke-front` проверен на его ветке

@TE взял правки **не патчами, а в код ветки** (патч конфликтовал бы с его уже сделанным `stepText`), и подтвердил, что мой `fe046-toast-timeout` снимается как перекрытый.

**Свежий прогон на его ветке** (это проверка не моей ветки-доказательства, а **той, что пойдёт в `main`**):

```
gh workflow run front-e2e-repeat.yml --ref role/te-toast -f specs=tests/wizard-record.spec.js
→ run 43330703353 → status: completed | conclusion: SUCCESS | job repeat -> success
```

Ранее та же спека на `main` давала `failure` («ШАГ ВИЗАРДА: 5 -> 6 ;; ТОСТЫ: ["Укажите длительность"]»). Очередь стенда перед запуском проверена правилом @DO: `in_progress: 0`.

**Итог:** `smoke-front` после мержа `#2176` зелёный по обеим причинам — таймауты (`api.js` 3 + `stepText` 1 + `actionTimeout`) и рассинхрон шага визарда.

**Статусы:** `fe046-toast-timeout.patch` — superseded; `wizard-record-duration.patch` — absorbed (`5dd0efe0c`). Оба остаются в реестре как запись проверок.

## `chat-shrink-guard-main.patch` — @TL 2026-11-13 03:12 (→ @DO, решение @LM)

**Что:** **маленький отдельный PR** (2 файла) — `tools/check_chat_shrink.py` (185 строк, из `role/do`) + шаг в `ci-guards.yml` (`check-chat`): `--selftest`, затем `--range <base>..HEAD`, где `base` = `pull_request.base.sha || github.event.before` с откатом на `HEAD~1`.

**Зачем:** за вечер **три** инцидента потери чата (`00:34` −81, `39773f010` −546, затем повторные откаты `fe`/`te`) и один случай, когда бот закоммитил **неразрешённый merge** (маркеры в `do.md`/`te.md`). Гейт лежит в `#2165` (19 файлов) и ждёт мержа; отдельный PR снимает риск уже сегодня — ровно вариант (1), который поддержали @TE и @AR.

**Проверено (все три состояния):**

| Состояние | Команда | rc |
|---|---|---|
| самотест | `check_chat_shrink.py --selftest` | **0** («6/6») |
| дефект-плечо (инцидент) | `--range c5764163f..39773f010` | **1** («усохло файлов 5 из 7») |
| норма (после мержа) | `--range <base>..HEAD` | 0 |

YAML: `yaml.safe_load` → ok, `ci-guards: jobs [check-chat, guard-secrets]`, в `check-chat` 30 шагов (было 29).

## ✅ 03:25 — `test-gates` ПОЗЕЛЕНЕЛ на проверочной ветке: два фикса подтверждены в CI

Ветка `verify/tg-py` = `role/do` + **`test-gates-pyyaml`** + форматтер @TE + **`be104-portability-clientid`**. Прогон `ci-guards` **`43336316072`**:

| Job | Результат |
|---|---|
| **`guard-secrets`** | success |
| **`test-gates`** | **success** |
| `check-chat` | failure — **только** «Регистр выданных решений не зарастает» (`AWAIT`, 10 решений `@lm`) |

**Что это доказывает:**

1. **`BE-104` исправлен:** в логе `10) сборка тестовых проектов PASS (нарушений 0, собрано 31, не проверено 0)` — до патча было `FAIL (1 проект не собирается)` с `CS7036`.
2. **`test-gates` требовал PyYAML:** без него `check_api_index_optional --strict` печатал `[warning] не измерено … (No module named 'yaml')` и валил шаг; с `pip install pyyaml` — проходит.

**Патчи:**

| Файл | Что |
|---|---|
| `be104-portability-clientid.patch` | `PortabilityTests.cs`: `ClientId` (Guid) в трёх вызовах `ImportRecords`/`ExportRecords` — адресат **@BE** |
| `test-gates-pyyaml.patch` | `ci-guards.yml`: `pip install pyyaml` в job `test-gates` — адресат **@DO** |

**Важно про приём:** локально `dotnet build` этого проекта даёт «Ошибка сборки, Ошибок: 0» (restore недоступен), поэтому проверка возможна **только в CI** — и она сделана.

## `check-chat-pyyaml-order.patch` — @TL 2026-11-13 03:34 (→ @DO)

**Что:** одна строка в шаг «Реестр API ↔ код — DO-064» (`ci-guards.yml`): `python -c "import yaml" || pip install --quiet pyyaml` **до** `gen_openapi.py --check`.

**Зачем:** в `main` PyYAML ставится **только в следующем** шаге (AR-024, стр. 155), а `gen_openapi --check` читает YAML **раньше** (стр. 147). Поэтому на любой ветке, где пакета нет, шаг падает с `No module named 'yaml'` — именно это красное у `#2174` (шаг «Реестр API ↔ код — DO-064»).

**Проверено локально:** `gen_openapi.py --check` → **rc=0** («спек 12, операций 78, расхождений 0»), `check_api_index.py --require-request` → **rc=0**; `yaml.safe_load` файла workflow → ok.

**Итог:** вместе с `be104-portability-clientid` и `test-gates-pyyaml` это закрывает **все** технические красные на `#2165` и на `#2174`; остаётся `check_status_chat` (арбитраж @LM).

## `pr-create-dispatch-checks.patch` — @TL 2026-11-13 03:42 (→ @DO, к `DO-119`)

**Проблема (нашёл @AR 03:40):** PR, созданный через `GITHUB_TOKEN`, **не порождает прогонов workflow** — анти-рекурсия GitHub («events triggered by the GITHUB_TOKEN will not create a new workflow run»). То есть фикс `DO-119` (`GH_TOKEN: github.token`) даёт **PR без чеков**, а не рабочий транспорт: `check_paths`, `check-chat`, `test` на нём просто не появятся.

**Что делает патч:** в `pr-create.yml`
1. `permissions:` += `actions: write`;
2. шаг создания получает `id: create` и пишет `created=true|false` в `$GITHUB_OUTPUT`;
3. новый шаг «Dispatch checks» при `created == 'true'` запускает `gh workflow run ci-guards.yml` и `ci-backend.yml` с `--ref "$GITHUB_REF_NAME"`.

**Проверено:** `yaml.safe_load` → ok (`permissions` содержит `actions: write`; шаги: checkout, Create PR, Dispatch checks); `check_workflow_shape` → **rc=0** («workflow 43, сломанных 0»). Приём `--ref` уже проверен сегодня прогонами `43336316072` и `43333979036`.

**Адресат:** @DO (`.github` его зона). Без этого `#2165` снимает только «PR не создаётся», но оставляет «PR без проверок».

### Правка 03:45 — dispatch на КАЖДОМ пуше, а не только при создании (нашёл @AR 03:43)

**Дефект первой версии:** условие `if: created == 'true'` делало фикс одноразовым — при следующем пуше в ту же ветку PR уже существует ⇒ `created=false` ⇒ dispatch пропускается ⇒ **новый SHA без чеков**. То есть болезнь возвращалась на втором пуше.

**Исправлено:** шаг всегда пишет `has_pr=true` (и когда PR создан, и когда уже был), а условие dispatch — `steps.create.outputs.has_pr == 'true'`.

**Проверено:** `yaml.safe_load` ok (`permissions` с `actions: write`; шаги: checkout, Create PR, Dispatch checks; `if` у последнего = `steps.create.outputs.has_pr == 'true'`); `check_workflow_shape` → **rc=0** (43 workflow, 0 сломанных); `workflow_dispatch` есть в `ci-guards.yml`, `ci-backend.yml`, `ci-front.yml` (проверено).

### Правка 03:49 — вернул dispatch **только на создании** (проверил возражение @AR замером)

@AR возразил (`03:43`), что `created == 'true'` делает фикс одноразовым: «второй пуш ⇒ PR уже есть ⇒ нет чеков». Проверил фактом — **возражение не подтверждается**:

```
gh run list --workflow ci-guards.yml → role/tl:
43337919127 00:32:52 pull_request
43339117812 00:41:57 pull_request
43339240650 00:42:51 pull_request
43339455114 00:44:26 pull_request
43339627615 00:45:42 pull_request
```

Каждый мой пуш в `role/tl` порождал прогон с `event=pull_request`. Анти-рекурсия GitHub относится **только** к событиям, вызванным `GITHUB_TOKEN` — то есть к самому созданию PR. Последующие пуши делают люди/агенты, и `pull_request: synchronize` запускает чеки штатно.

**Поэтому dispatch нужен РОВНО ОДИН РАЗ — на создании.** Dispatch на каждом пуше дал бы **дубли** прогонов, а при общих группах `concurrency` — ещё и `cancelled` (класс `DO-123`). Вернул `created == 'true'`, добавил в комментарий замер выше, чтобы следующий читатель не «починил» это снова.

`yaml.safe_load` ok; `check_workflow_shape` → rc=0.

### Финальная редакция 03:50 — «только при создании» + **семь** workflow

Объединены две внешние проверки:

| От кого | Что | Как учтено |
|---|---|---|
@TL (замер `gh run list`) | пуши в `role/tl` **уже** дают `event=pull_request` ⇒ dispatch нужен ровно один раз | `created == 'true'` (а не `has_pr` в обеих ветках) |
@AR (замер триггеров) | dispatch покрывал 2 workflow из 7; у авто-PR не было бы `guard`/`guard-tools`/`docs-budget`/`status-lint`/`ci-front` | цикл по **семи**: `ci-guards`, `ci-backend`, `ci-front`, `guard-paths`, `guard-tools`, `docs-budget`, `status-lint` |

**Проверено:** `yaml.safe_load` ok (`if: steps.create.outputs.created == 'true'`), `check_workflow_shape` → rc=0; **у всех семи** есть и `workflow_dispatch`, и `pull_request` (иначе dispatch был бы невалиден).

**Почему не «на каждом пуше»:** dispatch запускает тот же workflow на той же ветке, где уже идёт PR-прогон, а группа `concurrency: ci-guards` в `main` **без привязки к ref** ⇒ второй вытесняет первый ⇒ `cancelled` (класс `DO-123`).

## 18. `deploy-prod-deliver-update-sh.patch` — доставка `update.sh` на VPS-1 (DO-124, P0)

**Адресат:** @DO (`.github/workflows/deploy.yml`). **Выдан:** 2026-11-13 11:3x. **Статус:** ждёт применения.

**Дефект (факт из прогона `43379615221`, шаг «Deploy prod (VPS-1)»):**
```
APIM_PREFLIGHT_OK
/opt/xdiary/7-deploy/update.sh: line 59: say: command not found
/home/<deploy>/<script>.sh разошёлся со снимком (sha256 9173778a… != 7b62a233…)
```
**Причина:** dev-джоб **доставляет** скрипт (`UPDATE_B64` → `base64 -d > /opt/xdiary/7-deploy/update.sh`, строки 137/155), а prod-джоб просто вызывает `bash /home/deploy/update.sh` (строка 490) — на VPS-1 остаётся старая копия. В git `say()` определена (строки 40–56), а строка 59 — комментарий ⇒ ошибка возможна только на устаревшей копии.

**Что делает патч:** в prod-джобе добавляет шаг `Подготовить update.sh (base64 …)` и в ssh-скрипте — доставку файла + `chmod +x` + контроль `grep -q 'tag_of'`. `envs` расширены до `TAG,CODE_TS,UPDATE_B64`.

**Проверено:** `yaml.safe_load` ok (steps: Tag → Подготовить → Deploy; `envs: TAG, CODE_TS, UPDATE_B64`), `check_workflow_shape` → **rc=0** (43/0), `git apply --check` → **rc=0**.

**После применения:** первый `deploy` на `main` должен дать `deploy-prod` **success**; если сторож эталона всё ещё ругается — прогнать `bash /opt/xdiary/7-deploy/legacy-shims.sh` (пересоберёт шим и эталон детерминированно, файл бэкапится в `/home/deploy/legacy-<ts>/`).

## 19. `smoke-front-api-readiness.patch` — ждать готовность API dev, а не только `/health` (TE-110, P1)

**Адресат:** @DO (`.github/workflows/deploy.yml`). **Выдан:** 2026-11-13 11:5x. **Статус:** ждёт применения.

**Дефект (замер):** `front-e2e-repeat` на `main`, `add-partner`, `repeat=3` → **1 passed / 2 failed**; оба падения с текстом
`Вход не завершился … views={"view-onboarding":"visible"} ошибки=["http 502 http://127.0.0.1:8080/api/v1/clients", "502 (Bad Gateway)"]`.
Механизм: `deploy-dev` пересоздаёт контейнеры, `Туннель к dev` ждёт `/health` (**Caddy**), а сервис в это время ещё не готов → `/api/v1/clients` отдаёт 502; красный `smoke-front` через `needs` гасит `receipt-be092`.

**Что делает патч:** новый шаг `Готовность API dev (не 5xx на /api/v1/clients/me)` сразу после туннеля: до 60 попыток по 3 с, готовность = любой код кроме `5xx`/`000` (`GET /api/v1/clients/me` анонимно → `401`, это и есть «сервис жив»), без создания данных.

**Проверено:** `yaml.safe_load` ok (**8 jobs**, `smoke-front` — 12 шагов), `check_workflow_shape` → **rc=0** (43/0), `git apply --check` → **rc=0**.

**После применения:** первый `deploy` на `main` должен дать `smoke-front` **success** без повторов; в логе появится строка `API dev готов: GET /api/v1/clients/me -> 401 (попытка N)`.

### Проверка предположения патча №19 на живом стенде (2026-11-13 12:0x)

Шаг ждёт «не 5xx», и ожидаемый код `401` подтверждён **снаружи**, а не по документации:

```
curl -s -o /dev/null -w '%{http_code}' https://xdiary-vps2.taile0b6ee.ts.net/health          -> 200
curl -s -o /dev/null -w '%{http_code}' https://xdiary-vps2.taile0b6ee.ts.net/api/v1/clients/me -> 401
curl -s -o /dev/null -w '%{http_code}' https://xdiary-vps2.taile0b6ee.ts.net/api/v1/catalog    -> 401
```

То есть `401` на `/api/v1/clients/me` — это штатное «сервис жив, аноним не авторизован», и проба не создаёт данных. Если в окне пересоздания контейнеров будет `502`, шаг подождёт, а не упадёт.

## 20. `deploy-prod-deliver-legacy-shims.patch` — доставить `legacy-shims.sh` на VPS-1 (DO-124 ч.2, P0)

**Адресат:** @DO. **Применён координатором** 2026-11-13 12:1x по указанию владельца.

**Второй слой того же блокера.** После правки @DO (доставка `images.lock`/`update.sh`/`recreate-service.sh`) лог прогона `43398330290` показал:

```
APIM_PREFLIGHT_OK
DO-124: скрипты обновлены из git (update.sh 332 строк)
Process exited with status 1
/home/deploy/update.sh разошёлся со снимком (sha256 9173778a… != 7b62a233…)
```

То есть `/opt/xdiary/7-deploy/update.sh` уже свежий, но **шим** `/home/deploy/update.sh` остался старым и падает на своём стороже **до** вызова живого скрипта. Причина: шаг «пересоздать шимы» был условным (`if [ -f …/legacy-shims.sh ]`), а этого файла на VPS-1 **нет** — доставка трёх файлов его не покрывала.

**Что делает патч:** добавляет `LEGACY_B64` в шаг подготовки, доставляет `legacy-shims.sh` рядом с остальными, проверяет маркер `DO-066` и **безусловно** пересоздаёт шимы (`make_shim` пишет новый шим, а он `exec`-ит живой скрипт — сторожа на нём уже нет).

**Проверено:** `yaml.safe_load` ok (8 jobs), `envs: TAG,CODE_TS,LOCK_B64,UPDATE_B64,RECREATE_B64,LEGACY_B64`, `check_workflow_shape` 43/0, `check_yaml_dups` — 0 дублей.

## 21. `smoke-front-readiness-deep-probe.patch` — готовность dev должна доходить ДО сервиса (P0, блокирует deploy-prod)

**Адресат:** @DO (`.github/workflows/deploy.yml`). **Выдан:** 2026-11-13 18:0x. **Статус:** ждёт применения.

**Дефект (замер, не гипотеза).** Прогон `43447361176` (sha `5f7bdca4`): `smoke-front` ✅, но `smoke-api` ❌ и `receipt-be092` ❌, `deploy-prod` skipped.
Из лога `receipt-be092` (job `131962378438`): `POST /clients {phone: +799…54} → 502` → `check_be092_delivery: занятый номер: ожидали 409 + clientId, получили 502`.
Из лога `smoke-api`: `канон входа: не измерено ДВАЖДЫ (rc=2)`.

**Причина.** Шаг «Готовность API dev» проверяет `GET /api/v1/clients` и `GET /api/v1/clients/me` — оба отвечает **шлюз** (401 до авторизации), поэтому проба зеленеет, пока сам **сервис** `clients` ещё не поднялся. `POST /clients` доходит до сервиса и получает 502.

**Что делает патч.** Добавляет третью пробу — `POST /api/v1/clients` с пустым телом `{}`; ожидание — **400** (валидация из сервиса), 5xx/000 = ждём. Проверено на живом dev: `POST /clients {}` → **400** (`The Phone field is required.`), то есть проба доходит до сервиса и не создаёт данных.

**Проверено:** `yaml.safe_load` ok (8 jobs), `check_workflow_shape` — 43/0.

## 22. `release-lock-pr-via-bot.patch` — `release.yml` не может создать PR сам (P0, финал прод-выкатки)

**Адресат:** @DO (`.github/workflows/release.yml`). **Выдан:** 2026-11-13 18:1x.

**Дефект (замер).** Все прогоны `release.yml` падают (43448709708, 43447376705, 43446872279, 43445592672). Лог:
образы собираются и пушатся в GHCR, затем
`pull request create failed: GraphQL: Resource not accessible by integration (createPullRequest)` → `exit code 1`.
Две причины сразу: (1) у workflow `permissions` = `contents: write` + `packages: write`, **без `pull-requests: write`**;
(2) ветка называлась `release/lock-$SHA`, а бот-создатель PR слушает только `role/**`.

**Что делает патч:** пушить в `role/release-lock-$SHA` и **не** звать `gh pr create` — PR и чеки создаёт `pr-create.yml`, как для всех ролей (PDR-047 сохраняется, прямого пуша в `main` нет).

**Проверено:** `yaml.safe_load` ok, `check_workflow_shape` — 44/0, `gh pr create` в файле больше нет.

## 23. `recreate-service-exclude-own-ip.patch` — `client` получал `--add-host clients:<свой ip>` (DO-128, P0)

**Адресат:** @DO (`7-deploy/recreate-service.sh`). **Выдан:** 2026-11-13 18:4x.

**Арифметика дефекта (сходится с логом без остатка).** Генератор `gen-hosts.py --add-host` даёт 11 записей: 10 сервисов + псевдоним `clients:10.89.9.31` (он есть в `deploy.sh:78` у шлюза). `gen_expected` вычитает **своё имя** (`grep -v "^${svc}:"`):
- для `journal` — вычитается `journal:…` → остаётся **10** (так в логе);
- для `client` — вычитается `client:…`, **которого в списке нет** (есть `clients:` — псевдоним шлюза) → остаётся **11**, и в `podman run` клиента попадает `--add-host clients:10.89.9.31` рядом с его же `--ip 10.89.9.31`.

Отсюда `попытка 1/2/3 не удалась` → `client не поднялся` (класс «error configuring network namespace», уже описан в этом же файле, строка 224).

**Что делает патч:** в `gen_expected` берём IP сервиса из `deploy.sh` (`--ip`) и вычитаем **любой псевдоним, указывающий на свой IP** — закрывает и `clients:`, и будущие такие же алиасы.

**Проверка:** файл — bash-скрипт; локального bash в этой среде нет, поэтому `bash -n` не выполнялся. Изменение — только внутри функции `gen_expected` (heredoc и awk в отдельных строках), логика фильтра — три `grep -v` в одном конвейере.

## 24. `prod-gen-hosts-and-mktemp.patch` — доставить `gen-hosts.py` на прод + уникальные tmp-файлы (DO-129, P0)

**Адресат:** @DO. **Выдан:** 2026-11-13 18:5x.

**Два дефекта из прод-прогона `43455070581`** (он впервые дошёл до пересоздания сервисов: все теги `single-c0552f2` прочитаны из lock):

1. **`gen-hosts.py` не доставлялся на VPS-1.** Локально `python tools/gen-hosts.py --add-host` даёт **12** записей (10 сервисов + `clients:10.89.9.31` + `client:10.89.9.31`), а на dev в прогоне было 11 (Journal → «10 хостов»), на проде — 12 (journal → «11 хостов»). То есть генератор на стендах **разный**: он живёт только на VPS (`/opt/xdiary/tools/gen-hosts.py`) и никогда не доставлялся. Патч добавляет `GENHOSTS_B64` и запись `/opt/xdiary/tools/gen-hosts.py` из git в прод-джобе.
2. **Откат падал на фиксированных путях:** `PermissionError: [Errno 13] Permission denied: '/tmp/recreate-partners.body'` → `откат partners не удался (нужно вручную)`, и то же для `catalog`. Файл остаётся от прошлого запуска с другим владельцем. Патч заменяет фиксированные `BODY`/`RUNNER` на `mktemp "/tmp/recreate-$SVC.*.XXXXXX"`.

**Проверено:** `yaml.safe_load` ok, `envs` содержит `GENHOSTS_B64`, `check_workflow_shape` ok, `bash -n 7-deploy/recreate-service.sh` → rc=0.
