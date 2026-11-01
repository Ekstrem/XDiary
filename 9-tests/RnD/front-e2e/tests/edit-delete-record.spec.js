// E2E (настоящий) — TE-015: редактирование и удаление записи журнала.
// Запись создаём через живой API (быстрый setup), дальше всё делаем ЧЕРЕЗ UI:
// карточка записи → «Редактировать» → меняем длительность → «Сохранить»,
// затем удаляем через «Удалить» + подтверждение. Результат каждого шага — через API.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, unlockIfLocked } = require('./api');

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

async function apiList(page) {
  return page.evaluate(async ({ base }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/journal/records', {
      headers: t ? { Authorization: 'Bearer ' + t } : {},
    });
    return r.ok ? await r.json() : null;
  }, { base: API_BASE });
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

    // setup: запись через живой API (уникальное место = маркер)
    id = await apiCreate(page);
    const created = await apiRecord(page, id);
    expect(created.ok, 'запись создана через API: ' + apiLog.slice(-4).join(' | ')).toBe(true);
    expect(created.rec.durationMin, 'стартовая длительность').toBe(15);

    // перезагрузка → экран блокировки → разблокируем → журнал со свежими данными
    await page.reload();
    await unlockIfLocked(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

    const card = page.locator('#journal-list .record-card').filter({ hasText: MARK });
    await expect(card, 'карточка записи с местом ' + MARK + ' ;; API: ' + apiLog.slice(-6).join(' | ')).toHaveCount(1);
    await card.click();
    await expect(page.locator('#view-detail')).toBeVisible();

    // редактирование: длительность 15 -> 45
    await page.click('#btn-detail-edit');
    await expect(page.locator('#view-wizard')).toBeVisible();
    await page.locator('#wizard-progress button').nth(6).click();
    await page.fill('#w-dur-custom', '45');
    await page.click('#btn-wizard-next');
    await page.click('#btn-wizard-next');
    await page.click('#btn-wizard-save');
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 30000 });

    const edited = await apiRecord(page, id);
    expect(edited.ok).toBe(true);
    expect(edited.rec.durationMin, 'длительность после редактирования (API)').toBe(45);
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
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

    // Удаление в журнале — состояние агрегата (soft delete), отдельного поля в read-модели
    // нет, поэтому GET /records/{id} может отдавать 200. Проверяем наблюдаемое: запись
    // должна исчезнуть из СПИСКА и из UI-журнала.
    const list = await apiList(page);
    const tail = apiLog.slice(-12).join(' | ');
    expect(list, 'GET /api/v1/journal/records отвечает ;; API: ' + tail).toBeTruthy();
    const still = (list || []).find((x) => x.id === id);
    expect(!still, 'после удаления запись не должна быть в списке журнала (id ' + id + ') ;; API: ' + tail).toBe(true);
    await expect(page.locator('#journal-list .record-card').filter({ hasText: MARK })).toHaveCount(0);
    id = null;
  } finally {
    if (id) {
      const st = await apiDelete(page, id);
      console.log('cleanup record delete status:', st);
    }
  }
});
