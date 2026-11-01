// RnD: сценарий «без PIN» — FE-006 / BE-019.
//
// Продуктовое правило: PIN — возможность, а не правило. BE-019 выдаёт device-токен
// при `!HasPin`; FE-006 в ветке `lock()` → `!hasPin` запрашивает токен
// (`POST /api/v1/privacy/token`, pin: "") и только потом открывает журнал.
const { test, expect } = require('@playwright/test');
const { installApiMock } = require('./mock');

test('Без PIN: приложение берёт device-токен и сохраняет партнёра', async ({ page }) => {
  await installApiMock(page);

  await page.goto('/');
  await expect(page.locator('#tabbar')).toBeVisible();
  await expect(page.locator('#view-journal')).toBeVisible();

  // FE-006: без локального PIN должен уйти запрос токена (pin: "")
  const calls = await page.evaluate(() => window.__calls || []);
  expect(calls).toContain('POST /api/v1/privacy/token');

  // и сохранение работает (токен не мешает)
  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();
  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();
  await page.fill('#pe-name', 'БезПин');
  await page.click('#btn-partner-save');
  await expect(page.locator('#partners-list')).toContainText('БезПин');
});
