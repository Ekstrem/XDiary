// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
//
// Диагностика: на падении печатаем GitHub workflow-команду `::error::…` — она
// превращается в аннотацию check-run, которую можно прочитать без токена.
const { test, expect } = require('@playwright/test');

let lastStep = 'init';

test.afterEach(async ({}, testInfo) => {
  if (testInfo.status !== testInfo.expectedStatus) {
    const err = testInfo.error ? String(testInfo.error.message || testInfo.error) : 'unknown error';
    const msg = ('front-e2e FAILED at [' + lastStep + '] :: ' + err).replace(/[\r\n]+/g, ' ').slice(0, 900);
    console.log('::error::' + msg);
  }
});

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const mark = (s) => { lastStep = s; console.log('== ' + s + ' =='); };

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

  mark('goto');
  await page.goto('/');
  mark('loaded:' + (await page.title()));

  await expect(page.locator('#view-journal')).toBeVisible();
  mark('journal-visible');

  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();
  mark('partners-visible');

  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();
  mark('partner-edit-visible');

  await page.fill('#pe-name', 'Аня');
  await page.click('#btn-partner-save');
  mark('save-clicked');

  await expect(page.locator('#view-partners')).toBeVisible();
  mark('back-to-partners');

  await expect(page.locator('#partners-list')).toContainText('Аня');
  mark('partner-in-list');

  const names = await page.evaluate(() => (window.__partners || []).map((p) => p.name));
  expect(names).toContain('Аня');
  mark('done');
});
