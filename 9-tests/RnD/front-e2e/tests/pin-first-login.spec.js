// E2E (настоящий) — TE-023 / FE-007: устройство без локального PIN, а на стенде PIN есть.
// Путь PO: login("") → 401 → экран PIN → неверный PIN → ошибка → верный PIN → журнал.
const { test, expect } = require('@playwright/test');
const { PIN, installBase, waitToken } = require('./api');

test('E2E: устройство без локального PIN при PIN на стенде → экран PIN → вход', async ({ page }) => {
  // локальный PIN НЕ ставим — только адрес API (как на новом устройстве)
  await installBase(page);
  await page.goto('/');

  await test.step('показан экран PIN (401 от login(""))', async () => {
    await expect(page.locator('#view-lock')).toBeVisible({ timeout: 20000 });
    expect(await page.evaluate(() => !!localStorage.getItem('xdiary.accessToken')), 'JWT не должен появиться').toBe(false);
  });

  await test.step('неверный PIN → «Неверный PIN», без входа', async () => {
    await page.fill('#pin-input', '0000');
    await page.click('#pin-submit');
    await expect(page.locator('#pin-error')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('#view-lock')).toBeVisible();
    expect(await page.evaluate(() => !!localStorage.getItem('xdiary.accessToken')), 'JWT не должен появиться').toBe(false);
  });

  await test.step('верный PIN → JWT + журнал', async () => {
    await page.fill('#pin-input', PIN);
    await page.click('#pin-submit');
    await waitToken(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  });
});
