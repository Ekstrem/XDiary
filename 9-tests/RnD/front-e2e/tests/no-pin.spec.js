// E2E: сценарий «без PIN» (FE-006/BE-019) — только для стенда БЕЗ PIN.
// Если на стенде PIN задан, приложение без локального PIN получает 401 на login("") —
// это отдельный продуктовый дефект (клиент не предлагает ввести PIN с другого устройства).
const { test, expect } = require('@playwright/test');
const { API_BASE, installBase, waitToken } = require('./api');

test('E2E: без PIN приложение получает device-токен и открывает журнал', async ({ page, request }) => {
  const probe = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin: '' } });
  test.skip(probe.status() !== 200, 'на стенде задан PIN — сценарий «без PIN» требует чистого стенда (без PIN)');

  await installBase(page);
  await page.goto('/');
  await expect(page.locator('#tabbar')).toBeVisible();
  await waitToken(page);
  expect(await page.evaluate(() => localStorage.getItem('xdiary.accessToken'))).toBeTruthy();
});
