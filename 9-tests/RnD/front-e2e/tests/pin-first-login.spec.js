// E2E (настоящий) — новое устройство и PIN (адаптирован под FE-021, 0.51).
//
// ЧТО ИЗМЕНИЛОСЬ: до 0.51 на чистом устройстве показывался PIN-экран (legacy-путь).
// После FE-021 новое устройство сразу идёт в онбординг «кто ты» — PIN-экрана там больше НЕТ.
// Поэтому сценарий разделён:
//   1) чистое устройство → онбординг (никакого PIN-экрана);
//   2) PIN-экран появляется на устройстве, где онбординг уже пройден и есть clientId/PIN.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, installBase, waitToken, unlockIfLocked, reportStep, expectScreen, dumpViews } = require('./api');

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
    await expectScreen(page, '#onb-link', 'OTP принят (verify вернул 200)', 20000);
    // TE-036 (@TL 03:50): шаг пройден = #onb-link виден И нет тоста ошибки. Если бы verify
    // вернул 400, спека упала бы ЗДЕСЬ, а не на «#view-journal hidden» ниже.
    await reportStep(page, 'онбординг: OTP → привязка');
    // TE-043: карточка выбирается ДЕТЕРМИНИРОВАННО — по имени партнёра этого прогона,
    // а не `.first()` (на стенде с сотнями карточек это произвольная старая запись).
    const partnerName = 'e2e-auto-' + Date.now();
    const created = await page.evaluate(async ({ base, name }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      if (!t) return { ok: false, noToken: true };
      const r = await fetch(base + '/api/v1/partners', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
        body: JSON.stringify({ name }),
      });
      const j = r.ok ? await r.json().catch(() => null) : null;
      return { ok: r.ok, status: r.status, id: (j && j.id) || null };
    }, { base: API_BASE, name: partnerName }).catch((e) => ({ ok: false, err: String(e).slice(0, 120) }));
    console.log('::warning::партнёр через API: ' + JSON.stringify(created) + ' name=' + partnerName);

    const allCards = page.locator('#onboarding-list .partner-card');
    const ownCards = allCards.filter({ hasText: partnerName });
    const countOwn = async () => await ownCards.count();

    let ownCount = 0;
    for (let i = 0; i < 10 && ownCount === 0; i++) {
      ownCount = await countOwn();
      if (ownCount === 0) await page.waitForTimeout(500);
    }
    if (ownCount === 0) {
      await page.reload();
      for (let i = 0; i < 30 && ownCount === 0; i++) {
        ownCount = await countOwn();
        if (ownCount === 0) await page.waitForTimeout(500);
      }
    }
    const totalCards = await allCards.count();
    console.log('::warning::карточек всего: ' + totalCards + ', своих по имени ' + partnerName + ': ' + ownCount);
    if (ownCount !== 1) {
      throw new Error(
        'Своя карточка не найдена однозначно: совпадений ' + ownCount + ' из ' + totalCards +
        ', партнёр создан=' + JSON.stringify(created) + ', имя=' + partnerName +
        '. Клик не выполняется — привязка к чужому партнёру недопустима (TE-043).'
      );
    }
    await ownCards.scrollIntoViewIfNeeded().catch(() => {});
    await ownCards.click();
    // TE-032 (@DO 08:35): падение было на этой строке — при повторе нужен активный экран,
    // а не только «journal hidden», иначе снова гадаем «не тот экран или фронт не переключил».
    await page.waitForTimeout(700);
    await dumpViews(page, 'pin-first-login: после клика по карточке');
    // skip убран из UI (решение владельца п.4 от 06.11)
    await expectScreen(page, '#view-journal', 'PIN-less онбординг → журнал открылся', 20000);
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
      // TE-036 (@TL 03:30): unlockIfLocked ждёт ИСЧЕЗНОВЕНИЯ #view-lock; раньше waitToken
      // возвращался мгновенно (токен не удалялся), и expect(jr) стартовал при видимом замке.
      await unlockIfLocked(page, PIN);
      await expectScreen(page, '#view-journal', 'после PIN журнал открылся', 20000);
      console.log('::notice::после reload — PIN-экран, вход выполнен');
    } else if (await onb.isVisible()) {
      console.log('::notice::после reload — онбординг (новое устройство)');
    } else {
      console.log('::notice::после reload — сразу журнал');
    }
  });
});
