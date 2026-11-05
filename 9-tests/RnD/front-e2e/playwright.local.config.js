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

// Фича-спеки в обычном CI-прогоне помечены `test.skip` без флага (см. `front-e2e.md`):
// `XDIARY_BUSY`, `XDIARY_STALENOTE`, `XDIARY_READPATH_OFFLINE`, `XDIARY_READPARITY`.
// Локальный прогон их ВКЛЮЧАЕТ по умолчанию: двойник API их поддерживает, и смысл локального
// прогона — увидеть эти сценарии, а не молча пропустить. Внешняя переменная переопределяет.
for (const flag of ['XDIARY_BUSY', 'XDIARY_STALENOTE', 'XDIARY_READPATH_OFFLINE', 'XDIARY_READPARITY']) {
  if (process.env[flag] === undefined) process.env[flag] = '1';
}

// Адрес двойника выставляется ДО загрузки `tests/api.js` и `global-setup.js`: иначе `API_BASE`
// остался бы дефолтным (dev-стенд VPS-2) и globalSetup пошёл бы в интернет, а прогон упал бы
// на «стенд недоступен» вместо проверки спек. Это нашёл гейт `check_e2e_local_parity.py`.
process.env.XDIARY_E2E_BASE_URL = process.env.XDIARY_E2E_BASE_URL
  || `http://127.0.0.1:${Number(process.env.MOCK_API_PORT || 4180)}`;

const API_PORT = Number(process.env.MOCK_API_PORT || 4180);
const WEB_PORT = Number(process.env.FRONT_PORT || 4173);

module.exports = defineConfig({
  testDir: './tests',
  timeout: 60_000,
  workers: 1,
  fullyParallel: false,
  // Тот же предохранитель DO-017 и то же ожидание стенда, что в CI; на двойнике проходит
  // (проверено `node mock-smoke.js`: globalSetup отрабатывает против mock-api).
  globalSetup: './tests/global-setup.js',
  expect: { timeout: Number(process.env.XDIARY_E2E_EXPECT_WAIT || 10000) },
  // Тот же страж, что в CI: если ВСЕ тесты ушли в skip, локальный прогон тоже не «зелёный».
  reporter: [['list'], ['./reporters/notice.js'], ['./reporters/executed-guard.js']],
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
