# RnD: UI-автотесты фронта (Playwright)

Проверка «с фронта»: поднимаем `8-front/` статикой, мокаем API через `page.route`
и проходим пользовательский сценарий. Первый сценарий — **«Добавить партнёра»**.

## Состав
- `serve.js` — мини-статик-сервер для `8-front/` (0 зависимостей), порт 4173.
- `playwright.config.js` — Playwright, `baseURL http://127.0.0.1:4173`, авто-запуск `serve.js`.
- `tests/add-partner.spec.js` — сценарий: журнал → Партнёры → «+» → имя → Сохранить → партнёр в списке.
- API мокается (`GET/POST /api/v1/partners`, `PUT /api/v1/partners/{id}/profile`, `/api/v1/privacy/token`),
  поэтому живой бэкенд не нужен.

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

## CI (фрагмент для DO — добавить отдельным job)
```yaml
  front-e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - name: Install deps
        working-directory: 9-tests/RnD/front-e2e
        run: npm ci || npm install
      - name: Install Playwright browsers
        working-directory: 9-tests/RnD/front-e2e
        run: npx playwright install --with-deps chromium
      - name: Front E2E
        working-directory: 9-tests/RnD/front-e2e
        run: npm test
```

## Что дальше (по мере готовности)
- ещё сценарии: каталог (добавить/архив), запись через визард, аналитика;
- вариант против живого шлюза (без мока) — когда стенд доступен из CI;
- скриншоты/видео на падении (`use: { screenshot: 'only-on-failure' }`).
