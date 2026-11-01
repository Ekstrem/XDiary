// E2E (настоящий) — TE-020: archive/restore элемента каталога (кнопка #ce-archive — только для игрушек).
// Элемент создаётся через живой API, архив/восстановление — через UI, проверка — через API.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, removeByName } = require('./api');

const NAME = 'TE-TOY-' + Date.now();

async function createItem(page) {
  return page.evaluate(async ({ base, name }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/catalog', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
      body: JSON.stringify({ kind: 'toy', name }),
    });
    if (!r.ok) throw new Error('POST /catalog -> ' + r.status);
    return (await r.json()).id;
  }, { base: API_BASE, name: NAME });
}

async function getItem(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/catalog/' + id, { headers: { Authorization: 'Bearer ' + t } });
    return r.ok ? await r.json() : null;
  }, { base: API_BASE, id });
}

async function openItem(page) {
  await page.click('#tab-catalog');
  await expect(page.locator('#view-catalog')).toBeVisible();
  await page.click('#catalog-tabs button[data-kind="toy"]');
  const card = page.locator('#catalog-list .partner-row').filter({ hasText: NAME });
  await expect(card, 'карточка ' + NAME).toHaveCount(1);
  await card.click();
  await expect(page.locator('#view-catalog-edit')).toBeVisible();
}

test('E2E: каталог — archive и restore через UI, проверка API', async ({ page }) => {
  let id = null;
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('setup: создать игрушку через API', async () => {
      id = await createItem(page);
      expect(id, 'id созданного элемента').toBeTruthy();
    });

    await test.step('архивировать через UI', async () => {
      await openItem(page);
      await expect(page.locator('#ce-archive')).toBeVisible();
      await expect(page.locator('#ce-archive')).toHaveText(/В архив/);
      await page.click('#ce-archive');
      await expect(page.locator('#ce-archive')).toHaveText(/Вернуть/);
      await page.click('#btn-catalog-save');
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: 30000 });

      const archived = await getItem(page, id);
      expect(archived, 'GET /catalog/{id}').toBeTruthy();
      expect(archived.archived, 'archived = true после «В архив» (API)').toBe(true);
    });

    await test.step('восстановить через UI', async () => {
      await openItem(page);
      await expect(page.locator('#ce-archive')).toHaveText(/Вернуть/);
      await page.click('#ce-archive');
      await expect(page.locator('#ce-archive')).toHaveText(/В архив/);
      await page.click('#btn-catalog-save');
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: 30000 });

      const restored = await getItem(page, id);
      expect(restored.archived, 'archived = false после restore (API)').toBe(false);
    });
  } finally {
    const r = await removeByName(page, 'catalog', NAME);
    console.log('cleanup catalog:', JSON.stringify(r));
  }
});
