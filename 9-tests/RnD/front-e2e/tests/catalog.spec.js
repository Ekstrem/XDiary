// RnD: UI-автотест каталога XDiary — игрушки, костюмы, фантазии, контрацепция.
// Проверяет: вкладки по kind, создание элемента и появление его в списке.
const { test, expect } = require('@playwright/test');
const { installApiMock } = require('./mock');

const KINDS = [
  { kind: 'costume', name: 'Костюм медсестры' },
  { kind: 'toy', name: 'Вибратор LELO' },
  { kind: 'fantasy', name: 'Ролевая игра' },
  { kind: 'contraception', name: 'Презерватив Durex' },
];

for (const { kind, name } of KINDS) {
  test(`Каталог (${kind}): добавить «${name}» и увидеть в списке`, async ({ page }) => {
    await installApiMock(page);

    await page.goto('/');
    await expect(page.locator('#tabbar')).toBeVisible();

    await page.click('#tab-catalog');
    await expect(page.locator('#view-catalog')).toBeVisible();

    // выбираем вкладку kind
    await page.click(`#catalog-tabs button[data-kind="${kind}"]`);
    await page.click('#btn-catalog-add');
    await expect(page.locator('#view-catalog-edit')).toBeVisible();

    await page.fill('#ce-name', name);
    await page.click('#btn-catalog-save');

    // после сохранения — снова каталог, элемент в списке
    await expect(page.locator('#view-catalog')).toBeVisible();
    await expect(page.locator('#catalog-list')).toContainText(name);

    const kinds = await page.evaluate(() => (window.__catalog || []).map((x) => x.kind + ':' + x.name));
    expect(kinds).toContain(`${kind}:${name}`);
  });
}
