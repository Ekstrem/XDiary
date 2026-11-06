// E2E (настоящий) — TE-020: archive/restore элемента каталога (кнопка #ce-archive — только для игрушек).
// Элемент создаётся через живой API, архив/восстановление — через UI, проверка — через API.
const { test, expect } = require('@playwright/test');
const { UI_WAIT, API_BASE, loginWithPin, removeByName } = require('./api');

const NAME = 'TE-TOY-' + Date.now();

async function createItem(page) {
  return page.evaluate(async ({ base, name }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/catalog', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
      body: JSON.stringify({ kind: 'toy', name }),
    });
    if (!r.ok) throw new Error('POST /catalog -> ' + r.status);
    return (await r.json()).id;
  }, { base: API_BASE, name: NAME });
}

async function getItem(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/catalog/' + id, { headers: { Authorization: 'Bearer ' + t } });
    return r.ok ? await r.json() : null;
  }, { base: API_BASE, id });
}

async function openItem(page) {
  await page.click('#tab-catalog');
  await expect(page.locator('#view-catalog')).toBeVisible();
  await page.click('#catalog-tabs button[data-kind="toy"]');
  const card = page.locator('#catalog-list .partner-row').filter({ hasText: NAME });
  await expect(card, 'карточка ' + NAME).toHaveCount(1);
  await card.click();
  await expect(page.locator('#view-catalog-edit')).toBeVisible();
  // openCatalogEdit показывает форму ДО асинхронного DB.getCatalogItem().then,
  // который заполняет поля и сбрасывает catalogEditArchived. Без ожидания наш
  // клик по #ce-archive затирается (как в partner-edit). Ждём загрузки названия.
  await expect(page.locator('#ce-name')).toHaveValue(NAME);
}

test('E2E: каталог — archive и restore через UI, проверка API', async ({ page }) => {
  test.setTimeout(110_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  let id = null;
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('setup: создать игрушку через API', async () => {
      id = await createItem(page);
      expect(id, 'id созданного элемента').toBeTruthy();
    });

    await test.step('архивировать через UI', async () => {
      await openItem(page);
      await expect(page.locator('#ce-archive')).toBeVisible();
      await expect(page.locator('#ce-archive')).toHaveText(/В архив/);
      // Фото позиции каталога (`#ce-photo`) — скрытый file-вход, не покрывался.
      await page.setInputFiles('#ce-photo', {
        name: 'item.png', mimeType: 'image/png',
        buffer: Buffer.from(
          'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mP8z8AAAwAB/wFBn0VoAAAAAElFTkSuQmCC',
          'base64'),
      });
      await page.click('#ce-archive');
      await expect(page.locator('#ce-archive')).toHaveText(/Вернуть/);
      await page.click('#btn-catalog-save', { timeout: 20000 });
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: UI_WAIT });

      const archived = await getItem(page, id);
      // `toMatchObject` падает и на `null`, и на неверном id   строже, чем «truthy».
      expect(archived, 'GET /catalog/{id} вернул позицию с тем же id').toMatchObject({ id });
      expect(archived.archived, 'archived = true после «В архив» (API)').toBe(true);
    });

    await test.step('восстановить через UI', async () => {
      await openItem(page);
      await expect(page.locator('#ce-archive')).toHaveText(/Вернуть/);
      await page.click('#ce-archive');
      await expect(page.locator('#ce-archive')).toHaveText(/В архив/);
      await page.click('#btn-catalog-save', { timeout: 20000 });
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: UI_WAIT });

      const restored = await getItem(page, id);
      expect(restored.archived, 'archived = false после restore (API)').toBe(false);
    });

    await test.step('удалить позицию каталога через UI (#btn-catalog-delete)', async () => {
      // До этого спека не трогала `#btn-catalog-delete` вовсе: удаление позиции каталога
      // не проверялось никак (карта `check_e2e_ui_coverage`).
      // Навигация назад из правки позиции (`#btn-catalog-edit-back`) — тоже не была покрыта.
      await openItem(page);
      await page.click('#btn-catalog-edit-back');
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: 10000 });

      await openItem(page);
      await page.click('#btn-catalog-delete', { timeout: 15000 });
      // TE-069: диагностика вместо 110-секундного зависания. В прогонах #766/#774 шаг
      // «удалить позицию» висел до лимита, а снимок показывал форму БЕЗ модалки подтверждения.
      // Не угадываем: после клика ждём ЛИБО модалку, ЛИБО тост, и пишем в лог, что появилось.
      const afterDelete = await Promise.race([
        page.waitForSelector('#confirm-modal:not(.hidden)', { timeout: 15000 })
          .then(() => 'modal').catch(() => null),
        page.waitForFunction(() => {
          const t = document.getElementById('toast');
          return !!t && !t.classList.contains('hidden') && t.textContent.trim().length > 0;
        }, null, { timeout: 15000 }).then(() => 'toast').catch(() => null),
      ]).catch(() => null);
      const toastText = await page.evaluate(() => {
        const t = document.getElementById('toast');
        return t ? t.textContent.trim() : '';
      }).catch(() => '');
      const editId = await page.evaluate(() => document.getElementById('ce-name')?.value || '');
      console.log('::notice::catalog-delete: после клика=' + String(afterDelete) +
        ' | тост=' + JSON.stringify(String(toastText).slice(0, 120)) +
        ' | поле имени=' + JSON.stringify(String(editId).slice(0, 60)));
      await expect(page.locator('#confirm-modal')).toBeVisible();
      // негативный контроль: «Нет» обязано оставить позицию на месте
      await page.click('#confirm-no');
      await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 10000 });
      const kept = await getItem(page, id);
      expect(kept, 'после отмены позиция каталога на месте (API)').toBeTruthy();

      await openItem(page);
      await page.click('#btn-catalog-delete', { timeout: 20000 });
      await expect(page.locator('#confirm-modal')).toBeVisible();
      await page.click('#confirm-yes');
      await expect(page.locator('#view-catalog')).toBeVisible({ timeout: UI_WAIT });
      const gone = await getItem(page, id);
      expect(gone, 'после удаления GET /catalog/{id} не отдаёт позицию').toBeFalsy();
      // И в самом списке каталога позиции больше нет (требование @TL 01:06).
      await page.click('#catalog-tabs button[data-kind="toy"]');
      await expect(
        page.locator('#catalog-list .partner-row').filter({ hasText: NAME }),
        'после удаления позиции нет в списке каталога'
      ).toHaveCount(0);
      id = null;
    });
  } finally {
    const r = await removeByName(page, 'catalog', NAME);
    console.log('cleanup catalog:', JSON.stringify(r));
  }
});
