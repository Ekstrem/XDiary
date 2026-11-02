// E2E — TE-037: снятие ЛОКАЛЬНОГО PIN через UI.
//
// Канон MVP (ADR-021 п.3, AR-009 = B): PIN локальный (`window.Pin`), серверный
// `/privacy/pin/*` — legacy и условием прохождения НЕ является (критерий TE-037 п.1/п.3).
// Поэтому серверные негативы вынесены в `describe.skip` с причиной, а гейт проверяет:
//   1) `window.Pin.hasPin()` = true до снятия;
//   2) `window.Pin.hasPin()` = false после снятия через UI;
//   3) замок (`#view-lock`) больше не показывается — журнал открывается без PIN.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, loginWithPin } = require('./api');

async function hasPinLocal(page) {
  return page.evaluate(() => window.Pin.hasPin());
}

// ============================================================================
// LEGACY: серверный PIN — legacy (ADR-021 п.3 / AR-009 = B), вне MVP.
// Негативы `/privacy/pin/remove` остались как историческая регресс-защита BE-021,
// но НЕ должны быть условием прохождения ни одного MVP-спека.
// ============================================================================
test.describe.skip('legacy: серверный PIN (ADR-021 / AR-009) — вне MVP', () => {
  async function tokenStatus(request, pin) {
    const r = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin } });
    return r.status();
  }

  // pin/has скоупится по УСТРОЙСТВУ: голый `request.get` идёт без device-токена
  // и всегда отдаёт false (это был баг спека, а не продукта — BE-034 отклонена).
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

  test('серверные негативы pin/remove и device-токен', async ({ page, request }) => {
    await loginWithPin(page);

    const noCurrent = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: {} });
    expect(noCurrent.status(), 'pin/remove без currentPin должен быть 401').toBe(401);

    const badCurrent = await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: { currentPin: '0000' } });
    expect(badCurrent.status(), 'pin/remove с неверным currentPin должен быть 401').toBe(401);

    await request.post(API_BASE + '/api/v1/privacy/pin/remove', { data: { currentPin: PIN } });
    expect(await hasPinServer(page), 'после снятия pin/has = false').toBe(false);
    expect(await tokenStatus(request, ''), 'device-токен без PIN работает').toBe(200);
    expect(await tokenStatus(request, PIN), 'снятый PIN не мешает device-токену').toBe(200);

    await request.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: PIN } });
    expect(await hasPinServer(page), 'PIN восстановлен').toBe(true);
  });
});

test('E2E: снятие локального PIN через UI → журнал без замка', async ({ page }) => {
  let removed = false;
  try {
    await test.step('login по локальному PIN', async () => {
      await loginWithPin(page, PIN);
      expect(await hasPinLocal(page), 'локальный PIN стоит после loginWithPin').toBe(true);
    });

    await test.step('UI: Настройки → модалка PIN (currentPin заполнен)', async () => {
      await page.click('#tab-settings');
      await expect(page.locator('#view-settings')).toBeVisible();
      await page.click('#btn-pin-open');
      await expect(page.locator('#pin-modal')).toBeVisible();
      await page.fill('#pin-current', PIN);
      // openPinDialog() асинхронный и чистит поле — дожидаемся, что PIN реально в поле
      await expect(page.locator('#pin-current')).toHaveValue(PIN);
    });

    await test.step('UI: «Удалить PIN» → confirm поверх модалки (регресс FE-011)', async () => {
      await page.click('#btn-pin-remove', { timeout: 10000 });
      await expect(page.locator('#confirm-modal')).toBeVisible({ timeout: 10000 });
      await page.click('#confirm-yes', { timeout: 10000 });
      // removePinNow(): сервер (может ответить ошибкой) → Pin.removePin() → logout.
      // Условие прохождения — ЛОКАЛЬНОЕ состояние, поэтому ждём именно его.
      await expect.poll(async () => hasPinLocal(page), { timeout: 15000 }).toBe(false);
      removed = true;
    });

    await test.step('проверка: локального PIN нет, замок не показывается', async () => {
      expect(await hasPinLocal(page), 'window.Pin.hasPin() = false после снятия').toBe(false);
      await page.reload();
      await page.waitForTimeout(1500);
      await expect(page.locator('#view-lock')).toBeHidden({ timeout: 10000 });
    });
  } finally {
    // Чистота стенда: возвращаем локальный PIN (серверный не трогаем — legacy).
    await page.evaluate((v) => window.Pin.setPin(v), PIN).catch(() => {});
    const back = await hasPinLocal(page).catch(() => null);
    console.log('::notice::restore local pin (remove-pin): removed=' + removed + ' hasPin=' + back);
  }
});
