// E2E (настоящий) — TE-025: онбординг «кто ты» (FE-018, v0.43).
// Чистый контекст: только адрес API, без локального PIN/токена.
// afterAuth(): если у партнёров есть self → журнал; если нет → экран онбординга.
const { test, expect } = require('@playwright/test');
const { PIN, installBase, waitToken } = require('./api');

test('E2E: онбординг — новый контекст → «кто ты» → журнал', async ({ page }) => {
  // локальный PIN НЕ ставим — как на новом устройстве
  await installBase(page);
  await page.goto('/');

  await test.step('вход по PIN (PIN на стенде задан)', async () => {
    await expect(page.locator('#view-lock')).toBeVisible({ timeout: 20000 });
    await page.fill('#pin-input', PIN);
    await page.click('#pin-submit');
    await waitToken(page);
  });

  await test.step('после входа — онбординг «кто ты» либо журнал (если «я» уже есть)', async () => {
    // FE-018: afterAuth() смотрит GET /partners → self
    await expect(page.locator('#view-onboarding').or(page.locator('#view-journal')).first())
      .toBeVisible({ timeout: 20000 });
  });

  await test.step('онбординг: «Пропустить» ведёт в журнал', async () => {
    const onb = page.locator('#view-onboarding');
    if (await onb.isVisible()) {
      // экран онбординга: список партнёров + «Создать партнёра» + «Пропустить»
      await expect(page.locator('#onboarding-list')).toBeVisible();
      await expect(page.locator('#btn-onboarding-add')).toBeVisible();
      await expect(page.locator('#btn-onboarding-skip')).toBeVisible();
      await page.click('#btn-onboarding-skip');
    }
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  });

  await test.step('«я» переживает перезагрузку (сервер, не localStorage)', async () => {
    await page.reload();
    await expect(page.locator('#pin-input')).toBeVisible({ timeout: 20000 });
    await page.fill('#pin-input', PIN);
    await page.click('#pin-submit');
    await waitToken(page);
    // после reload afterAuth() снова решает по self из API — журнал должен открыться
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  });
});
