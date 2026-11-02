// ============================================================================
// LEGACY (ADR-008 / ADR-021 п.3): этот спек проверяет СЕРВЕРНЫЙ PIN
// (`/api/v1/privacy/token`, `/privacy/pin/set`). По канону MVP PIN — ЛОКАЛЬНЫЙ
// (`window.Pin`, IndexedDB), серверный оставлен для device-токена и в MVP не
// создаётся. Поэтому спек исключён из гейта через `describe.skip`, но НЕ удалён:
// переписать под локальный PIN — задача TE-037 (P2).
// ============================================================================
// E2E (настоящий): смена PIN — живое приложение + живой шлюз.
// Ловит FE-009: `applyPin` проверяет текущий PIN только локально и шлёт
// `setPin(nw)` без `currentPin`; BE-020 на это отвечает 400 → локальный PIN меняется,
// серверный — нет (приложение при этом говорит «PIN сохранён»).
// Поэтому источник истины — сам шлюз: после смены новый PIN должен работать, старый — нет.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, loginWithPin } = require('./api');

const NEW_PIN = '5678';

test.describe.skip('legacy: серверный PIN (ADR-008, вне MVP) — переписать в TE-037', () => {
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
    // openPinDialog открывается асинхронно и может чистить поля — дожидаемся,
    // что currentPin реально остался в поле (иначе setPin уйдёт с currentPin="" → 401)
    await expect(page.locator('#pin-current')).toHaveValue(oldPin);
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
});
