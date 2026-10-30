// RnD: UI-автотест фронта (Playwright). Сценарий — «Добавить партнёра».
// API замокан через page.route, чтобы тест не зависел от живого бэкенда.
const { test, expect } = require('@playwright/test');

test('Добавить партнёра: сохранение и появление в списке', async ({ page }) => {
  const partners = [];

  await page.route('**/api/v1/**', async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const p = url.pathname;
    const method = req.method();
    let body = {};

    if (p === '/api/v1/privacy/token') body = { accessToken: 'test-access', refreshToken: 'test-refresh' };
    else if (p === '/api/v1/partners' && method === 'GET') body = partners;
    else if (p === '/api/v1/partners' && method === 'POST') {
      const payload = JSON.parse(req.postData() || '{}');
      const created = { id: 'p' + (partners.length + 1), name: payload.name, photo: '', gender: 'female', notes: '' };
      partners.push(created);
      body = created;
    } else if (p.startsWith('/api/v1/partners/') && method === 'PUT') body = {};
    else if (p === '/api/v1/partners' && method === 'DELETE') body = {};
    else if (p === '/api/v1/journal/records') body = [];
    else body = {};

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });

  await page.goto('/');

  // Свежий профиль → приложение сразу в журнале (локального PIN нет).
  await expect(page.locator('#view-journal')).toBeVisible();

  // Партнёры → добавить.
  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();
  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();

  await page.fill('#pe-name', 'Аня');
  await page.click('#btn-partner-save');

  // После сохранения — снова партнёры, и там есть «Аня».
  await expect(page.locator('#view-partners')).toBeVisible();
  await expect(page.locator('#partners-list')).toContainText('Аня');

  // Запрос POST /api/v1/partners действительно был.
  expect(partners.map((x) => x.name)).toContain('Аня');
});
