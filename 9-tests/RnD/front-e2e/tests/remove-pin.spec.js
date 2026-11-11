// E2E — TE-037: снятие ЛОКАЛЬНОГО PIN через UI.
//
// Канон MVP (ADR-021 п.3, AR-009 = B): PIN локальный (`window.Pin`), серверный
// `/privacy/pin/*` — legacy и условием прохождения НЕ является (критерий TE-037 п.1/п.3).
// Поэтому серверные негативы вынесены в `describe.skip` с причиной, а гейт проверяет:
//   1) `window.Pin.hasPin()` = true до снятия;
//   2) `window.Pin.hasPin()` = false после снятия через UI;
//   3) замок (`#view-lock`) больше не показывается — журнал открывается без PIN.
//
// MVP-истории: US-11
//   US-11 вход по PIN (снятие PIN)
const { test, expect } = require('@playwright/test');
const { waitAppReady, API_BASE, PIN, loginWithPin } = require('./api');

async function hasPinLocal(page) {
  return page.evaluate(() => window.Pin.hasPin());
}

// ============================================================================
// LEGACY: серверный PIN — legacy (ADR-021 п.3 / AR-009 = B), вне MVP.
// Негативы `/privacy/pin/remove` остались как историческая регресс-защита BE-021,
// но НЕ должны быть условием прохождения ни одного MVP-спека.
// ============================================================================
test.describe.skip('legacy: серверный PIN (ADR-021 / AR-009)   вне MVP', () => {
  // ADR-021 этап 2 (@AR 2026-11-16): в `9-tests` не должно быть `fetch`/`request` в `/privacy/*`
  // даже в skipped-блоке   только описания и ссылки. Исторические негативы сняты из кода и
  // остаются в истории задач: BE-021 (снятие серверного PIN), BE-034 (pin/has скоупится по
  // устройству), TE-037 (снятие PIN).
  // Что проверялось: `pin/remove` без `currentPin`  401; с неверным  401; после снятия
  // `pin/has` = false; device-токен без PIN  200; затем PIN восстанавливался.
  // Возвращать это в код не нужно: канон MVP   ЛОКАЛЬНЫЙ PIN (`window.Pin`), он проверяется ниже.
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
      await waitAppReady(page);
      // Восстановлено (правка): это ожидание после reload — перезагрузка приложения, а не локальный переход.
      await expect(page.locator('#view-lock')).toBeHidden({ timeout: 20000 });
    });
  } finally {
    // Чистота стенда: возвращаем локальный PIN (серверный не трогаем — legacy).
    await page.evaluate((v) => window.Pin.setPin(v), PIN).catch(() => {});
    const back = await hasPinLocal(page).catch(() => null);
    console.log('::notice::restore local pin (remove-pin): removed=' + removed + ' hasPin=' + back);
  }
});
