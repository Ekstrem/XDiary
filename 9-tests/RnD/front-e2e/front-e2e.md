# front-e2e — маркер фронтового E2E набора (ADR-028 §4)

**Зачем файл:** гейт `tools/check_front_e2e.py` считает проект фронтовым E2E только если у него есть **маркер `front-e2e.md`** и **браузерная зависимость**. Это сделано, чтобы API-тесты не возвращались в `9-tests` «по привычке»: `9-tests/**` — только проверки **через фронт (браузер)**, имя папки не должно врать о содержимом.

- **Раннер:** Playwright (`@playwright/test`), конфиг `playwright.config.js`, `workers: 1`, `timeout: 90_000`.
- **Как запускается:** `webServer` поднимает статику `8-front/` локально (`node serve.js`, `use.baseURL = http://127.0.0.1:4173`); API берётся из `XDIARY_E2E_BASE_URL` (dev/прод-контур), `XDIARY_E2E_BASE_URL` **не** задаёт адрес самого приложения — оно всегда отдаётся локально, поэтому тест проверяет **свой** фронт, а не задеплоенный.
- **Фикстура:** `tests/api.js` — `loginWithPin` + `seedSession` (профиль «я» через `POST /api/v1/clients/me/profile`, партнёр отдельным действием; шим `link-partner` вызывается только на образах < `0.88` и это видно в логе как `profileMode`/`shimUsed`).
- **Чего здесь нет и не должно быть:** C#-проектов и тестов «по HTTP без браузера» — они живут в папке своего сервиса — `6-msa/BC-<X>/src/<S>.ApiTests`, а тест пары — у потребителя (`<Потребитель>.ApiTests`); общих папок `6-msa/ApiTests` и `6-msa/E2E` **нет** (ADR-028 §1.1, §3.1).

## Экраны и что покрыто

| Экран (`8-front/index.html`) | Спека | Что проверяется через браузер |
|---|---|---|
`#view-splash`, `#view-onboarding` | `onboarding.spec.js` | первый вход: онбординг без партнёра, профиль «я» |
`#view-lock` (PIN) | `pin-first-login.spec.js`, `no-pin.spec.js`, `change-pin.spec.js`, `remove-pin.spec.js` | установка/смена/снятие PIN, вход по PIN, режим «без PIN» |
`#view-lock` (PIN, стенд с PIN + нет локального) | `pin-fallback.spec.js` | путь FE-007: экран PIN появляется и принимает верный PIN; стенд без PIN → skip по внешнему факту |
`#view-journal`, `#tab-journal` | `wizard-record.spec.js`, `edit-delete-record.spec.js` | создание записи мастером, правка и удаление записи, отображение в списке |
`#view-wizard` (без сети) | `wizard-offline.spec.js` | read-paths §1: ошибка чтения отличима от «данных нет», форма сохранена; включается `XDIARY_READPATH_OFFLINE=1` |
`#view-detail` | `edit-delete-record.spec.js`, `somatic-detail.spec.js` | карточка записи: правка, детали, соматические поля |
`#view-partners`, `#tab-partners` | `add-partner.spec.js`, `partner-edit.spec.js`, `partner-client-link.spec.js` | добавление партнёра, переименование/профиль, привязка партнёра к клиенту |
`#view-partner-edit` | `partner-edit.spec.js` | профиль партнёра (пол канонизируется на сервере) |
`#view-catalog`, `#tab-catalog` | `catalog.spec.js`, `catalog-archive.spec.js` | каталог: добавление по `kind`, архив игрушек |
`#view-catalog-edit` | `catalog.spec.js` | правка позиции каталога |
`#view-trends`, `#tab-trends` | `trends.spec.js` | графики/агрегаты |
`#view-import` | `csv.spec.js` | импорт CSV, разбор ошибок |
`#view-settings`, `#tab-settings` | `settings.spec.js` | настройки: PIN, профиль, приватность |
`#tab-trends` | `busy-indicator.spec.js` | индикатор «Считаем тренды»: на 1000 мс появляется, на 100 мс — нет, после завершения исчезает; > 3 с текст «дольше обычного» + «Повторить»; включается `XDIARY_BUSY=1`
`#view-journal` | `stale-note.spec.js` | read-paths §5 п.2: пометка «данные могли устареть» есть на устаревшем кэше и отсутствует на свежих данных; включается `XDIARY_STALENOTE=1`
`#view-partners`, `#tab-partners` | `partners-read-parity.spec.js` | `BE-071`: созданный партнёр появляется в `GET /partners` (паритет read-модели с живыми данными, а не «200 на пустоте»)
— (Garmin/соматика) | `garmin-somatic.spec.js` | данные Garmin и соматические серии на экранах журнала/трендов |

**Не покрыто браузером (осознанно):** контракты API, инварианты агрегатов, межсервисные сценарии — это `6-msa/BC-<X>/src/` (unit/интеграция/`*.ApiTests`) по ADR-028 ред. 3 — общих тестовых папок нет.

## Правила для новых спек

1. Новая спека = строка в таблице выше (экран + что проверяется). Спека без строки здесь — не покрытие, а заготовка.
2. Селекторы берём **из `8-front/index.html`** (`#view-*`, `#tab-*`), а не придумываем: дрейф id ловится этим набором.
3. Если сценарий требует только API (нет действий в UI) — он не идёт сюда: место — в папке сервиса (`6-msa/BC-<X>/src/<S>.ApiTests`), для пары — у потребителя.

## Правило: спека не открывает приложение без E2E-фикстуры (FE-024)

После `0.57` фронт читает `localStorage.xdiary.apiBase` **только** при `window.__XDIARY_E2E__ === true`;
иначе ключ вычищается и `baseUrl()` отдаёт `DEFAULT_BASE` (прод VPS-1). Флаг ставит `installBase()`
через `page.addInitScript` (`tests/api.js:72-78`), а `seedSessionOnce` вызывает её первым шагом (`:145`),
поэтому `loginWithPin`/`seedSession` безопасны. `global-setup.js` проверяет только `XDIARY_E2E_BASE_URL`
и такой случай не ловит — он про API-базу теста, а не про то, куда смотрит страница.

**Проверка:** `python 9-tests/scripts/check_front_specs_base.py` (по `origin/main`) или
`REF=WORKTREE python 9-tests/scripts/check_front_specs_base.py` (по рабочему дереву).
Правило: если в спеке есть `page.goto(`, в ней обязано быть `installBase(` **или** `loginWithPin(`/`seedSession(`.

## Локальный прогон без dev-стенда и без скачивания браузеров

В песочнице агента прогон «как в CI» невозможен по двум причинам, обе проверены:
**браузеры Playwright не устанавливаются** (`spawn EPERM` при распаковке) и **внешняя сеть закрыта**
(TLS-обрыв к `xdiary-vps2.taile0b6ee.ts.net`). Для проверки спек и фронта без этого добавлены:

| Файл | Назначение |
|---|---|
| `mock-api.js` | локальный двойник API на `127.0.0.1:4180`, состояние в памяти: клиенты, `clients/{id}/verify`, `clients/me`, `clients/me/profile`, `partners` (+remove), `journal/records` (+recent/places), `catalog`, `privacy/pin/*`, `somatic-series`/`daily-metrics`/`analytics` (пустые). CORS открыт. |
| `playwright.local.config.js` | тот же `testDir`, но браузер **системный** — `channel: 'msedge'`, и два `webServer`: `serve.js` (4173) + `mock-api.js` (4180) |

Запуск (на обычной машине, где браузер разрешён):

```bash
cd 9-tests/RnD/front-e2e
npm run test:local          # playwright test --config=playwright.local.config.js
npm run mock-api            # отдельно, если нужен только двойник API
```

Переменные: `XDIARY_E2E_BASE_URL=http://127.0.0.1:4180` (фикстура `installBase` кладёт адрес
в `localStorage.xdiary.apiBase`), `XDIARY_E2E_PIN=1234`, `XDIARY_READPATH_OFFLINE=1`.

**Двойник держит контракт стенда, который проверяет `tests/global-setup.js`:** `/health` → 200,
`GET /api/v1/clients` → 405, `/api/v1/partners` без токена → 401, `POST /clients/refresh` с битым
токеном → 401, `GET /clients/me` без токена → 401 + `WWW-Authenticate`. Без этих ответов локальный
прогон падал бы **в globalSetup** («стенд отвечает не так»), хотя спеки к нему готовы. Проверено
реальным прогоном: `require('./tests/global-setup.js')()` против двойника возвращает **0** и печатает
«контракт стенда ок: refresh 405 (401), verify refreshToken, /clients/me=401+WWW-Authenticate».
`mock-smoke.js` теперь проверяет и это: **23/23**, `rc=0`.

**Самопроверка двойника — `node mock-smoke.js`** (без браузера): поднимает `mock-api.js` на свободном
порту, дёргает те же 17 путей, что и спеки, и проверяет **формы ответов**, а не только коды
(`pin/has`, `clients`, `verify`, `me/profile`, `me`, `partners` + список, `journal/records` + `recent`,
`places`, `somatic-series/*`, `daily-metrics/*`, `analytics/*`, `privacy/pin/set|remove`).
Сейчас: **17/17, `rc=0`**. Это защищает от «двойник отвечает не тем» — иначе прогон спек дал бы
ложный красный по продукту.

**Что это доказывает и чего нет:** локальный прогон проверяет **спеки и фронт** (селекторы, шаги,
ожидания, ветки ошибок) и НЕ проверяет контракт с живым бэкендом — это по-прежнему делает CI на стенде.
Проверено в текущей среде: `--list` через локальный конфиг даёт **29 тестов в 22 файлах**
(браузерный канал Edge подхватывается без скачивания), эндпоинты двойника отвечают
(`pin/has` → `{"has":false}`, `clients` → `id`, `verify` → токены, `partners`/`journal` создаются и читаются).
Сам запуск браузера в песочнице падает на `spawn EPERM` — это ограничение среды, а не набора.
