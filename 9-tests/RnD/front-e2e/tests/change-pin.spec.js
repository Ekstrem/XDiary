// E2E (настоящий): смена PIN — живое приложение + живой шлюз.
// Ловит FE-009: `applyPin` проверяет текущий PIN только локально и шлёт
// `setPin(nw)` без `currentPin`; BE-020 на это отвечает 400 → локальный PIN меняется,
// серверный — нет (приложение при этом говорит «PIN сохранён»).
// Поэтому источник истины — сам шлюз: после смены новый PIN должен работать, старый — нет.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, loginWithPin } = require('./api');

const NEW_PIN = '5678';

test('E2E: смена PIN — сервер принимает новый PIN, старый перестаёт работать', async ({ page, request }) => {
  const oldPin = PIN;
  try {
    await loginWithPin(page, oldPin);

    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
    await page.click('#btn-pin-open');
    await expect(page.locator('#pin-modal')).toBeVisible();

    await page.fill('#pin-current', oldPin);
    await page.fill('#pin-new', NEW_PIN);
    await page.fill('#pin-confirm', NEW_PIN);
    await page.click('#btn-pin-set');

    // сервер — источник истины
    const withNew = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin: NEW_PIN } });
    expect(withNew.status(), 'сервер должен принять НОВЫЙ PIN (FE-009: setPin без currentPin → 400)').toBe(200);

    const withOld = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin: oldPin } });
    expect(withOld.status(), 'старый PIN после смены работать не должен').toBe(401);
  } finally {
    // всегда возвращаем исходный PIN, чтобы не ломать API-E2E (XDIARY_E2E_PIN=1234);
    // если смена не удалась, сервер остался на oldPin и этот вызов просто вернёт 400
    const r = await request.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: oldPin, currentPin: NEW_PIN } });
    console.log('restore pin:', r.status());
  }
});
