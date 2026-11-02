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

// ВАЖНО (@TL 08:50): pin/has скоупится по УСТРОЙСТВУ. Голый `request.get` идёт без
// device-токена и всегда отдаёт false — это был баг спека, а не продукта (BE-034 отклонена).
// Поэтому проверяем через страницу, где device-токен есть.
async function hasPinServer(page) {
  return page.evaluate(async (base) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/privacy/pin/has', {
      headers: t ? { Authorization: 'Bearer ' + t } : {},
    });
    if (!r.ok) return null;
    const j = await r.json();
    return typeof j === 'boolean' ? j : !!(j && (j.hasPin !== undefined ? j.hasPin : j.value));
  }, API_BASE);
}

async function hasPinLocal(page) {
  return page.evaluate(() => window.Pin.hasPin());
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
      expect(await hasPinLocal(page), 'локальный замок стоит после loginWithPin').toBe(true);
    });

    await test.step('негатив: pin/remove с неверным currentPin → 401', async () => {
      const bad = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: { currentPin: '0000' } });
      expect(bad.status(), 'pin/remove с неверным currentPin должен быть 401').toBe(401);
    });

    await test.step('UI: открыть настройки и модалку PIN', async () => {
      await page.click('#tab-settings');
      await expect(page.locator('#view-settings')).toBeVisible();
      await page.click('#btn-pin-open');
      await expect(page.locator('#pin-modal')).toBeVisible();
      await page.fill('#pin-current', PIN);
      // модалка открывается асинхронно (Pin.hasPin().then) и может чистить поле —
      // дожидаемся, что наш PIN реально в поле до «Удалить PIN»
      await expect(page.locator('#pin-current')).toHaveValue(PIN);
    });

    await test.step('UI: «Удалить PIN» → confirm поверх pin-модалки (регресс FE-011)', async () => {
      await page.click('#btn-pin-remove', { timeout: 10000 });
      await expect(page.locator('#confirm-modal')).toBeVisible({ timeout: 10000 });
      await page.click('#confirm-yes', { timeout: 10000 });
      await page.waitForTimeout(2000);
    });

    await test.step('проверка: pin/has=false и device-токен работает', async () => {
      // При отсутствии PIN сервер выдаёт device-токен (BE-019) на любой pin,
      // поэтому «старый PIN → 401» здесь неверно. Проверяем наблюдаемое:
      // PIN снят + device-токен выдаётся.
      expect(await hasPinServer(request), 'после снятия pin/has = false').toBe(false);
      removed = true;
      expect(await tokenStatus(request, ''), 'device-токен без PIN работает').toBe(200);
      expect(await tokenStatus(request, PIN), 'снятый PIN не мешает device-токену').toBe(200);
    });
  } finally {
    if (removed) {
      await test.step('finally: вернуть PIN', async () => {
        const r = await request.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: PIN } });
        console.log('restore pin:', r.status());
        expect(await hasPinServer(request), 'PIN восстановлен в finally').toBe(true);
      });
    }
  }
});
