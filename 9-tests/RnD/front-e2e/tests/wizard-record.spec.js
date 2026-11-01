// E2E (настоящий) — TE-014: создание записи журнала через визард.
// Живое приложение + живой шлюз (dev, VPS-2), вход по PIN.
// `saveWizard` делает ~10 последовательных запросов, поэтому после «Сохранить» ЖДЁМ
// переключения вида (а не снимаем снимок сразу). Диагностика при падении: тосты + API-лог.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }
const NOW = new Date();
const WHEN = '2026-11-02T' + pad(NOW.getUTCHours()) + ':' + pad(NOW.getUTCMinutes());

async function viewState(page) {
  return page.evaluate(() => {
    const map = {};
    document.querySelectorAll('.view').forEach((v) => { map[v.id] = !v.classList.contains('hidden'); });
    return map;
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
  const apiLog = [];
  page.on('response', (r) => {
    const u = r.url();
    if (u.indexOf('/api/v1/') !== -1) {
      apiLog.push(r.status() + ' ' + r.request().method() + ' ' + u.replace(/^https?:\/\/[^/]+/, ''));
    }
  });

  let id = null;
  try {
    await loginWithPin(page);
    const vis0 = await viewState(page);
    expect(vis0['view-journal'], 'виды после логина: ' + JSON.stringify(vis0)).toBe(true);

    await page.click('#btn-new');
    // 5с по умолчанию не хватало: на нагруженном dev открытие визарда иногда дольше
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 20000 });

    await page.evaluate(() => {
      window.__toasts = [];
      const t = document.getElementById('toast');
      if (t) {
        new MutationObserver(() => { if (t.textContent) window.__toasts.push(t.textContent); })
          .observe(t, { childList: true, characterData: true, subtree: true });
      }
    });

    await page.fill('#w-startedAt', WHEN);
    await page.click('#btn-wizard-next');  // 0 -> 1
    await page.click('#btn-wizard-next');  // 1 -> 2
    await page.click('#btn-wizard-next');  // 2 -> 3
    await page.click('#btn-wizard-next');  // 3 -> 4
    await page.click('#btn-wizard-next');  // 4 -> 5
    await page.click('#btn-wizard-next');  // 5 -> 6
    await page.fill('#w-dur-custom', '30');
    await page.click('#btn-wizard-next');  // 6 -> 7
    await page.click('#btn-wizard-next');  // 7 -> 8

    await page.click('#btn-wizard-save');

    // ждём переключения на журнал (цепочка из ~10 запросов)
    try {
      await expect(page.locator('#view-journal')).toBeVisible({ timeout: 30000 });
    } catch (e) {
      const toasts = await page.evaluate(() => window.__toasts || []);
      const vis = await viewState(page);
      throw new Error('ТОСТЫ: ' + JSON.stringify(toasts) +
        ' ;; API: ' + apiLog.slice(-12).join(' | ') +
        ' ;; ВИДЫ: ' + JSON.stringify(vis));
    }

    const found = await findRecord(page, WHEN);
    expect(found.ok, 'GET /api/v1/journal/records → ' + apiLog.slice(-5).join(' | ')).toBe(true);
    expect(found.found, 'запись ' + WHEN + ' не найдена; API: ' + apiLog.slice(-8).join(' | ')).toBe(true);
    id = found.id;
  } finally {
    if (id) {
      const r = await deleteRecord(page, id);
      console.log('cleanup record:', JSON.stringify(r));
    }
  }
});
