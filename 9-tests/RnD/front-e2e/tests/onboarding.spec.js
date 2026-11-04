// E2E — TE-049 / TE-025: онбординг **E7** (ADR-024): телефон → OTP → «Кто вы?» (имя + пол) → журнал.
//
// ЧТО ИЗМЕНИЛОСЬ (0.88, FE-038): на онбординге больше нет шага «привяжи себя к партнёру».
// Профиль «я» — это `Client` (`POST /clients/me/profile` → `displayName` + `gender`), партнёр
// добавляется позже отдельным действием на вкладке «Партнёры». Поэтому в этом сценарии
// **нет ни одного обращения к legacy-привязке (шим ADR-024)**: на образах < 0.88 онбординг ещё старый, и такая
// сборка честно пропускается с причиной — версия читается из отданного бандла (`APP_VERSION`),
// то есть из внешнего факта, а не из поведения UI.
//
// Поля экрана «Кто вы?» пока не имеют зафиксированных id (FE-038 в работе), поэтому шаг
// проверяется **без выдуманных селекторов**: сначала утверждается, что экран профиля показан
// (виден `#view-onboarding` и в нём есть поле ввода имени, а журнала нет), затем профиль
// сохраняется по **контракту** (`POST /clients/me/profile` тем же токеном) и проверяется, что
// журнал открылся. Клик по кнопке «Продолжить» станет отдельным шагом, когда @FE зафиксирует id.
const { test, expect } = require('@playwright/test');
const { API_BASE, uniquePhone, installBase, reportStep, expectScreen, dumpViews } = require('./api');

const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const PHONE = uniquePhone('+7999'); // TE-044: 10 млн вариантов, меньше коллизий с накопленными клиентами

/** Версия фронта из отданного бандла. `null` — прочитать не удалось (это не «старая», это «неизвестно»). */
async function deployedVersion(request) {
  const base = API_BASE.replace(/\/api\/v1\/?$/, '');
  const r = await request.get(base + '/js/app.js');
  if (!r.ok()) return null;
  const m = (await r.text()).match(/APP_VERSION\s*=\s*"([\d.]+)"/);
  return m ? m[1] : null;
}

/** Сравнение версий вида `0.88` / `0.87.3`: true, если a >= b. */
function atLeast(a, b) {
  const pa = String(a).split('.').map((x) => parseInt(x, 10) || 0);
  const pb = String(b).split('.').map((x) => parseInt(x, 10) || 0);
  const n = Math.max(pa.length, pb.length);
  for (let i = 0; i < n; i++) {
    const x = pa[i] || 0;
    const y = pb[i] || 0;
    if (x !== y) return x > y;
  }
  return true;
}

test('E2E: онбординг E7 — телефон → OTP → «Кто вы?» (имя + пол) → журнал', async ({ page, request }) => {
  const ver = await deployedVersion(request);
  test.skip(
    !ver || !atLeast(ver, '0.88'),
    'стенд ' + (ver || 'версия неизвестна') + ': онбординг E7 не выкачен (нужен 0.88+, FE-038) — ' +
      'на таком образе путь онбординга другой, и проверять его этой спекой нельзя'
  );

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
    // Поле имени: семантически, без выдуманного id (FE-038 ещё не зафиксировал разметку).
    const nameInput = page.locator('#view-onboarding input:visible').first();
    await expect(nameInput, 'на шаге «Кто вы?» должно быть видимое поле имени').toBeVisible({ timeout: 20000 });
  });

  await test.step('шаг 3: профиль сохраняется контрактом (displayName + gender)', async () => {
    await reportStep(page, 'шаг 3: профиль «я» через POST /clients/me/profile');
    const saved = await page.evaluate(
      async ({ base, name }) => {
        const t = localStorage.getItem('xdiary.accessToken') || '';
        if (!t) return { ok: false, noToken: true };
        const r = await fetch(base + '/api/v1/clients/me/profile', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
          body: JSON.stringify({ displayName: name, gender: 'male' }),
        });
        return { ok: r.ok, status: r.status };
      },
      { base: API_BASE, name: 'e2e-e7-' + Date.now() }
    );
    expect(saved.noToken, 'client-JWT должен быть получен на шаге OTP').toBeFalsy();
    expect(saved.ok, 'POST /clients/me/profile → ' + JSON.stringify(saved)).toBeTruthy();
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
