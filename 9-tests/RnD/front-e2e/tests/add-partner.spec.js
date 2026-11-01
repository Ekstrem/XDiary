// E2E (настоящий): живое приложение + живой шлюз, без заглушек.
// Вход по PIN (как пользователь) → POST /api/v1/partners реально уходит →
// партнёр появляется в списке из бэкенда → тест удаляет его за собой.
const { test, expect } = require('@playwright/test');
const { loginWithPin, removeByName } = require('./api');

test('E2E: партнёр — добавить в приложении, увидеть в списке, удалить', async ({ page }) => {
  const name = 'TE-' + Date.now();
  try {
    await loginWithPin(page);
    await expect(page.locator('#view-journal')).toBeVisible();

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
