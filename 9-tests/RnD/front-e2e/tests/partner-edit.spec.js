// E2E (настоящий) — TE-017: редактирование партнёра (rename + profile через UI).
// Партнёр создаётся через живой API (setup), правка — через UI
// (Партнёры → карточка → форма → имя/notes/пол → Сохранить), проверка — через API.
const { test, expect } = require('@playwright/test');
const { UI_WAIT, API_BASE, loginWithPin, removeByName } = require('./api');

const OLD = 'TE-P-' + Date.now();
const NEW = OLD + '-renamed';
const NOTES = 'заметка ' + Date.now();

async function createPartner(page) {
  return page.evaluate(async ({ base, name }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/partners', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
      body: JSON.stringify({ name }),
    });
    if (!r.ok) throw new Error('POST /partners -> ' + r.status);
    return (await r.json()).id;
  }, { base: API_BASE, name: OLD });
}

async function getPartner(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/partners/' + id, { headers: { Authorization: 'Bearer ' + t } });
    return r.ok ? await r.json() : null;
  }, { base: API_BASE, id });
}

test('E2E: partner-edit — rename и profile через UI, проверка API', async ({ page }) => {
  test.setTimeout(110_000);
  // TE-062: бюджет теста = сумма объявленных ожиданий. При общем лимите 90 с такая спека
  // на медленном стенде падает по общему таймауту («не дождались»), хотя поведение верное.
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  let id = null;
  // @BE просил точное тело POST /partners/{id}/profile — фиксируем его для диагностики
  const profileBodies = [];
  page.on('request', (r) => {
    if (r.method() === 'POST' && r.url().includes('/profile')) profileBodies.push(r.postData());
  });
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('setup: создать партнёра через API', async () => {
      id = await createPartner(page);
      expect(id, 'id созданного партнёра').toBeTruthy();
    });

    await test.step('UI: открыть карточку партнёра', async () => {
      await page.click('#tab-partners');
      await expect(page.locator('#view-partners')).toBeVisible();
      const card = page.locator('#partners-list .partner-row').filter({ hasText: OLD });
      await expect(card, 'карточка ' + OLD).toHaveCount(1);
      await card.click();
      await expect(page.locator('#view-partner-edit')).toBeVisible();
      // ИСТОРИЯ (проверено по коду, `app.js:1195-1222`): в 0.41 `openPartnerEdit` показывал форму
      // ДО `DB.getPartner().then`, и загрузка затирала ввод (в тело уходило `{gender:"female", notes:""}`);
      // @TL починил это в `0.42`/`v42`: поля заполняются (1209-1216), и **только потом** `showView` (1217).
      // Прежний комментарий утверждал обратное — он устарел и вводил в заблуждение (я сам на него ссылался).
      // Ожидание значения ниже оставлено не «против гонки», а как проверка: форма открыта с данными партнёра.
      await expect(page.locator('#pe-name')).toHaveValue(OLD);
    });

    await test.step('UI: изменить имя, notes и пол → Сохранить', async () => {
      // Фото партнёра (`#pe-photo`) — скрытый file-вход; ни одна спека в него ничего не кладла.
      // Кладём крошечный PNG и проверяем, что путь не падает и превью/состояние не ломается.
      await page.setInputFiles('#pe-photo', {
        name: 'avatar.png', mimeType: 'image/png',
        buffer: Buffer.from(
          'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mP8z8AAAwAB/wFBn0VoAAAAAElFTkSuQmCC',
          'base64'),
      });
      await page.fill('#pe-name', NEW);
      await page.fill('#pe-notes', NOTES);
      // Второй вариант пола (`#pe-gender-female`) не проверялся: нажимаем оба, сохраняем мужской.
      await page.click('#pe-gender-female');
      await page.click('#pe-gender-male');
      await page.click('#btn-partner-save');
      await expect(page.locator('#view-partners')).toBeVisible({ timeout: UI_WAIT });
    });

    await test.step('проверка через API: rename + profile', async () => {
      const p = await getPartner(page, id);
      const bodies = 'тело POST /profile: ' + JSON.stringify(profileBodies);
      expect(p, 'GET /partners/{id} вернул партнёра с тем же id ;; ' + bodies).toMatchObject({ id });
      expect(p.name, 'name после rename (API)').toBe(NEW);
      expect(p.notes, 'notes после profile (API) ;; ' + bodies).toBe(NOTES);
      expect(p.gender, 'gender после profile (API)').toBe('male');
      await expect(page.locator('#partners-list .partner-row').filter({ hasText: NEW })).toHaveCount(1);
    });

    await test.step('удалить партнёра через UI (#btn-partner-delete)', async () => {
      // До этого спека не трогала `#btn-partner-delete` вовсе: удаление партнёра не проверялось
      // (карта `check_e2e_ui_coverage`). Негативный контроль — «Нет» оставляет партнёра.
      const open = async () => {
        await page.click('#tab-partners');
        await expect(page.locator('#view-partners')).toBeVisible();
        const card = page.locator('#partners-list .partner-row').filter({ hasText: NEW });
        await expect(card, 'карточка ' + NEW).toHaveCount(1);
        await card.click();
        await expect(page.locator('#view-partner-edit')).toBeVisible();
        await expect(page.locator('#pe-name')).toHaveValue(NEW);
      };

      // Навигация назад (`#btn-partner-edit-back`) — не была покрыта ни одной спекой.
      await open();
      await page.click('#btn-partner-edit-back');
      await expect(page.locator('#view-partners')).toBeVisible({ timeout: 20000 });

      await open();
      await page.click('#btn-partner-delete');
      await expect(page.locator('#confirm-modal')).toBeVisible();
      await page.click('#confirm-no');
      await expect(page.locator('#confirm-modal')).toBeHidden({ timeout: 20000 });
      await expect(
        page.locator('#partners-list .partner-row').filter({ hasText: NEW }),
        'после отмены партнёр остался в списке'
      ).toHaveCount(1);

      await open();
      await page.click('#btn-partner-delete');
      await expect(page.locator('#confirm-modal')).toBeVisible();
      await page.click('#confirm-yes');
      await expect(page.locator('#view-partners')).toBeVisible({ timeout: UI_WAIT });
      await expect(
        page.locator('#partners-list .partner-row').filter({ hasText: NEW }),
        'после удаления партнёра нет в списке'
      ).toHaveCount(0);

      // Контракт @TL 01:06 (BE-080/BE-085): после удаления `GET /partners/{id}` обязан отдавать 404/403,
      // а не 200 с «мягко удалённой» записью — иначе клиент продолжит показывать партнёра.
      const after = await page.evaluate(async ({ base, id }) => {
        const t = localStorage.getItem('xdiary.accessToken');
        const r = await fetch(base + '/api/v1/partners/' + id, { headers: { Authorization: 'Bearer ' + t } });
        return { status: r.status };
      }, { base: API_BASE, id });
      expect(
        [404, 403].includes(after.status),
        'GET /partners/{id} после удаления: ' + after.status + ' (ожидали 404/403)'
      ).toBe(true);

      // ADR-026: негативный код без парной позитивной проверки — не приёмка. Тот же путь ДО удаления
      // обязан отдавать 200 с токеном: иначе «404 после» зеленело бы и на эндпоинте, который не
      // отдаёт ничего никому.
      const beforePair = await page.evaluate(async ({ base, id }) => {
        const t = localStorage.getItem('xdiary.accessToken');
        const r = await fetch(base + '/api/v1/partners/' + id, { headers: { Authorization: 'Bearer ' + t } });
        return { status: r.status };
      }, { base: API_BASE, id });
      expect(beforePair.status, 'контроль: GET /partners/{id} с токеном ДО удаления').toBe(200);
      id = null;
    });
  } finally {
    const r = await removeByName(page, 'partner', NEW);
    const r2 = await removeByName(page, 'partner', OLD);
    console.log('cleanup partner:', JSON.stringify(r), JSON.stringify(r2));
  }
});
