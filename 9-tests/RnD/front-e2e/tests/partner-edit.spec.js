// E2E (настоящий) — TE-017: редактирование партнёра (rename + profile через UI).
// Партнёр создаётся через живой API (setup), правка — через UI
// (Партнёры → карточка → форма → имя/notes/пол → Сохранить), проверка — через API.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, removeByName } = require('./api');

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
      // ВАЖНО: openPartnerEdit показывает форму ДО асинхронного DB.getPartner().then,
      // который перезаписывает поля. Без ожидания fill затирается — в тело уходит
      // {gender:"female", notes:""} (нашёл по логу POST /profile).
      await expect(page.locator('#pe-name')).toHaveValue(OLD);
    });

    await test.step('UI: изменить имя, notes и пол → Сохранить', async () => {
      await page.fill('#pe-name', NEW);
      await page.fill('#pe-notes', NOTES);
      await page.click('#pe-gender-male');
      await page.click('#btn-partner-save');
      await expect(page.locator('#view-partners')).toBeVisible({ timeout: 30000 });
    });

    await test.step('проверка через API: rename + profile', async () => {
      const p = await getPartner(page, id);
      const bodies = 'тело POST /profile: ' + JSON.stringify(profileBodies);
      expect(p, 'GET /partners/{id} ;; ' + bodies).toBeTruthy();
      expect(p.name, 'name после rename (API)').toBe(NEW);
      expect(p.notes, 'notes после profile (API) ;; ' + bodies).toBe(NOTES);
      expect(p.gender, 'gender после profile (API)').toBe('male');
      await expect(page.locator('#partners-list .partner-row').filter({ hasText: NEW })).toHaveCount(1);
    });
  } finally {
    const r = await removeByName(page, 'partner', NEW);
    const r2 = await removeByName(page, 'partner', OLD);
    console.log('cleanup partner:', JSON.stringify(r), JSON.stringify(r2));
  }
});
