// E2E (настоящий) — TE-016: снятие PIN через настройки (BE-021: нужен currentPin).
// Безопасно для dev-стенда: в finally PIN возвращается через pin/set.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, loginWithPin } = require('./api');

async function tokenStatus(request, pin) {
  const r = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin } });
  return r.status();
}

async function hasPin(request) {
  const r = await request.get(API_BASE + '/api/v1/privacy/pin/has');
  return r.ok() ? await r.json() : null;
}

test('E2E: снятие PIN — без currentPin 401, с currentPin снимает и device-токен работает', async ({ page, request }) => {
  let removed = false;
  try {
    await loginWithPin(page);

    // --- негатив (BE-021): без currentPin снять нельзя
    const bad = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: {} });
    expect(bad.status(), 'pin/remove без currentPin должен быть 401').toBe(401);
    expect(await hasPin(request), 'PIN на месте после неудачного снятия').toBe(true);

    // --- позитив: снятие через UI (Настройки → «Удалить PIN» → «Да»)
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
    await page.click('#btn-pin-open');
    await expect(page.locator('#pin-modal')).toBeVisible();
    await page.fill('#pin-current', PIN);
    await page.click('#btn-pin-remove');
    await expect(page.locator('#confirm-modal')).toBeVisible();
    await page.click('#confirm-yes');
    await page.waitForTimeout(1500);

    expect(await hasPin(request), 'после снятия pin/has = false').toBe(false);
    expect(await tokenStatus(request, PIN), 'старый PIN больше не выдаёт токен').toBe(401);
    removed = true;
    expect(await tokenStatus(request, ''), 'device-токен без PIN работает').toBe(200);
  } finally {
    if (removed) {
      const r = await request.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: PIN } });
      console.log('restore pin:', r.status());
      expect(await hasPin(request), 'PIN восстановлен в finally').toBe(true);
    }
  }
});
