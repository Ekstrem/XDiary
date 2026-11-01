// E2E (настоящий): без PIN приложение получает device-токен (FE-006/BE-019)
// и открывает журнал — на ЖИВОМ шлюзе, без заглушек.
const { test, expect } = require('@playwright/test');
const { waitToken } = require('./api');

test('E2E: без PIN приложение получает device-токен и открывает журнал', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#tabbar')).toBeVisible();
  await waitToken(page);
  expect(await page.evaluate(() => localStorage.getItem('xdiary.accessToken'))).toBeTruthy();
});
