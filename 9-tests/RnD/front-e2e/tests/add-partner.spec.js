// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
// API замокан через page.route, чтобы тест не зависел от живого бэкенда.
//
// Важно (фикс падения в CI): приложение по умолчанию ходит на
// https://xdiary-vps1.taile0b6ee.ts.net (кросс-домен). Мок page.route без CORS-заголовков
// браузер режет → fetch reject → UI встаёт → таймаут.
// Здесь: (1) apiBase переключаем на same-origin (init-script), (2) на всякий случай
// отдаём CORS-заголовки в fulfill.
const { test, expect } = require('@playwright/test');

const BASE = 'http://127.0.0.1:4173';

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const partners = [];

  // Приложение возьмёт baseUrl из localStorage → same-origin, CORS не нужен.
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
      headers: {
        'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Headers': '*',
        'Access-Control-Allow-Methods': '*',
      },
      body: JSON.stringify(body),
    });
  });

  await page.goto('/');
  await expect(page.locator('#view-journal')).toBeVisible();

  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();
  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();

  await page.fill('#pe-name', 'Аня');
  await page.click('#btn-partner-save');

  await expect(page.locator('#view-partners')).toBeVisible();
  await expect(page.locator('#partners-list')).toContainText('Аня');
  expect(partners.map((x) => x.name)).toContain('Аня');
});
