// E2E — TE-049 / TE-025: онбординг **E7** (ADR-024): телефон → OTP → «Кто вы?» (имя + пол) → журнал.
//
// 0.88 (FE-038) выкачен: шага «привяжи себя к партнёру» больше нет. Профиль «я» — это `Client`
// (`POST /clients/me/profile` → `displayName` + `gender`), партнёр добавляется позже на вкладке
// «Партнёры». Поэтому в этом сценарии **нет ни одного обращения к legacy-привязке**, и шаг профиля
// проверяется реальными id экрана «Кто вы?»: `#onb-name`, `#onb-gender-male`, `#btn-onb-profile`,
// `#onb-profile-error` (зафиксированы @FE, переданы @TL 2026-11-04 05:13/10:05).
//
// Версионного skip здесь больше НЕТ (@TL 10:05): фронт 0.88 на проде (`xdiary-v88`), значит спека
// обязана выполняться. Если её запустить на старом образе — она упадёт на отсутствии `#onb-name`,
// и это правильный сигнал: гейт должен соответствовать тому, что деплоится (§5.4), а не тихо
// пропускать несовпадение.
const { test, expect } = require('@playwright/test');
const { API_BASE, uniquePhone, installBase, reportStep, expectScreen, dumpViews } = require('./api');

const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const PHONE = uniquePhone('+7999'); // TE-044: 10 млн вариантов, меньше коллизий с накопленными клиентами

test('E2E: онбординг E7 — телефон → OTP → «Кто вы?» (имя + пол) → журнал', async ({ page }) => {

  await test.step('чистое устройство → экран онбординга', async () => {
    await reportStep(page, 'шаг 0: чистое устройство → онбординг');
    await installBase(page);
    await page.goto('/');
    await page.evaluate(() => {
      try {
        localStorage.removeItem('xdiary.clientId');
        localStorage.removeItem('xdiary.legacy');
        localStorage.removeItem('xdiary.accessToken');
      } catch (e) {}
    });
    await page.reload();
    await expectScreen(page, '#view-onboarding', 'чистое устройство → онбординг', 20000);
    await expect(page.locator('#view-lock')).toBeHidden();
  });

  await test.step('шаг 1: телефон → запрос OTP', async () => {
    await reportStep(page, 'шаг 1: телефон → запрос OTP');
    await page.fill('#onb-phone-input', PHONE);
    await page.click('#btn-onb-phone');
    await expect(page.locator('#onb-otp')).toBeVisible({ timeout: 20000 });
  });

  await test.step('шаг 2: OTP → «Кто вы?» (имя + пол), без шага привязки', async () => {
    await reportStep(page, 'шаг 2: OTP → «Кто вы?»');
    await page.fill('#onb-otp-input', TEST_OTP);
    await page.click('#btn-onb-otp');
    await expect(page.locator('#onb-otp-error')).toBeHidden({ timeout: 20000 });
    // E7: после OTP остаёмся в онбординге, но уже на шаге профиля — журнала ещё нет.
    await dumpViews(page, 'onboarding E7: после OTP');
    await expect(page.locator('#view-onboarding')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#view-journal')).toBeHidden();
    // TE-049: id профильного шага зафиксированы @FE (`@TL 2026-11-15 05:13`):
    // `#onb-name`, `#onb-gender-male`, `#onb-gender-female`, `#btn-onb-profile`, `#onb-profile-error`.
    await expect(page.locator('#onb-name'), 'шаг «Кто вы?»: поле имени').toBeVisible({ timeout: 20000 });
    await expect(page.locator('#btn-onb-profile'), 'шаг «Кто вы?»: кнопка «Продолжить»').toBeVisible({ timeout: 10000 });
  });

  await test.step('шаг 3: «Кто вы?» — имя + пол кликом, профиль сохраняется формой', async () => {
    await reportStep(page, 'шаг 3: форма «Кто вы?» → Продолжить');
    const displayName = 'e2e-e7-' + Date.now();
    await page.fill('#onb-name', displayName);
    await page.click('#onb-gender-male');
    await expect(page.locator('#onb-profile-error')).toBeHidden({ timeout: 5000 });
    await page.click('#btn-onb-profile');
    // Форма уходит в журнал: профиль сохранён её собственным запросом (POST /clients/me/profile).
    await expectScreen(page, '#view-journal', 'форма «Кто вы?» → журнал', 25000);
    await expect(page.locator('#view-onboarding')).toBeHidden();
    await page.evaluate((n) => { window.__e7Name = n; }, displayName);
    expect(displayName, 'имя для шага 5').toBeTruthy();
  });

  await test.step('шаг 4: повторный вход — сразу журнал, онбординг не повторяется', async () => {
    await reportStep(page, 'шаг 4: reload → журнал');
    await page.reload();
    await dumpViews(page, 'onboarding E7: после сохранения профиля и reload');
    await expectScreen(page, '#view-journal', 'профиль сохранён → журнал', 20000);
    await expect(page.locator('#view-onboarding')).toBeHidden();
  });

  await test.step('шаг 5: «я» — это профиль Client, а не партнёр', async () => {
    await reportStep(page, 'шаг 5: GET /clients/me — поля профиля, без linkedPartnerId');
    const me = await page.evaluate(async ({ base }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const r = await fetch(base + '/api/v1/clients/me', { headers: { Authorization: 'Bearer ' + t } });
      const j = r.ok ? await r.json().catch(() => null) : null;
      return { status: r.status, body: j };
    }, { base: API_BASE });
    expect(me.status, 'GET /clients/me → ' + me.status).toBe(200);
    expect(me.body.displayName, 'профиль должен содержать displayName').toBeTruthy();
    expect(['male', 'female'], 'пол — канонический (ADR-024 §3)').toContain(me.body.gender);
    expect(me.body, 'в E7 у клиента больше нет linkedPartnerId').not.toHaveProperty('linkedPartnerId');
  });
});
