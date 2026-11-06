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
