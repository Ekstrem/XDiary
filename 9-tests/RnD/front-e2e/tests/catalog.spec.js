// E2E (настоящий): каталог по всем kind — живое приложение + живой шлюз.
// Вход по PIN, добавление элемента каждого вида, проверка в списке, удаление за собой.
const { test, expect } = require('@playwright/test');
const { loginWithPin, removeByName } = require('./api');

const KINDS = ['costume', 'toy', 'fantasy', 'contraception'];

for (const kind of KINDS) {
  test(`E2E: каталог ${kind} — добавить, увидеть в списке, удалить`, async ({ page }) => {
    const name = 'TE-' + kind + '-' + Date.now();
    try {
      await loginWithPin(page);
      await expect(page.locator('#view-journal')).toBeVisible();

      await page.click('#tab-catalog');
      await expect(page.locator('#view-catalog')).toBeVisible();
      await page.click(`#catalog-tabs button[data-kind="${kind}"]`);

      await page.click('#btn-catalog-add');
      await expect(page.locator('#view-catalog-edit')).toBeVisible();
      await page.fill('#ce-name', name);
      await page.click('#btn-catalog-save');

      await expect(page.locator('#view-catalog')).toBeVisible();
      await expect(page.locator('#catalog-list')).toContainText(name);
    } finally {
      const r = await removeByName(page, 'catalog', name);
      console.log('cleanup catalog:', JSON.stringify(r));
    }
  });
}
