// E2E (настоящий) — TE-025: онбординг «телефон → SMS OTP → client-JWT → link-partner» (FE-018 ч.2, 0.48).
//
// Путь нового устройства (после 0.47/0.48):
//   чистый контекст (нет локального PIN и нет xdiary.legacy) → afterAuth() → Pin.hasPin()=false → openOnboarding().
// Если на устройстве PIN уже есть — путь legacy (журнал), поэтому чистим оба флага перед стартом.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, installBase, waitToken } = require('./api');

const PHONE = '+7999000' + String(Date.now()).slice(-4);
const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';

async function clearFlags(page) {
  await page.evaluate(() => {
    try {
      localStorage.removeItem('xdiary.clientId');
      localStorage.removeItem('xdiary.legacy');
      localStorage.removeItem('xdiary.accessToken');
    } catch (e) {}
  });
}

test('E2E: онбординг — телефон → OTP → link-partner → «я» по clientId', async ({ page }) => {
  await installBase(page);
  await page.goto('/');
  await clearFlags(page);

  await test.step('чистое устройство → экран онбординга «кто ты»', async () => {
    await page.reload();
    await expect(page.locator('#view-onboarding')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#onb-phone')).toBeVisible();
  });

  await test.step('шаг 1: телефон → запрос OTP', async () => {
    await page.fill('#onb-phone-input', PHONE);
    await page.click('#btn-onb-phone');
    await expect(page.locator('#onb-otp')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#onb-otp-phone')).toContainText(PHONE);
  });

  await test.step('шаг 2: OTP ' + TEST_OTP + ' → верификация и client-JWT', async () => {
    await page.fill('#onb-otp-input', TEST_OTP);
    await page.click('#btn-onb-otp');
    await expect(page.locator('#onb-link')).toBeVisible({ timeout: 20000 });
    const clientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId'));
    expect(clientId && clientId !== 'pending', 'clientId сохранён после верификации: ' + clientId).toBe(true);
  });

  await test.step('шаг 3: привязка «я» (или «Пропустить») → журнал', async () => {
    const card = page.locator('#onboarding-list .partner-card').first();
    let mode = 'пропуск (нет партнёров)';
    if (await card.count()) {
      await card.click();
      mode = 'link-partner по карточке';
    } else {
      await page.click('#btn-onboarding-skip');
    }
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    console.log('::notice::TE-025 онбординг пройден: ' + mode + ', phone=' + PHONE);
  });

  await test.step('«я» с сервера после перезагрузки', async () => {
    const clientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId'));
    expect(clientId, 'clientId сохранён').toBeTruthy();
    await page.reload();
    // после reload — снова журнал (клиент верифицирован), либо PIN-экран
    const lock = page.locator('#view-lock');
    if (await lock.isVisible()) {
      await page.fill('#pin-input', PIN);
      await page.click('#pin-submit');
      await waitToken(page);
    }
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

    const me = await page.evaluate(async ({ base, id }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const r = await fetch(base + '/api/v1/clients/' + id, { headers: t ? { Authorization: 'Bearer ' + t } : {} });
      return { status: r.status, data: r.ok ? await r.json() : null };
    }, { base: API_BASE, id: clientId });
    expect(me.status, 'GET /clients/{id}').toBe(200);
    expect(me.data && me.data.verified, 'клиент верифицирован на сервере').toBe(true);
    console.log('::notice::TE-025 «я» с сервера после reload: clientId=' + clientId + ', verified=' + (me.data && me.data.verified));
  });
});
