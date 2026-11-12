const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 90_000,
  // PIN на стенде общий, а change-pin его меняет — тесты идут строго последовательно.
  workers: 1,
  // Дефолт Playwright для `expect(...)` без явного таймаута — 5 с. На dev-стенде UI иногда
  // «доезжает» дольше (загрузка IndexedDB после reload), и 5 с давали ложные красные
  // в утверждениях без собственного таймаута. 10 с — компромисс: медленные ожидания
  // по-прежнему диагностируются, а мгновенный флейк уходит. Управляется тем же
  // `XDIARY_E2E_UI_WAIT`, если нужно поднять разом.
  expect: { timeout: Number(process.env.XDIARY_E2E_EXPECT_WAIT || 10000) },
  fullyParallel: false,
  globalSetup: './tests/global-setup.js',
  // TE-118: сидер убирает за собой (см. `tests/global-teardown.js`)
  globalTeardown: './tests/global-teardown.js',
  // Второй репортёр  страж: если ВСЕ тесты ушли в skip, прогон НЕ зелёный (код возврата 1,
  // `::error::` в аннотации). Иначе «0 passed, N skipped» читалось бы как успех.
  reporter: [['list'], ['./reporters/notice.js'], ['./reporters/executed-guard.js']],
  use: {
    baseURL: 'http://127.0.0.1:4173',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    // FE-046 (@AR 01:51, @TL 01:54): без `actionTimeout` ЛЮБОЕ действие без явного
    // таймаута ждёт таймаут ТЕСТА (90 с) — так `innerText()` на несуществующем
    // `#toast` вешал спеку, а дамп показывал `about:blank` уже после закрытия
    // контекста. 15 с: отсутствующий элемент падает по-английски «элемента нет»,
    // а страница к моменту дампа ещё жива.
    actionTimeout: Number(process.env.XDIARY_E2E_ACTION_TIMEOUT || 15000),
  },
  webServer: {
    command: 'node serve.js',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: true,
    timeout: 15_000,
  },
});
