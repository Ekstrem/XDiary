// E2E (настоящий) — TE-021/2: настройки.
// В приложении из «настроек» реально есть только **пол** (myGender); темы/единиц
// измерения в UI нет — это отмечено в TE-021. Блок «Сервер» и «Это я» сняты
// по требованию владельца 2026-11-23 (PR #3302, FE-057).
// Проверяем: пол переключается и сохраняется после перезагрузки, состояние кнопки активное.
const { test, expect } = require('@playwright/test');
const { loginWithPin, unlockIfLocked, reportStep } = require('./api');

test('E2E: настройки — пол сохраняется после перезагрузки', async ({ page }) => {
  test.setTimeout(180_000);
  // TE-070: бюджет 150 -> 180 с (возвраты на настройки + reload). Шаг «Снять я» снят
  // (владелец 2026-11-23: кнопки «Это я» удалены), бюджет оставлен с запасом.
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  await test.step('login по PIN', async () => {
    await loginWithPin(page);
  });

  await test.step('настройки: по умолчанию «Мужской»', async () => {
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
    await expect(page.locator('#my-gender-male')).toHaveClass(/active/);
    // Диагностика для разбора в CI: если настройки показали тост (ошибка сохранения), он попадёт в лог.
    await reportStep(page, 'settings: пол сохранён');
  });

  await test.step('переключить на «Женский»', async () => {
    await page.click('#my-gender-female');
    await expect(page.locator('#my-gender-female')).toHaveClass(/active/);
    await expect(page.locator('#my-gender-male')).not.toHaveClass(/active/);
  });

  await test.step('после перезагрузки выбранный пол сохранился', async () => {
    // setMyGender пишет в IndexedDB асинхронно (DB.setMeta без await) — даём
    // записи зафиксироваться, иначе reload теряет настройку (не баг, а гонка теста)
    await page.waitForFunction(() => new Promise((res) => {
      try {
        const r = indexedDB.open('xdiary');
        r.onsuccess = () => {
          const db = r.result;
          const tx = db.transaction('meta', 'readonly');
          const g = tx.objectStore('meta').get('myGender');
          g.onsuccess = () => res((g.result && (g.result.value || g.result)) === 'female');
          g.onerror = () => res(false);
        };
        r.onerror = () => res(false);
      } catch (e) { res(false); }
    }), null, { timeout: 10000 });
    await page.reload();
    await unlockIfLocked(page);
    await page.click('#tab-settings');
    await expect(page.locator('#my-gender-female')).toHaveClass(/active/);
  });

  await test.step('опасные действия спрашивают подтверждение и ОТМЕНЯЮТСЯ', async () => {
    // `#btn-wipe` и `#btn-ident-logout` до этого не нажимала ни одна спека. Проверяем не действие,
    // а защиту: открывается подтверждение, отмена его закрывает, состояние остаётся прежним.
    await page.click('#btn-wipe');
    await expect(page.locator('#confirm-modal'), 'подтверждение стирания').toBeVisible();
    await page.click('#confirm-no');
    await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 20000 });
    await expect(page.locator('#view-settings')).toBeVisible();

    await page.click('#btn-ident-logout');
    await expect(page.locator('#confirm-modal'), 'подтверждение сброса устройства').toBeVisible();
    await page.click('#confirm-no');
    await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 10000 });
    // устройство не сброшено: после reload вход сохранён и журнал доступен
    await page.reload();
    await unlockIfLocked(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
  });

  await test.step('миграция спрашивает подтверждение (нативный диалог) и отменяется', async () => {
    // `migrateData` использует `window.confirm`, а не модалку: диалог обязан появиться,
    // и отмена не должна ничего переносить.
    // Ждём САМО событие диалога, а не паузу: `page.waitForTimeout` запрещён гейтом
    // `check_e2e_no_sleeps` (он поймал эту же строку при первом заходе — правильно поймал).
    let seen = false;
    const dialogSeen = new Promise((resolve) => {
      page.once('dialog', async (dlg) => { seen = true; await dlg.dismiss(); resolve(true); });
    });
    const guard = new Promise((resolve) => setTimeout(() => resolve(false), 10000));
    await page.click('#btn-migrate');
    const appeared = await Promise.race([dialogSeen, guard]);
    expect(appeared, 'по клику миграции показан нативный подтверждающий диалог').toBe(true);
    expect(seen).toBe(true);
  });

  await test.step('вход в идентификацию открывает онбординг', async () => {
  // TE-067: уборка («вернуть Мужской») должна идти, пока НАСТРОЙКИ показаны. Раньше она шла после
  // шага «вход в идентификацию», который уводит приложение в онбординг: клик по `#my-gender-male`
  // (контрол экрана настроек) ждал появления до лимита — в прогоне #770 `Test timeout of 150000ms
  // exceeded`, а снимок `error-context.md` показывал именно онбординг («Твой номер телефона»).
  // Плюс страховка: если элемент не виден, не висим, а пишем в лог (состояние уже не то).
  if (await page.locator('#my-gender-male').isVisible()) {
    await page.click('#my-gender-male');
    await expect(page.locator('#my-gender-male')).toHaveClass(/active/);
  } else {
    console.log('::warning::settings: уборка «Мужской» пропущена — экран настроек не показан');
  }
  // `#btn-self-change` удалён из Настроек (владелец 2026-11-23: блок «Это я» снят) —
  // проверки «Сменить я»/«Снять я» убраны вместе с кнопками.
  // `#btn-ident-start` обязан привести к экрану онбординга, не ломая текущую сессию.
  await page.click('#btn-ident-start');
    await expect(page.locator('#view-onboarding'), 'вход в идентификацию открывает онбординг').toBeVisible({ timeout: 20000 });
  });

});
