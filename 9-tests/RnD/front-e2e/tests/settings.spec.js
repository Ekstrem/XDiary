// E2E (настоящий) — TE-021/2: настройки.
// В приложении из «настроек» реально есть только **пол** (myGender) + адрес API;
// темы/единиц измерения в UI нет — это отмечено в TE-021.
// Проверяем: пол переключается и сохраняется после перезагрузки, состояние кнопки активное.
const { test, expect } = require('@playwright/test');
const { loginWithPin, unlockIfLocked } = require('./api');

test('E2E: настройки — пол сохраняется после перезагрузки', async ({ page }) => {
  await test.step('login по PIN', async () => {
    await loginWithPin(page);
  });

  await test.step('настройки: по умолчанию «Мужской»', async () => {
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
    await expect(page.locator('#my-gender-male')).toHaveClass(/active/);
  });

  await test.step('переключить на «Женский»', async () => {
    await page.click('#my-gender-female');
    await expect(page.locator('#my-gender-female')).toHaveClass(/active/);
    await expect(page.locator('#my-gender-male')).not.toHaveClass(/active/);
  });

  await test.step('после перезагрузки выбранный пол сохранился', async () => {
    await page.reload();
    await unlockIfLocked(page);
    await page.click('#tab-settings');
    await expect(page.locator('#my-gender-female')).toHaveClass(/active/);
  });

  await test.step('вернуть «Мужской» (уборка)', async () => {
    await page.click('#my-gender-male');
    await expect(page.locator('#my-gender-male')).toHaveClass(/active/);
  });
});
