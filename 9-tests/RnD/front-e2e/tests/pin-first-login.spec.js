// E2E (настоящий) — новое устройство и PIN (адаптирован под FE-021, 0.51).
//
// ЧТО ИЗМЕНИЛОСЬ: до 0.51 на чистом устройстве показывался PIN-экран (legacy-путь).
// После FE-021 новое устройство сразу идёт в онбординг «кто ты» — PIN-экрана там больше НЕТ.
// Поэтому сценарий разделён:
//   1) чистое устройство → онбординг (никакого PIN-экрана);
//   2) PIN-экран появляется на устройстве, где онбординг уже пройден и есть clientId/PIN.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, uniquePhone, installBase, waitToken, unlockIfLocked, reportStep, expectScreen, dumpViews } = require('./api');

const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const PHONE = uniquePhone('+7999'); // TE-044: 10 млн вариантов, а не 10 тыс. — иначе коллизия с накопленными клиентами (~16%)

test('E2E: новое устройство → онбординг (без PIN-экрана) → журнал', async ({ page }) => {
  await installBase(page);
  await page.goto('/');

  await test.step('чистое устройство: PIN-экрана нет, показывается онбординг', async () => {
    // TE-044 (@DO 17:15): обёртка на КАЖДОМ шаге — иначе в логе падения нет имени шага,
    // и повторный `repeat` даёт ту же диагностику «время есть, места нет».
    await reportStep(page, 'шаг 1: чистое устройство → онбординг');
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
    // TE-044 (@DO 17:15): именно здесь падали все «флейки» (строка 35, `#onb-otp` hidden
    // 44 полла). Обёртка стоит ДО заполнения телефона, чтобы шаг назывался сам.
    await reportStep(page, 'шаг 2: телефон → OTP');
    await page.fill('#onb-phone-input', PHONE);
    await page.click('#btn-onb-phone');
    await expect(page.locator('#onb-otp')).toBeVisible({ timeout: 20000 });
    await page.fill('#onb-otp-input', TEST_OTP);
    await page.click('#btn-onb-otp');
    // 0.88 (E7 / FE-038): после OTP шага привязки партнёра НЕТ — онбординг спрашивает профиль
    // «Кто вы?» (id: onb-name, onb-gender-male, btn-onb-profile). Контейнер onb-link во фронте
    // остался, но всегда скрыт (app.js:282 и app.js:313 добавляют hidden), поэтому ожидать его
    // видимым — гарантированное падение. Спека писалась под 0.51, где шаг привязки был живым.
    await expectScreen(page, '#onb-name', 'OTP принят (verify вернул 200) → шаг «Кто вы?»', 20000);
    await expectScreen(page, '#btn-onb-profile', 'шаг «Кто вы?»: кнопка «Продолжить»', 10000);
    await expect(page.locator('#view-lock'), 'PIN-экрана на чистом устройстве нет').toBeHidden();

    await reportStep(page, 'онбординг: форма «Кто вы?» → Продолжить');
    const displayName = 'e2e-pinless-' + Date.now();
    await page.fill('#onb-name', displayName);
    await page.click('#onb-gender-male');
    await expect(page.locator('#onb-profile-error')).toBeHidden({ timeout: 5000 });
    await page.click('#btn-onb-profile');
    await dumpViews(page, 'pin-first-login: после формы «Кто вы?»');
    await expectScreen(page, '#view-journal', 'PIN-less онбординг → журнал открылся', 25000);
    await expect(page.locator('#view-onboarding')).toBeHidden();
    console.log('::notice::новое устройство: онбординг пройден без PIN-экрана, phone=' + PHONE);
  });

  await test.step('после reload устройство помнит вход (журнал или PIN — оба допустимы)', async () => {
    // TE-044 (@DO 17:15): третий шаг раньше вообще был без обёртки.
    await reportStep(page, 'шаг 3: после reload устройство помнит вход');
    // ВАЖНО: токен НЕ удаляем — устройство уже прошло онбординг (clientId + client-JWT).
    // Удаление токена уводило приложение в состояние, где afterAuth не мог решить, что показать,
    // и все три экрана оставались hidden — это ошибка теста, не продукта (@TL 02:45).
    await page.reload();
    const lock = page.locator('#view-lock');
    const onb = page.locator('#view-onboarding');
    const jr = page.locator('#view-journal');

    // УСТРОЙСТВО УЖЕ ПРОШЛО ОНБОРДИНГ — значит допустимы только ДВА исхода: журнал или замок.
    // Онбординг здесь = потеря сохранённого `clientId`/сессии, то есть дефект, а не «новое устройство».
    // Раньше шаг принимал любой из трёх экранов и печатал console.log — спека была зелёной даже при
    // регрессе входа (@AR 19:30: первичная аутентификация — телефон+OTP, PIN — только локальный замок,
    // повторный онбординг на пройденном устройстве не предусмотрен).
    await expect(lock.or(jr).first(), 'после reload на пройденном устройстве — журнал или замок').toBeVisible({ timeout: 20000 });
    await expect(onb, 'повторный онбординг на устройстве, где он уже пройден — потеря clientId/сессии').toBeHidden();

    if (await lock.isVisible()) {
      // TE-036 (@TL 03:30): unlockIfLocked ждёт ИСЧЕЗНОВЕНИЯ #view-lock; раньше waitToken
      // возвращался мгновенно (токен не удалялся), и expect(jr) стартовал при видимом замке.
      await unlockIfLocked(page, PIN);
      await expectScreen(page, '#view-journal', 'после PIN журнал открылся', 20000);
      console.log('::notice::после reload — PIN-экран (локальный замок), вход выполнен');
    } else {
      console.log('::notice::после reload — сразу журнал (сессия сохранена)');
    }
  });
});
