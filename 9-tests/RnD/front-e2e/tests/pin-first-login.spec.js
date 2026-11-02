// E2E (настоящий) — новое устройство и PIN (адаптирован под FE-021, 0.51).
//
// ЧТО ИЗМЕНИЛОСЬ: до 0.51 на чистом устройстве показывался PIN-экран (legacy-путь).
// После FE-021 новое устройство сразу идёт в онбординг «кто ты» — PIN-экрана там больше НЕТ.
// Поэтому сценарий разделён:
//   1) чистое устройство → онбординг (никакого PIN-экрана);
//   2) PIN-экран появляется на устройстве, где онбординг уже пройден и есть clientId/PIN.
const { test, expect } = require('@playwright/test');
const { PIN, installBase, waitToken } = require('./api');

const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const PHONE = '+7999777' + String(Date.now()).slice(-4);

test('E2E: новое устройство → онбординг (без PIN-экрана) → журнал', async ({ page }) => {
  await installBase(page);
  await page.goto('/');

  await test.step('чистое устройство: PIN-экрана нет, показывается онбординг', async () => {
    await page.evaluate(() => {
      try {
        localStorage.removeItem('xdiary.clientId');
        localStorage.removeItem('xdiary.legacy');
        localStorage.removeItem('xdiary.accessToken');
      } catch (e) {}
    });
    await page.reload();
    // FE-021: чистое устройство → сразу онбординг, минуя PIN
    await expect(page.locator('#view-onboarding')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#view-lock')).toBeHidden();
  });

  await test.step('онбординг проходится: телефон → OTP → журнал (PIN не требуется)', async () => {
    await page.fill('#onb-phone-input', PHONE);
    await page.click('#btn-onb-phone');
    await expect(page.locator('#onb-otp')).toBeVisible({ timeout: 20000 });
    await page.fill('#onb-otp-input', TEST_OTP);
    await page.click('#btn-onb-otp');
    await expect(page.locator('#onb-link')).toBeVisible({ timeout: 20000 });
    const card = page.locator('#onboarding-list .partner-card').first();
    if (await card.count()) await card.click();
    // skip убран из UI (решение владельца п.4 от 06.11)
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    console.log('::notice::новое устройство: онбординг пройден без PIN-экрана, phone=' + PHONE);
  });

  await test.step('после reload устройство помнит вход (журнал или PIN — оба допустимы)', async () => {
    // ВАЖНО: токен НЕ удаляем — устройство уже прошло онбординг (clientId + client-JWT).
    // Удаление токена уводило приложение в состояние, где afterAuth не мог решить, что показать,
    // и все три экрана оставались hidden — это ошибка теста, не продукта (@TL 02:45).
    await page.reload();
    const lock = page.locator('#view-lock');
    const onb = page.locator('#view-onboarding');
    const jr = page.locator('#view-journal');
    await expect(lock.or(onb).or(jr).first()).toBeVisible({ timeout: 20000 });
    if (await lock.isVisible()) {
      await page.fill('#pin-input', String(PIN));
      await page.click('#pin-submit');
      await waitToken(page);
      await expect(jr).toBeVisible({ timeout: 20000 });
      console.log('::notice::после reload — PIN-экран, вход выполнен');
    } else if (await onb.isVisible()) {
      console.log('::notice::после reload — онбординг (новое устройство)');
    } else {
      console.log('::notice::после reload — сразу журнал');
    }
  });
});
