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
    await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 15000 });
    await expect(page.locator('#view-settings')).toBeVisible();

    await page.click('#btn-ident-logout');
    await expect(page.locator('#confirm-modal'), 'подтверждение сброса устройства').toBeVisible();
    await page.click('#confirm-no');
    await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 15000 });
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

  await test.step('смена «я» и вход в идентификацию открывают онбординг', async () => {
    // `#btn-self-change` и `#btn-ident-start` не проверялись: оба обязаны привести к экрану
    // онбординга (профиль «я» / идентификация), не ломая текущую сессию.
    await page.click('#btn-self-change');
    await expect(page.locator('#view-onboarding'), 'смена «я» открывает онбординг').toBeVisible({ timeout: 20000 });
    await page.reload();
    await unlockIfLocked(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();

    await page.click('#btn-ident-start');
    await expect(page.locator('#view-onboarding'), 'вход в идентификацию открывает онбординг').toBeVisible({ timeout: 20000 });
  });

  await test.step('вернуть «Мужской» (уборка)', async () => {
    await page.click('#my-gender-male');
    await expect(page.locator('#my-gender-male')).toHaveClass(/active/);
  });
});
