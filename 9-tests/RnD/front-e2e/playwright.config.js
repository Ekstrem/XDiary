const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 90_000,
  reporter: 'list',
  use: {
    baseURL: 'http://127.0.0.1:4173',
    headless: true,
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'node serve.js',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: true,
    timeout: 15_000,
  },
});
