// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
// API замокан через page.route; service worker заблокирован (иначе SW перехватывает
// fetch до page.route — известное ограничение Playwright).
const { test, expect } = require('@playwright/test');

const BASE = 'http://127.0.0.1:4173';

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const partners = [];

  page.on('console', (m) => console.log('[console]', m.type(), m.text()));
  page.on('pageerror', (e) => console.log('[pageerror]', e.message));
  page.on('requestfailed', (r) => console.log('[reqfail]', r.url(), r.failure() && r.failure().errorText));

  // same-origin API → CORS не нужен
  await page.addInitScript((base) => {
    try { localStorage.setItem('xdiary.apiBase', base); } catch (e) { /* ignore */ }
  }, BASE);

  await page.route('**/api/v1/**', async (route) => {
    const req = route.request();
    const p = new URL(req.url()).pathname;
    const method = req.method();
    let body = {};

    if (p === '/api/v1/privacy/pin/has') body = false;
    else if (p === '/api/v1/privacy/token') body = { accessToken: 'test-access', refreshToken: 'test-refresh' };
    else if (p === '/api/v1/partners' && method === 'GET') body = partners;
    else if (p === '/api/v1/partners' && method === 'POST') {
      const payload = JSON.parse(req.postData() || '{}');
      const created = { id: 'p' + (partners.length + 1), name: payload.name, photo: '', gender: 'female', notes: '' };
      partners.push(created);
      body = created;
    } else if (p.startsWith('/api/v1/partners/') && method === 'PUT') body = {};
    else if (p === '/api/v1/journal/records') body = [];

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': '*' },
      body: JSON.stringify(body),
    });
  });

  console.log('== goto ==');
  await page.goto('/');
  console.log('== loaded, title:', await page.title());

  await expect(page.locator('#view-journal')).toBeVisible();
  console.log('== journal visible ==');

  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();
  console.log('== partners visible ==');

  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();
  console.log('== partner-edit visible ==');

  await page.fill('#pe-name', 'Аня');
  await page.click('#btn-partner-save');
  console.log('== save clicked ==');

  await expect(page.locator('#view-partners')).toBeVisible();
  console.log('== back to partners ==');

  await expect(page.locator('#partners-list')).toContainText('Аня');
  console.log('== partner in list ==');

  expect(partners.map((x) => x.name)).toContain('Аня');
});
