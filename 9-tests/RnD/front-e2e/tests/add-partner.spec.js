// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
//
// Мок максимально повторяет реальный контракт бэкенда:
//   POST /api/v1/partners {name}            -> создаёт (Register), отдаёт {id,name}
//   PUT  /api/v1/partners/{id}/name {name}  -> 404, если партнёра нет (Rename: "Partner not found.")
//   PUT  /api/v1/partners/{id}/profile {...}-> 404, если партнёра нет
// Диагностика падения — `::error::` с шагом, DOM и журналом вызовов (читается без токена).
const { test, expect } = require('@playwright/test');

let lastStep = 'init';
const errors = [];

const jsonResp = (body, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' },
});

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const mark = (s) => { lastStep = s; };
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console.error: ' + m.text()); });

  await page.addInitScript(() => {
    const partners = [];
    let seq = 0;
    window.__partners = partners;
    window.__calls = [];

    const json = (body, status = 200) => new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' },
    });
    const notFound = () => json({ error: 'Partner not found.' }, 404);

    window.fetch = function (input, init) {
      const opts = init || {};
      const url = String(typeof input === 'string' ? input : (input && input.url) || '');
      const method = String(opts.method || 'GET').toUpperCase();
      const path = url.replace(/^[a-z]+:\/\/[^/]+/i, '');
      window.__calls.push(method + ' ' + path);

      let body = {};
      try { body = JSON.parse(opts.body || '{}'); } catch (e) { body = {}; }

      if (path === '/api/v1/privacy/pin/has') return Promise.resolve(json(false));
      if (path === '/api/v1/privacy/token') return Promise.resolve(json({ accessToken: 't', refreshToken: 'r' }));
      if (path === '/api/v1/partners' && method === 'GET') return Promise.resolve(json(partners));
      if (path === '/api/v1/partners' && method === 'POST') {
        const created = { id: 'srv-' + (++seq), name: body.name || '', photo: '', gender: 'female', notes: '' };
        partners.push(created);
        return Promise.resolve(json(created));
      }

      const m = path.match(/^\/api\/v1\/partners\/([^/]+)\/(name|profile)$/);
      if (m && method === 'PUT') {
        const p = partners.find((x) => x.id === m[1]);
        if (!p) return Promise.resolve(notFound());
        if (m[2] === 'name') p.name = body.name || p.name;
        else {
          if (body.photo !== undefined) p.photo = body.photo;
          if (body.gender !== undefined) p.gender = body.gender;
          if (body.notes !== undefined) p.notes = body.notes;
        }
        return Promise.resolve(json(p));
      }

      if (path === '/api/v1/journal/records') return Promise.resolve(json([]));
      if (path.indexOf('/local/') === 0 || path.indexOf('/activities') === 0) return Promise.resolve(json([]));
      return Promise.resolve(json({}));
    };
  });

  mark('goto');
  await page.goto('/');
  mark('loaded');

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
        partnerEdit: vis('view-partner-edit'),
        tabbar: vis('tabbar'),
        partnersLen: (window.__partners || []).length,
        calls: (window.__calls || []).slice(0, 20),
        listHtml: (document.getElementById('partners-list') || {}).innerHTML,
      };
    });
  } catch (e) {
    snap = { evalError: String(e && e.message) };
  }
  const payload = { step: lastStep, error: err, dom: snap, errors: errors.slice(0, 6) };
  console.log('::error::' + JSON.stringify(payload).replace(/[\r\n]+/g, ' ').slice(0, 1200));
});
