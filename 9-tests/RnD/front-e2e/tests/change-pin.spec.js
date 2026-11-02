// E2E — TE-037: смена ЛОКАЛЬНОГО PIN через UI Настроек.
//
// Канон MVP (ADR-021 п.3, AR-009 = B): PIN живёт локально в `window.Pin` (IndexedDB),
// серверный `/privacy/pin/*` — legacy и условием прохождения НЕ является.
// Поэтому спек проверяет ровно три вещи (критерии TE-037 п.1):
//   1) новый локальный PIN пускает в журнал;
//   2) старый локальный PIN больше не проходит проверку;
//   3) `Pin.hasPin()` = true, а в `finally` исходный PIN возвращается (чистота стенда).
const { test, expect } = require('@playwright/test');
const { PIN, loginWithPin, unlockIfLocked } = require('./api');

const NEW_PIN = process.env.XDIARY_E2E_NEW_PIN || '5678';

async function hasPinLocal(page) {
  return page.evaluate(() => window.Pin.hasPin());
}

async function verifyLocal(page, pin) {
  return page.evaluate((v) => window.Pin.verifyPin(v), pin);
}

test('E2E: смена локального PIN — новый пускает, старый больше нет', async ({ page }) => {
  try {
    await test.step('login по локальному PIN', async () => {
      await loginWithPin(page, PIN);
      expect(await hasPinLocal(page), 'локальный PIN стоит после loginWithPin').toBe(true);
    });

    await test.step('UI: Настройки → модалка PIN → новый PIN', async () => {
      await page.click('#tab-settings');
      await expect(page.locator('#view-settings')).toBeVisible();
      await page.click('#btn-pin-open');
      await expect(page.locator('#pin-modal')).toBeVisible();
      await page.fill('#pin-current', PIN);
      await page.fill('#pin-new', NEW_PIN);
      await page.fill('#pin-confirm', NEW_PIN);
      // openPinDialog() асинхронный (Pin.hasPin().then) и чистит поля — проверяем,
      // что значения реально доехали до сабмита
      await expect(page.locator('#pin-current')).toHaveValue(PIN);
      await expect(page.locator('#pin-new')).toHaveValue(NEW_PIN);
      await page.click('#btn-pin-set');
      await expect(page.locator('#pin-modal')).toBeHidden({ timeout: 15000 });
    });

    await test.step('новый PIN верен, старый — нет', async () => {
      expect(await verifyLocal(page, NEW_PIN), 'новый локальный PIN верен').toBe(true);
      expect(await verifyLocal(page, PIN), 'старый локальный PIN больше не верен').toBe(false);
      expect(await hasPinLocal(page), 'Pin.hasPin() = true после смены').toBe(true);
    });

    await test.step('перезагрузка: замок снимается НОВЫМ PIN', async () => {
      await page.reload();
      await page.waitForTimeout(1500);
      if (await page.locator('#view-lock').isVisible()) {
        await unlockIfLocked(page, NEW_PIN);
        await expect(page.locator('#view-lock')).toBeHidden({ timeout: 15000 });
      }
    });
  } finally {
    // Чистота стенда: возвращаем исходный локальный PIN (серверный не трогаем — legacy).
    await page.evaluate((v) => window.Pin.setPin(v), PIN).catch(() => {});
    const back = await hasPinLocal(page).catch(() => null);
    console.log('::notice::restore local pin (change-pin): hasPin=' + back);
  }
});
