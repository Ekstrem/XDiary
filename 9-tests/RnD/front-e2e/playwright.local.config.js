// playwright.local.config.js — прогон front-e2e БЕЗ скачивания браузеров и БЕЗ dev-стенда.
//
// Зачем: в песочнице (а) браузеры Playwright не скачиваются (`spawn EPERM`), (б) внешняя сеть
// закрыта (TLS-обрыв к dev-стенду). Это конфиг для локальной проверки **спек и фронта**:
//   * браузер берётся системный — `channel: 'msedge'` (Edge установлен в Windows);
//   * API подменяется `mock-api.js` (состояние в памяти) — фронт ходит в него через
//     `XDIARY_E2E_BASE_URL`, а `installBase` кладёт адрес в `localStorage.xdiary.apiBase`.
//
// Это НЕ замена прогону в CI на живом стенде: он проверяет контракт с бэкендом, а этот — что
// спеки и фронт согласованы. Запуск:
//   cd 9-tests/RnD/front-e2e
//   node node_modules/@playwright/test/cli.js test --config=playwright.local.config.js

const { defineConfig } = require('@playwright/test');

const API_PORT = Number(process.env.MOCK_API_PORT || 4180);
const WEB_PORT = Number(process.env.FRONT_PORT || 4173);

module.exports = defineConfig({
  testDir: './tests',
  timeout: 60_000,
  workers: 1,
  fullyParallel: false,
  reporter: [['list'], ['./reporters/notice.js']],
  use: {
    baseURL: `http://127.0.0.1:${WEB_PORT}`,
    headless: true,
    channel: 'msedge',
    screenshot: 'only-on-failure',
    trace: 'off',
  },
  webServer: [
    {
      command: 'node serve.js',
      url: `http://127.0.0.1:${WEB_PORT}`,
      reuseExistingServer: true,
      timeout: 20_000,
      env: { PORT: String(WEB_PORT) },
    },
    {
      command: 'node mock-api.js',
      url: `http://127.0.0.1:${API_PORT}/api/v1/privacy/pin/has`,
      reuseExistingServer: true,
      timeout: 20_000,
      env: { PORT: String(API_PORT) },
    },
  ],
  metadata: {
    // Стенд, против которого идёт прогон: локальный двойник, а не dev.
    stand: `mock-api http://127.0.0.1:${API_PORT}`,
  },
});
