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
— (Garmin/соматика) | `garmin-somatic.spec.js` | данные Garmin и соматические серии на экранах журнала/трендов |

**Не покрыто браузером (осознанно):** контракты API, инварианты агрегатов, межсервисные сценарии — это `6-msa/BC-<X>/src/` (unit/интеграция/`*.ApiTests`) по ADR-028 ред. 3 — общих тестовых папок нет.

## Правила для новых спек

1. Новая спека = строка в таблице выше (экран + что проверяется). Спека без строки здесь — не покрытие, а заготовка.
2. Селекторы берём **из `8-front/index.html`** (`#view-*`, `#tab-*`), а не придумываем: дрейф id ловится этим набором.
3. Если сценарий требует только API (нет действий в UI) — он не идёт сюда: место — в папке сервиса (`6-msa/BC-<X>/src/<S>.ApiTests`), для пары — у потребителя.
