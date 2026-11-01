// E2E (настоящий) — TE-015: редактирование и удаление записи журнала.
// Запись создаём через живой API (быстрый setup), дальше всё делаем ЧЕРЕЗ UI:
// карточка → карточка записи → «Редактировать» → меняем длительность → «Сохранить»,
// затем удаляем через «Удалить» + подтверждение. Результат каждого шага проверяем API.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }
const NOW = new Date();
const WHEN = '2026-11-02T' + pad(NOW.getUTCHours()) + ':' + pad(NOW.getUTCMinutes());
const MARK = 'TE-EDIT-' + Date.now();

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

async function apiRecord(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/journal/records/' + id, {
      headers: t ? { Authorization: 'Bearer ' + t } : {},
    });
    if (!r.ok) return { ok: false, status: r.status };
    return { ok: true, rec: await r.json() };
  }, { base: API_BASE, id });
}

async function apiDelete(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/journal/records/' + id + '/delete', {
      method: 'POST', headers: t ? { Authorization: 'Bearer ' + t } : {},
    });
    return r.status;
  }, { base: API_BASE, id });
}

test('E2E: запись журнала — редактирование и удаление через UI', async ({ page }) => {
  let id = null;
  try {
    await loginWithPin(page);

    // setup: запись через живой API (уникальное место = маркер)
    id = await apiCreate(page);
    const created = await apiRecord(page, id);
    expect(created.ok, 'запись создана через API').toBe(true);
    expect(created.rec.durationMin, 'стартовая длительность').toBe(15);

    // журнал → карточка нашей записи → детали → редактировать
    await page.reload();
    await expect(page.locator('#view-journal')).toBeVisible();
    const card = page.locator('#journal-list .record-card').filter({ hasText: MARK });
    await expect(card, 'карточка записи с местом ' + MARK).toHaveCount(1);
    await card.click();
    await expect(page.locator('#view-detail')).toBeVisible();

    await page.click('#btn-detail-edit');
    await expect(page.locator('#view-wizard')).toBeVisible();

    // переходим на шаг длительности (7-я точка прогресса) и меняем 15 -> 45
    await page.locator('#wizard-progress button').nth(6).click();
    await page.fill('#w-dur-custom', '45');
    await page.click('#btn-wizard-next');  // 6 -> 7
    await page.click('#btn-wizard-next');  // 7 -> 8
    await page.click('#btn-wizard-save');
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 30000 });

    const edited = await apiRecord(page, id);
    expect(edited.ok).toBe(true);
    expect(edited.rec.durationMin, 'длительность после редактирования (через API)').toBe(45);
    expect(edited.rec.place, 'место не изменилось').toBe(MARK);

    // удаление через UI: та же запись → редактировать → «Удалить» → «Да»
    const card2 = page.locator('#journal-list .record-card').filter({ hasText: MARK });
    await expect(card2).toHaveCount(1);
    await card2.click();
    await expect(page.locator('#view-detail')).toBeVisible();
    await page.click('#btn-detail-edit');
    await expect(page.locator('#view-wizard')).toBeVisible();
    await page.locator('#wizard-progress button').nth(8).click();
    await page.click('#btn-wizard-delete');
    await expect(page.locator('#confirm-modal')).toBeVisible();
    await page.click('#confirm-yes');
    await expect(page.locator('#view-journal')).toBeVisible();

    const gone = await apiRecord(page, id);
    expect(gone.ok, 'после удаления GET записи должен быть 404').toBe(false);
    id = null;
  } finally {
    if (id) {
      const st = await apiDelete(page, id);
      console.log('cleanup record delete status:', st);
    }
  }
});
