// E2E (настоящий) — TE-016: снятие PIN.
// Негативы — через API (BE-021: без/с неверным currentPin → 401).
// Позитив — ЧЕРЕЗ UI (Настройки → «Удалить PIN» → подтверждение поверх pin-модалки,
// это регресс-защита FE-011: confirm должен быть кликабелен).
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

test('E2E: снятие PIN — негативы по API, снятие через UI, device-токен', async ({ page, request }) => {
  let removed = false;
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('негатив: pin/remove без currentPin → 401', async () => {
      const bad = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: {} });
      expect(bad.status(), 'pin/remove без currentPin должен быть 401').toBe(401);
      expect(await hasPin(request), 'PIN на месте после неудачного снятия').toBe(true);
    });

    await test.step('негатив: pin/remove с неверным currentPin → 401', async () => {
      const bad = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: { currentPin: '0000' } });
      expect(bad.status(), 'pin/remove с неверным currentPin должен быть 401').toBe(401);
      expect(await hasPin(request), 'PIN на месте после неверного currentPin').toBe(true);
    });

    await test.step('UI: открыть настройки и модалку PIN', async () => {
      await page.click('#tab-settings');
      await expect(page.locator('#view-settings')).toBeVisible();
      await page.click('#btn-pin-open');
      await expect(page.locator('#pin-modal')).toBeVisible();
      await page.fill('#pin-current', PIN);
    });

    await test.step('UI: «Удалить PIN» → confirm поверх pin-модалки (регресс FE-011)', async () => {
      await page.click('#btn-pin-remove', { timeout: 10000 });
      await expect(page.locator('#confirm-modal')).toBeVisible({ timeout: 10000 });
      await page.click('#confirm-yes', { timeout: 10000 });
      await page.waitForTimeout(2000);
    });

    await test.step('проверка: pin/has=false, старый PIN 401, device-токен 200', async () => {
      expect(await hasPin(request), 'после снятия pin/has = false').toBe(false);
      expect(await tokenStatus(request, PIN), 'старый PIN больше не выдаёт токен').toBe(401);
      removed = true;
      expect(await tokenStatus(request, ''), 'device-токен без PIN работает').toBe(200);
    });
  } finally {
    if (removed) {
      await test.step('finally: вернуть PIN', async () => {
        const r = await request.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: PIN } });
        console.log('restore pin:', r.status());
        expect(await hasPin(request), 'PIN восстановлен в finally').toBe(true);
      });
    }
  }
});
