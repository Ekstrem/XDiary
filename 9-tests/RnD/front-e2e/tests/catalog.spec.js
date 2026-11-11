// Трассировка: TE-032 (E2E-фикстуры под новую модель входа) + TE-020 (каталог: добавление и архив).
// E2E (настоящий): каталог по всем kind — живое приложение + живой шлюз.
// Вход по PIN, добавление элемента каждого вида, проверка в списке, удаление за собой.
const { test, expect } = require('@playwright/test');
const { loginWithPin, removeByName, stepGuard } = require('./api');

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
      // правка: поля показаны НЕ для всех видов — `app.js:1404/1407`: `ce-brand-wrap` виден только при
      // `kind === "toy"`, `ce-partner-wrap` — только при `kind === "fantasy"`. Раньше спека заполняла
      // `#ce-brand` для всех четырёх видов: у costume/fantasy/contraception поле лежит в скрытой обёртке,
      // `page.fill` ждёт видимости и падает по общему лимиту («не дождались», три падения в CI).
      if (await page.locator('#ce-brand-wrap').isVisible()) {
        await page.fill('#ce-brand', 'TE-BRAND');
        await expect(page.locator('#ce-brand')).toHaveValue('TE-BRAND');
      }
      if (await page.locator('#ce-partner-wrap').isVisible()) {
        const partnerOptions = await page.locator('#ce-partner option').count();
        if (partnerOptions > 0) {
          await page.locator('#ce-partner').selectOption({ index: 0 });
        }
      }
      await stepGuard(page, 'catalog: save', async () => {
        await page.click('#btn-catalog-save', { timeout: 20000 });
        // saveCatalogEdit сохраняет через API — 5с по умолчанию мало на dev (был красный contraception)
        await expect(page.locator('#view-catalog')).toBeVisible({ timeout: 20000 });
      });
      // правка: `#catalog-list` содержит и чужие записи, поэтому «имени нет в тексте контейнера»
      // неотличимо от «список показывает не то». Утверждаем СТРОКУ (карточка — `button.partner-row`):
      await expect(page.locator('#catalog-list .partner-row', { hasText: name })).toHaveCount(1);
    } finally {
      const r = await removeByName(page, 'catalog', name);
      console.log('cleanup catalog:', JSON.stringify(r));
    }
  });
}
