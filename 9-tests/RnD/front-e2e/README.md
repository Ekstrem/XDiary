# RnD: UI-автотесты фронта (Playwright)

Проверка «с фронта»: поднимаем `8-front/` статикой, подменяем API (стаб `window.fetch`)
и проходим пользовательский сценарий. Первый сценарий — **«Добавить партнёра»**.

## Состав
- `serve.js` — мини-статик-сервер для `8-front/` (0 зависимостей), порт 4173.
- `playwright.config.js` — Playwright, `baseURL http://127.0.0.1:4173`, авто-запуск `serve.js`,
  `serviceWorkers: 'block'` (иначе SW перехватывает `fetch` до стаба).
- `tests/add-partner.spec.js` — сценарий: журнал → Партнёры → «+» → имя → Сохранить → партнёр в списке.
- API подменяется стабом `window.fetch` (через `addInitScript`) по контракту бэкенда
  **BE-018 (POST-only)**: `POST /api/v1/partners`, `POST /api/v1/partners/{id}/name|profile|remove`;
  `PUT`/`DELETE` → `405`. Живой бэкенд не нужен.

## Диагностика падений
На падении тест печатает `::error::<json>` (шаг, ошибка, снимок DOM, журнал вызовов) —
GitHub превращает это в аннотацию check-run, которую можно прочитать без токена.

## Почему в CI, а не локально
В песочнице разработчика браузер не запускается (mojo/IPC: `Отказано в доступе`), поэтому
прогон — на CI-раннере (Linux), где Chromium ставится из `npx playwright install`.

## Локальный запуск (машина с браузером)
```bash
cd 9-tests/RnD/front-e2e
npm install
npx playwright install --with-deps chromium
npm test
```

## CI
Job `front-e2e` уже добавлен в `.github/workflows/ci-tests.yml` (**DO-011**):
`actions/setup-node` → `npx playwright install --with-deps chromium` → `npm test`.

## Что дальше (по мере готовности)
- ещё сценарии: каталог (добавить/архив), запись через визард, аналитика;
- вариант против живого шлюза (без мока) — когда стенд доступен из CI.
