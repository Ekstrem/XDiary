# E2E: UI-автотесты фронта (Playwright) — против ЖИВОГО API

Живое приложение (`8-front/` статикой) + **живой шлюз** `XDIARY_E2E_BASE_URL`
(по умолчанию `https://xdiary-vps1.taile0b6ee.ts.net`). Заглушек нет:
каждый шаг реально уходит в API, тест видит и 401, и 502, и лежащий стенд.

## Состав
- `serve.js` — мини-статик-сервер для `8-front/` (0 зависимостей), порт 4173.
- `playwright.config.js` — Playwright, `baseURL http://127.0.0.1:4173`, авто-запуск `serve.js`, timeout 90s.
- `tests/api.js` — базовый URL API + ожидание device-токена + **уборка** созданного через API.
- `tests/add-partner.spec.js` — партнёр: журнал → Партнёры → «+» → имя → Сохранить → в списке → удалить.
- `tests/catalog.spec.js` — **каталог по всем kind**: костюм, игрушка, фантазия, контрацепция —
  добавить → в списке → удалить.
- `tests/no-pin.spec.js` — без PIN приложение получает device-токен (FE-006/BE-019) и открывает журнал.

## Данные
Тесты создают сущности с префиксом `TE-` и **удаляют их за собой** (`POST .../remove`).
Если прогон упал в момент создания, сущность может остаться — это тестовый мусор с префиксом `TE-`.

## Зависимость от стенда
Тесты ходят в реальный API, поэтому **падение стенда = красный CI**. Это осознанно:
именно так ловятся обрывы шлюза (`rootlessport`/Funnel 502), которые UI-тест на заглушке не видит.

## Локальный запуск (машина с браузером)
```bash
cd 9-tests/RnD/front-e2e
npm install
npx playwright install --with-deps chromium
XDIARY_E2E_BASE_URL=https://xdiary-vps1.taile0b6ee.ts.net npm test
```

## CI
Job `front-e2e` в `.github/workflows/ci-tests.yml` (**DO-011**):
`actions/setup-node` → `npx playwright install --with-deps chromium` → `npm test`.

## Что дальше
- визард записи (аксессуары/контрацепция/оргазмы), тренды, настройки;
- smoke `/health` в CI, чтобы падение стенда было видно отдельно от кода.
