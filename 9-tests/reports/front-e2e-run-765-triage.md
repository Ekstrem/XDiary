# Разбор живого прогона `Front E2E` (run #765, REF `32138913`, 2026-11-06 01:11)

Источник — не пересказ, а **скачанные данные**: лог джобы (`##[error]`-аннотации репортёров) и
артефакт `front-e2e-results` (`receipt.txt`). Команда, которой это воспроизводится:

```bash
# лог и артефакт берутся через API GitHub (нужен токен), затем:
python 9-tests/scripts/e2e_triage.py --input run-log.txt
```

## Итог прогона

`receipt.txt` из артефакта: **passed 7, failed 19, flaky 0, skipped 2** (28 тестов, 13.9 мин).
Скипнуты: `change-pin` (флаг `XDIARY_CHANGE_PIN` не ставится) и LEGACY `no-pin`.

## Что уже работает (факты из лога)

- **Кэш сессии работает:** `##[notice]{"title":"seedSession","cached":true}` и
  `login fastPath: after-seed, elapsedMs 1095` — вход ~1.1 с вместо ~3.0 с (первый тест показал
  `elapsedMs 3050, seedMs 2556` на полном онбординге).
- **Гейты до браузера проходят** (в этом прогоне шаг `E2E-гейты по спекам` зелёный).
- **Пропуски объяснены** и видны в квитанции как `skipped 2`.

## Разбор по классам (`e2e_triage.py`)

| класс | сколько | примеры (спека) | самый долгий шаг | лечение |
|---|---|---|---|---|
| `test-timeout` | 16 | E2E: CSV — экспорт журнала и импорт Garmin-CSV, E2E: запись через визард — создаётся и видна через API, E2E: каталог contraception — добавить, увидеть в списке, удалить, E2E: каталог costume — добавить, увидеть в списке, удалить | архивировать через UI (4.2s) | лимит теста израсходован: смотри длительности шагов; сокращать время входа/навигации, а не увеличивать ожидания |
| `session-lost` | 6 | E2E: деталь записи — соматика из API, при ошибке видно сообщение (FE-013), E2E: запись журнала — редактирование и удаление через UI, E2E: тренды — открываются и рендерятся без ошибок консоли | Evaluate (2.4s) | сессию убили на стенде (401/404): очистка стенда или параллельный прогон; фикстура делает один повтор с новым телефоном |
| `assert` | 16 | E2E: link-client/unlink-client — партнёр привязан к клиенту (TE-027), E2E: partner-edit — rename и profile через UI, проверка API, E2E: read-модель партнёров не пуста при живых данных (BE-071), E2E: stale-note — нет на свежих данных, есть на устаревшем кэше (read-paths §5 п.2) | login по PIN (3.1s) | ожидание не совпало: дефект продукта ИЛИ неверное ожидание в спеке — смотреть данные |
| `browser-noise` | 98 | browser console.error, browser http error, browser requestfailed | — | шум диагностики браузера/фикстуры, а не отдельное падение — смотреть, к какому тесту относится |

итого аннотаций: 136 | классов: 4 | по классам: assert=16, browser-noise=98, session-lost=6, test-timeout=16

## Причины по каждому упавшему тесту

| Тест | Статус | Где остановился | Причина (из аннотации) |
|---|---|---|---|
| E2E: CSV — экспорт журнала и импорт Garmin-CSV | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. | Error: page.click: Test timeout of 90000ms exceeded. Call log:   - waiting for locator('#tab-settings')     - locator resolved to <button class= |
| E2E: link-client/unlink-client — партнёр привязан к клиенту  | failed | Before Hooks (0.1s) | Error: POST /partners/{id}/link-client expect(received).toBeLessThan(expected) Expected: < 300 Received:   400 |
| E2E: partner-edit — rename и profile через UI, проверка API | failed | Before Hooks (0.1s) | Error: expect(locator).toBeVisible() failed Locator:  locator('#view-partners') Expected: visible Received: hidden Timeout:  30000ms Call log:   - Expect "toBeVisible" locator('#vi |
| E2E: read-модель партнёров не пуста при живых данных (BE-071 | failed | Before Hooks (0.1s) | Error: создание партнёра не удалось: 504  expect(received).toContain(expected) // indexOf Expected value: 504 Received array: [200, 201] |
| E2E: stale-note — нет на свежих данных, есть на устаревшем к | failed | Before Hooks (0.1s) | Error: данные взяты из устаревшего кэша, но бейдж `#stale-note` не показан — правило §5 п.2 не выполнено expect(locator).toHaveCount(expected) failed Locator:  locator('#view-wizar |
| E2E: без локального PIN замка нет; с локальным PIN замок поя | failed | Before Hooks (0.1s) | Error: expect(locator).toBeVisible() failed Locator:  locator('#view-onboarding').or(locator('#view-journal')).first() Expected: visible Received: hidden Timeout:  20000ms Call log |
| E2E: визард подставляет последние партнёра и место (FE-014) | failed | Before Hooks (0.1s) | Error: фикстура: {"ok":false,"step":"when","status":404,"partnerId":"9b2e26b2-acda-4242-8d21-6534292efb00","recordId":"08a600ab-3e5c-4eef-92b4-9f5d51b3832c"} expect(received).toBe( |
| E2E: деталь записи — соматика из API, при ошибке видно сообщ | failed | Before Hooks (0.1s) | Error: page.evaluate: Error: /api/v1/journal/records/b02c709e-090a-4d37-85c2-1cb7cad68dc0/when -> 404     at post (eval at evaluate (:370:30), <anonymous>:17:24)     at async eval  |
| E2E: запись журнала — редактирование и удаление через UI | failed | Before Hooks (0.1s) | Error: page.evaluate: Error: /api/v1/journal/records/61cb8815-c146-4281-8329-8672b41b3853/when -> 404     at post (eval at evaluate (:370:30), <anonymous>:17:24)     at async eval  |
| E2E: запись через визард — создаётся и видна через API | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. | Error: page.click: Test timeout of 90000ms exceeded. Browser logs: <launching> /home/runner/.cache/ms-playwright/chromium_headless_shell-1248/ch |
| E2E: каталог contraception — добавить, увидеть в списке, уда | timedOut | Before Hooks (0.1s) | Test timeout of 90000ms exceeded. | Error: page.fill: Test timeout of 90000ms exceeded. Browser logs: <launching> /home/runner/.cache/ms-playwright/chromium_headless_shell-1248/chr |
| E2E: каталог costume — добавить, увидеть в списке, удалить | timedOut | Before Hooks (0.1s) | Test timeout of 90000ms exceeded. |
| E2E: каталог fantasy — добавить, увидеть в списке, удалить | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. |
| E2E: каталог — archive и restore через UI, проверка API | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. |
| E2E: настройки — пол сохраняется после перезагрузки | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. |
| E2E: новое устройство → онбординг (без PIN-экрана) → журнал | failed | Before Hooks (0.1s) | Error: expect(locator).toBeVisible() failed Locator:  locator('#view-lock').or(locator('#view-onboarding')).or(locator('#view-journal')).first() Expected: visible Received: hidden  |
| E2E: онбординг E7 — телефон → OTP → «Кто вы?» (имя + пол) →  | timedOut | Before Hooks (0.0s) | Test timeout of 90000ms exceeded. | Error: page.addInitScript: Target page, context or browser has been closed |
| E2E: открытие визарда не грузит полный журнал (FE-016) | failed | Before Hooks (0.1s) | Error: визард не должен грузить полный журнал (FE-016); запросы: GET /api/v1/partners | GET /api/v1/journal/recent | GET /api/v1/journal/places | GET /api/v1/catalog | GET /api/v1/ |
| E2E: тренды — открываются и рендерятся без ошибок консоли | failed | Before Hooks (0.1s) | Error: консольные ошибки: ["console: Failed to load resource: the server responded with a status of 401 ()"] ;; API: 200 POST https://xdiary-vps2.taile0b6ee.ts.net/api/v1/clients | |

## Что это значит для приёмки (адресаты)

1. **@BE — дефекты API (4 теста, повторяемые):**
   `POST /api/v1/journal/records/{id}/when` → **404** (`edit-delete-record`, `somatic-detail`,
   `wizard-record`); `POST /api/v1/partners/{id}/link-client` → **400** (`partner-client-link`);
   `POST /api/v1/partners` → **504** (`partners-read-parity`).
2. **@FE — приложение не показывает ни одного экрана (3 теста):** `onboarding`, `pin-first-login`,
   `pin-fallback` падают на «`#view-onboarding`/`#view-lock`/`#view-journal` все hidden». Это не
   «не дождались медленного UI»: экранов нет вообще.
3. **@FE — поведение:** после rename карточка `#view-partners` не появляется (`partner-edit`);
   бейдж `#stale-note` не показан при устаревшем кэше (`stale-note`, правило read-paths §5 п.2);
   визард грузит полный журнал (`wizard-record` FE-016).
4. **@DO — уборка не работает:** в логе `cleanup partner: {"ok":false,"removed":false,"status":400}` —
   `DELETE /partners/{id}` отдаёт 400, значит тестовые партнёры копятся на стенде.
5. **@TE (исправлено в этом прогоне):** `trends` падал из-за **самой фикстуры** — анонимная проба
   `GET /partners` шла из страницы и давала `console error 401`, а спека требует «ошибок консоли нет».
   Проба перенесена в Node (`TE-064`), консоль страницы больше не засоряется.
6. **@TE — сверх лимита теста (16 из 19):** почти все падения — `Test timeout of 90000ms exceeded`,
   то есть тест не успел. Это подтверждает решение `TE-062`: лимиты по спекам + лимит джобы.

## Почему это не «флейк»

Падения воспроизводимы по причине: 404/400/504 — это ответы API, «все экраны hidden» — состояние
приложения, а не таймаут ожидания. Флейк в этом прогоне: `flaky 0`.

## Прогон #766 (REF `c71616e1`): что изменилось и ГДЕ именно висит

Второй разобранный прогон (первый — #765). Квитанция: **passed 8, failed 19, flaky 0, skipped 2**
(#765 было 7/19/0/2 → +1 passed). Падения те же по составу — значит это **не флейк**, а устойчивые
причины; ниже — шаг, на котором тест провисел лимит (из `steps` аннотации, секунды в скобках).

| Тест | Статус | Шаг, на котором повис / причина |
|---|---|---|
| E2E: CSV — экспорт журнала и импорт Garmin-CSV | timedOut | импорт: Garmin-подобный CSV (somatic_minute) принимается (77.6s) [FAIL] |
| E2E: link-client/unlink-client — партнёр привязан к клие | failed | link-client: clientId появляется в read-model (0.8s) [FAIL] |
| E2E: partner-edit — rename и profile через UI, проверка  | failed | UI: изменить имя, notes и пол → Сохранить (30.2s) [FAIL] |
| E2E: stale-note — нет на свежих данных, есть на устаревш | failed | Evaluate (0.0s) [FAIL] |
| E2E: визард подставляет последние партнёра и место (FE-0 | failed | Evaluate (0.0s) [FAIL] |
| E2E: деталь записи — соматика из API, при ошибке видно с | failed | setup: запись через API (0.9s) [FAIL] |
| E2E: запись журнала — редактирование и удаление через UI | failed | Evaluate (0.0s) [FAIL] |
| E2E: запись через визард — создаётся и видна через API | timedOut | Evaluate (0.0s) [FAIL] |
| E2E: каталог contraception — добавить, увидеть в списке, | timedOut | Evaluate (0.0s) [FAIL] |
| E2E: каталог costume — добавить, увидеть в списке, удали | timedOut | Evaluate (0.0s) [FAIL] |
| E2E: каталог fantasy — добавить, увидеть в списке, удали | timedOut | Evaluate (0.0s) [FAIL] |
| E2E: каталог — archive и restore через UI, проверка API | timedOut | удалить позицию каталога через UI (#btn-catalog-delete) (80.2s) [FAIL] |
| E2E: локальный PIN есть   замок, неверный PIN даёт ошибк | timedOut | Evaluate (0.0s) [FAIL] |
| E2E: настройки — пол сохраняется после перезагрузки | timedOut | вернуть «Мужской» (уборка) (86.1s) [FAIL] |
| E2E: новое устройство → онбординг (без PIN-экрана) → жур | failed | после reload устройство помнит вход (журнал или PIN — оба допустимы) (20.1s) [FAIL] |
| E2E: онбординг E7 — телефон → OTP → «Кто вы?» (имя + пол | timedOut | чистое устройство → экран онбординга (90.0s) [FAIL] |
| E2E: открытие визарда не грузит полный журнал (FE-016) | failed | Evaluate (0.0s) [FAIL] |
| E2E: тренды — открываются и рендерятся без ошибок консол | failed | ошибок в консоли нет (0.0s) [FAIL] |
| E2E: чистый профиль без локального PIN   журнал открыт,  | failed | Evaluate (0.0s) [FAIL] |

### Что из этого следует (по адресатам)

1. **@FE — приложение зависает, а не «медленно отвечает».** Восемь падений — это UI-действия, которые
   не завершились за 80–90 с: `Fill`/`Click` по визарду и каталогу, а `онбординг` вообще не показал
   `#view-onboarding` за 90 с. В снимках `views` после засева устойчиво `view-splash: visible`, остальные
   `hidden` — то есть сплэш не разрешается, когда API отвечает ошибкой/висит. Это и объясняет «все экраны
   hidden» в `pin-first-login` (после reload 20 с) и `pin-fallback`.
2. **@FE — `#btn-catalog-delete`:** шаг «удалить позицию каталога через UI» висел **80.2 с**
   (`catalog-archive`). Отдельно: в `partner-edit` шаг «изменить имя, notes и пол → Сохранить» висел
   **30.2 с**, после чего `#view-partners` не появился.
3. **@BE — те же дефекты API, что и в #765:** `POST /journal/records/{id}/when` → **404** (три теста),
   `POST /partners/{id}/link-client` → **400**, `POST /partners` → **504**. Два прогона подряд — не случайность.
4. **@FE — поведение:** `#stale-note` не показывается при устаревшем кэше (правило read-paths §5 п.2);
   визард грузит полный журнал (FE-016).
5. **@TE — `trends`** падал из-за **нашей** фикстуры (анонимная проба давала `console error 401`).
   Исправлено в `TE-064`, фикс попал в прогоны #770+ — проверить на следующем завершившемся прогоне.
6. **@DO — уборка:** `cleanup partner: {"ok":false,"removed":false,"status":400}` и шаг «вернуть
   "Мужской" (уборка)» висел **86.1 с** — уборка не работает, данные копятся, а тесты тратят на неё лимит.

### Чего в этих данных нет

- `flaky 0` в обоих прогонах: повторных «зелёных после красного» нет, то есть **нестабильности не видно**;
  видно устойчивые дефекты. Это важно: до экономии времени их путали с «медленным UI».
- Время входа больше не является узким местом: `login fastPath: after-seed, elapsedMs 1095`,
  полный засев — `3050 мс`. Узкое место — шаги UI после ошибки API.

## КОРНЕВАЯ ПРИЧИНА: прогоны идут ПАРАЛЛЕЛЬНО на одном стенде, и очистка удаляет чужие данные

Это измерено по API GitHub (job steps с временами), а не выведено рассуждением.

| Прогон | Ждать стенд | **Очистить** | **Front E2E (тесты)** |
|---|---|---|---|
| #766 | 01:03:45 | **01:03:46–01:03:55** | **01:14:12 → 01:30:53** |
| #767 | 01:13:28 | **01:13:29–01:13:37** | **01:14:46 → 01:35:41** |
| #768 | 01:16:51 | **01:16:51–01:16:58** | **01:18:10 → 01:35:45** |
| #770 | 01:23:56 | **01:23:57–01:24:05** | **01:25:36 → …** |

Что из этого следует буквально:

1. **Три прогона одновременно тестировали один стенд** (окна #766/#767/#768 пересекаются с 01:18 до 01:30).
2. **Очистка одного прогона попадала ВНУТРЬ тестов другого:** очистка #768 в `01:16:51` удалила всех
   клиентов `+7999…` и партнёров `TE-…`, пока #766 (начал тесты в `01:14:12`) ещё работал; очистка #770 в
   `01:23:57` — внутрь #767/#768.
3. Отсюда «странные» дефекты API: `POST /journal/records/{id}/when` → **404**, `POST /partners/{id}/link-client`
   → **400**, `#view-partners` пусто — это **данные, удалённые другим прогоном посреди теста**, а не
   обязательно дефект BE. Формулировка «404 = дефект BE» была бы неверным выводом из этих прогонов.
4. Длительность: #766 — **16.7 мин**, #767 — **20.9 мин**, #768 — **17.6 мин** (раньше было ~14 мин),
   и почти всё это время — 19 тестов × лимит 90 с = **28.5 мин** ожиданий, которые сгорают на удалённых данных.

### Почему это случилось

Job-level `concurrency` из `ci-front.yml` убран (`DO-100` п.3): с `cancel-in-progress: false` GitHub держит
только **один pending** прогон и отменял предыдущий pending при новом пуше. Решение «убрать concurrency»
сняло отмены, но открыло параллельность: теперь каждый пуш запускает свой прогон, и они идут одновременно.

### Что с этим делать (зона @DO, варианты по возрастанию усилий)

1. **Очистку — только в dispatch/ночном прогоне.** Push-прогоны не вызывают `cleanup-e2e-dev.sh`:
   мусор копится, но исчезает риск удалить данные чужого прогона. Дешевле всего, ломает меньше всего.
2. **Серийность без потери прогонов:** `concurrency: {group: front-e2e-stand, cancel-in-progress: false}`
   на уровне **job**, но запуск по `workflow_dispatch` и `schedule`, а пуши — только гейты (без браузера).
   Тогда браузерный прогон один, а быстрые гейты не ждут.
3. **Разные префиксы данных на прогон** (`+7999-<run_id>` и `TE-<run_id>-…`): тогда очистка может удалять
   только «своё» (шаблон по `GITHUB_RUN_ID`). Требует правки и спек, и скрипта очистки — самая большая работа.

Без одного из этих решений **любой** новый прогон будет давать те же 19 падений: данные исчезают
из-под теста, а тест ждёт до лимита. Это не дефект тестов и не «медленный UI».
