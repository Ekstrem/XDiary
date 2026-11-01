// E2E (настоящий) — TE-025: онбординг «телефон → SMS OTP → client-JWT → link-partner» (FE-018 ч.2, 0.46).
//
// Важно про вход в онбординг: по коду `afterAuth()` включает клиентский онбординг ТОЛЬКО если в
// localStorage уже есть `xdiary.clientId` (флаг «онбординг начат»). На абсолютно чистом устройстве
// путь идёт в журнал (legacy device-token). Поэтому сценарий онбординга симулируем так, как это
// делает сам пользователь после начала регистрации: выставляем флаг clientId и перезагружаемся.
// Отсутствие входа в онбординг с чистого устройства — отдельная продуктовая находка (в чат).
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, installBase, waitToken } = require('./api');

const PHONE = '+7999000' + String(Date.now()).slice(-4);
const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';

async function unlock(page) {
  await expect(page.locator('#view-lock')).toBeVisible({ timeout: 20000 });
  await page.fill('#pin-input', PIN);
  await page.click('#pin-submit');
  await waitToken(page);
}

test('E2E: онбординг — телефон → OTP → link-partner → «я» по clientId', async ({ page }) => {
  await installBase(page);
  await page.goto('/');

  await test.step('вход по PIN (устройство)', async () => {
    await unlock(page);
  });

  await test.step('начало онбординга: флаг clientId поставлен, перезагрузка → экран онбординга', async () => {
    await page.evaluate(() => localStorage.removeItem('xdiary.clientId'));
    // симулируем «онбординг начат» — как после первого шага регистрации
    await page.evaluate(() => localStorage.setItem('xdiary.clientId', 'pending'));
    await page.reload();
    await unlock(page);
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

  await test.step('шаг 3: link-partner → журнал', async () => {
    const card = page.locator('#onboarding-list .partner-card').first();
    let linkedByCard = false;
    if (await card.count()) {
      await card.click();
      linkedByCard = true;
    } else {
      // нет партнёров для выбора — путь «Пропустить»
      await page.click('#btn-onboarding-skip');
    }
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    console.log('::notice::TE-025 онбординг пройден: ' + (linkedByCard ? 'link-partner по карточке' : 'пропуск (нет партнёров)') +
      ', phone=' + PHONE);
  });

  await test.step('«я» переживает перезагрузку: clientId из localStorage + /clients/me с сервера', async () => {
    const clientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId'));
    expect(clientId, 'clientId сохранён').toBeTruthy();
    await page.reload();
    await unlock(page);
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
