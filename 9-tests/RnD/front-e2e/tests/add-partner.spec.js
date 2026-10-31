// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
//
// Диагностика: на падении печатаем `::error::` с шагом, ошибкой и снимком DOM —
// GitHub превращает это в аннотацию, читаемую без токена.
const { test, expect } = require('@playwright/test');

let lastStep = 'init';
const errors = [];

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const mark = (s) => { lastStep = s; };
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console.error: ' + m.text()); });

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
  mark('loaded');

  // Ждём появления таббара: его показывает showView("journal") из lock()/init().
  await expect(page.locator('#tabbar')).toBeVisible();
  mark('tabbar-visible');

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

test.afterEach(async ({ page }, testInfo) => {
  if (testInfo.status === testInfo.expectedStatus) return;
  const err = testInfo.error ? String(testInfo.error.message || testInfo.error) : 'unknown';
  let snap = null;
  try {
    snap = await page.evaluate(() => {
      const vis = (id) => {
        const el = document.getElementById(id);
        return el ? !el.classList.contains('hidden') : 'missing';
      };
      return {
        lock: vis('view-lock'),
        journal: vis('view-journal'),
        partners: vis('view-partners'),
        tabbar: vis('tabbar'),
        pin: typeof window.Pin,
        db: typeof window.DB,
        api: typeof window.Api,
        apiBase: localStorage.getItem('xdiary.apiBase'),
        bodyClass: document.body.className,
      };
    });
  } catch (e) {
    snap = { evalError: String(e && e.message) };
  }
  const payload = { step: lastStep, error: err, dom: snap, errors: errors.slice(0, 6) };
  console.log('::error::' + JSON.stringify(payload).replace(/[\r\n]+/g, ' ').slice(0, 900));
});
