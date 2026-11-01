// E2E (настоящий) — TE-014: создание записи журнала через визард.
// Живое приложение + живой шлюз (dev, VPS-2), вход по PIN.
// Уникальный маркер — дата-время записи (шаг «Когда?» рендерится синхронно; шаг «Где?»
// на dev асинхронный и в этом прогоне не отрисовался — это отдельная находка).
// Проверка результата — ЧЕРЕЗ API, уборка — в finally.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }
const NOW = new Date();
const WHEN = '2026-11-02T' + pad(NOW.getUTCHours()) + ':' + pad(NOW.getUTCMinutes());

async function viewState(page) {
  return page.evaluate(() => {
    const map = {};
    document.querySelectorAll('.view').forEach((v) => { map[v.id] = !v.classList.contains('hidden'); });
    return { views: map, step: window.wizardStep === undefined ? 'n/a' : window.wizardStep };
  });
}

async function findRecord(page, startedAt) {
  return page.evaluate(async ({ base, startedAt }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = t ? { Authorization: 'Bearer ' + t } : {};
    const r = await fetch(base + '/api/v1/journal/records', { headers });
    if (!r.ok) return { ok: false, status: r.status };
    const list = await r.json();
    const it = (list || []).find((x) => String(x.startedAt || '').indexOf(startedAt) === 0);
    return { ok: true, found: !!it, id: it && it.id };
  }, { base: API_BASE, startedAt });
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
    const vis = await viewState(page);
    expect(vis.views['view-journal'], 'виды после логина: ' + JSON.stringify(vis)).toBe(true);

    await page.click('#btn-new');
    await expect(page.locator('#view-wizard')).toBeVisible();

    // шаг 0 «Когда?» — синхронный, ставим уникальный маркер
    await page.fill('#w-startedAt', WHEN);
    await page.click('#btn-wizard-next');  // 0 -> 1 партнёр
    await page.click('#btn-wizard-next');  // 1 -> 2 место
    await page.click('#btn-wizard-next');  // 2 -> 3 позы
    await page.click('#btn-wizard-next');  // 3 -> 4 отметки
    await page.click('#btn-wizard-next');  // 4 -> 5 контрацепция
    await page.click('#btn-wizard-next');  // 5 -> 6 длительность
    await page.fill('#w-dur-custom', '30');
    await page.click('#btn-wizard-next');  // 6 -> 7 оргазмы
    await page.click('#btn-wizard-next');  // 7 -> 8 итог

    await page.click('#btn-wizard-save');
    await expect(page.locator('#view-journal')).toBeVisible();

    const found = await findRecord(page, WHEN);
    expect(found.ok, 'GET /api/v1/journal/records должен отвечать 200').toBe(true);
    expect(found.found, 'запись с датой ' + WHEN + ' должна быть в журнале (проверка через API)').toBe(true);
    id = found.id;
  } finally {
    if (id) {
      const r = await deleteRecord(page, id);
      console.log('cleanup record:', JSON.stringify(r));
    }
  }
});
