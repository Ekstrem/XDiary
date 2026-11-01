// E2E (настоящий) — TE-021/1: соматика детали записи (FE-013).
// Проверяем: (1) деталь запрашивает соматику из API (`/somatic-series/slice`), а не из
// локального кэша (`getSomaticRange` после FE-013 удалён); (2) при ошибке API видно
// сообщение «Не удалось загрузить соматику» в `#somatic-stats`.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, unlockIfLocked } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }
const NOW = new Date();
const WHEN = '2026-11-02T' + pad(NOW.getUTCHours()) + ':' + pad(NOW.getUTCMinutes());
const MARK = 'TE-SOM-' + Date.now();

async function apiCreate(page) {
  return page.evaluate(async ({ base, place, when }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const h = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t };
    const post = async (p, b) => {
      const r = await fetch(base + p, { method: 'POST', headers: h, body: b ? JSON.stringify(b) : undefined });
      if (!r.ok) throw new Error(p + ' -> ' + r.status);
      return r.json().catch(() => null);
    };
    const start = await post('/api/v1/journal/records');
    const id = start && start.id;
    if (!id) throw new Error('start() не вернул id');
    await post('/api/v1/journal/records/' + id + '/when', { startedAt: when });
    await post('/api/v1/journal/records/' + id + '/place', { place });
    await post('/api/v1/journal/records/' + id + '/duration', { durationMin: 15 });
    await post('/api/v1/journal/records/' + id + '/complete');
    return id;
  }, { base: API_BASE, place: MARK, when: WHEN });
}

async function openDetail(page) {
  // Журнал рендерится при загрузке/переходах, но не подхватывает запись, созданную
  // через API после логина. В edit-delete по этой же причине есть reload — здесь тоже.
  await page.reload();
  await unlockIfLocked(page);
  await page.click('#tab-journal');
  await expect(page.locator('#view-journal')).toBeVisible();
  const card = page.locator('#journal-list .record-card').filter({ hasText: MARK });
  // карточек с одним маркером может быть несколько (дубль записи из повторного прогона
  // или из бэкенда) — для детали достаточно первой
  await expect(card.first(), 'карточка ' + MARK).toBeVisible({ timeout: 20000 });
  await card.first().click();
  await expect(page.locator('#view-detail')).toBeVisible();
  await expect(page.locator('#somatic-canvas')).toBeVisible();
}

test('E2E: деталь записи — соматика из API, при ошибке видно сообщение (FE-013)', async ({ page }) => {
  let id = null;
  const sliceReqs = [];
  page.on('request', (r) => { if (r.url().includes('/somatic-series/slice')) sliceReqs.push(r.url()); });
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('setup: запись через API', async () => {
      id = await apiCreate(page);
      expect(id, 'id записи').toBeTruthy();
    });

    await test.step('деталь запрашивает соматику из API (не локально)', async () => {
      await openDetail(page);
      expect(sliceReqs.length, 'GET /somatic-series/slice должен быть вызван').toBeGreaterThan(0);
      await expect(page.locator('#somatic-stats')).not.toHaveText(/Не удалось загрузить соматику/);
    });

    await test.step('ошибка API → видимое сообщение', async () => {
      await page.route('**/somatic-series/slice*', (route) => route.abort());
      await page.reload();
      await unlockIfLocked(page);
      await openDetail(page);
      await expect(page.locator('#somatic-stats')).toHaveText(/Не удалось загрузить соматику/, { timeout: 15000 });
    });
  } finally {
    if (id) {
      const st = await page.evaluate(async ({ base, id }) => {
        const t = localStorage.getItem('xdiary.accessToken') || '';
        const r = await fetch(base + '/api/v1/journal/records/' + id + '/delete', { method: 'POST', headers: { Authorization: 'Bearer ' + t } });
        return r.status;
      }, { base: API_BASE, id }).catch(() => null);
      console.log('cleanup record delete status:', st);
    }
  }
});
