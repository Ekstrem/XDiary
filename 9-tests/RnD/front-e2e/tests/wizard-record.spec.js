// E2E (настоящий) — TE-014: создание записи журнала через визард.
// Живое приложение + живой шлюз, вход по PIN. Уникальный маркер — в поле «место»,
// проверка результата — ЧЕРЕЗ API (не по UI), уборка — в finally.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin } = require('./api');

const MARK = 'TE-WIZ-' + Date.now();

async function findRecord(page, place) {
  return page.evaluate(async ({ base, place }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = t ? { Authorization: 'Bearer ' + t } : {};
    const r = await fetch(base + '/api/v1/journal/records', { headers });
    if (!r.ok) return { ok: false, status: r.status };
    const list = await r.json();
    const it = (list || []).find((x) => x.place === place);
    return { ok: true, found: !!it, id: it && it.id };
  }, { base: API_BASE, place });
}

async function deleteRecord(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = t ? { Authorization: 'Bearer ' + t } : {};
    const r = await fetch(base + '/api/v1/journal/records/' + id + '/delete', { method: 'POST', headers });
    return { ok: r.ok, status: r.status };
  }, { base: API_BASE, id });
}

test('E2E: запись через визард — создаётся и видна через API', async ({ page }) => {
  let id = null;
  try {
    await loginWithPin(page);
    await expect(page.locator('#view-journal')).toBeVisible();

    await page.click('#btn-new');
    await expect(page.locator('#view-wizard')).toBeVisible();

    await page.click('#btn-wizard-next');  // 0 дата -> 1 партнёр
    await page.click('#btn-wizard-next');  // 1 -> 2 место
    await page.fill('#w-place', MARK);     // уникальный маркер
    await page.click('#btn-wizard-next');  // 2 -> 3 позы
    await page.click('#btn-wizard-next');  // 3 -> 4 отметки
    await page.click('#btn-wizard-next');  // 4 -> 5 контрацепция
    await page.click('#btn-wizard-next');  // 5 -> 6 длительность
    await page.fill('#w-dur-custom', '30');
    await page.click('#btn-wizard-next');  // 6 -> 7 оргазмы
    await page.click('#btn-wizard-next');  // 7 -> 8 итог

    await page.click('#btn-wizard-save');
    await expect(page.locator('#view-journal')).toBeVisible();

    const found = await findRecord(page, MARK);
    expect(found.ok, 'GET /api/v1/journal/records должен отвечать 200').toBe(true);
    expect(found.found, 'запись должна появиться в журнале (проверка через API)').toBe(true);
    id = found.id;
  } finally {
    if (id) {
      const r = await deleteRecord(page, id);
      console.log('cleanup record:', JSON.stringify(r));
    }
  }
});
