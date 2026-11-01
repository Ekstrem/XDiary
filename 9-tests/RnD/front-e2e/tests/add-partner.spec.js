// E2E (настоящий): живое приложение + живой шлюз, без заглушек.
// Сквозной путь: приложение без PIN берёт device-токен → POST /api/v1/partners →
// партнёр реально сохраняется и появляется в списке → тест удаляет его за собой.
const { test, expect } = require('@playwright/test');
const { waitToken, removeByName } = require('./api');

test('E2E: партнёр — добавить в приложении, увидеть в списке, удалить', async ({ page }) => {
  const name = 'TE-' + Date.now();
  try {
    await page.goto('/');
    await expect(page.locator('#tabbar')).toBeVisible();

    // FE-006/BE-019: без PIN приложение получает device-токен, иначе все запросы 401
    await waitToken(page);

    await page.click('#tab-partners');
    await expect(page.locator('#view-partners')).toBeVisible();

    await page.click('#btn-partner-add');
    await expect(page.locator('#view-partner-edit')).toBeVisible();
    await page.fill('#pe-name', name);
    await page.click('#btn-partner-save');

    await expect(page.locator('#view-partners')).toBeVisible();
    await expect(page.locator('#partners-list')).toContainText(name);
  } finally {
    const r = await removeByName(page, 'partner', name);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
