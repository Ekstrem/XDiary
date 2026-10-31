// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
//
// Почему стаб fetch, а не page.route:
//  - приложение по умолчанию ходит на кросс-доменный https://...ts.net;
//  - PWA регистрирует sw.js, а service worker может перехватывать fetch ДО page.route.
// Стаб window.fetch через addInitScript убирает сеть/CORS/SW из уравнения: тест
// проверяет именно UI-логику (кнопки, роутинг видов, рендер списка).
const { test, expect } = require('@playwright/test');

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  page.on('console', (m) => console.log('[console]', m.type(), m.text()));
  page.on('pageerror', (e) => console.log('[pageerror]', e.message));

  await page.addInitScript(() => {
    const partners = [];
    window.__partners = partners;
    const json = (body) => new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });

    window.fetch = function (input, init) {
      const opts = init || {};
      const url = String(typeof input === 'string' ? input : (input && input.url) || '');
      const method = String(opts.method || 'GET').toUpperCase();
      const path = url.replace(/^[a-z]+:\/\/[^/]+/i, '');

      if (path === '/api/v1/privacy/pin/has') return Promise.resolve(json(false));
      if (path === '/api/v1/privacy/token') return Promise.resolve(json({ accessToken: 't', refreshToken: 'r' }));
      if (path === '/api/v1/partners' && method === 'GET') return Promise.resolve(json(partners));
      if (path === '/api/v1/partners' && method === 'POST') {
        let body = {};
        try { body = JSON.parse(opts.body || '{}'); } catch (e) { body = {}; }
        const created = { id: 'p' + (partners.length + 1), name: body.name || '', photo: '', gender: 'female', notes: '' };
        partners.push(created);
        return Promise.resolve(json(created));
      }
      if (path.indexOf('/api/v1/partners/') === 0 && method === 'PUT') return Promise.resolve(json({}));
      if (path === '/api/v1/journal/records') return Promise.resolve(json([]));
      if (path.indexOf('/local/') === 0 || path.indexOf('/activities') === 0) return Promise.resolve(json([]));
      return Promise.resolve(json({}));
    };
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

  const names = await page.evaluate(() => (window.__partners || []).map((p) => p.name));
  expect(names).toContain('Аня');
});
