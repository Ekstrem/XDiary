// Хелпер настоящих E2E: живой API + реальный вход по PIN (как пользователь).
// Стенд берётся из XDIARY_E2E_BASE_URL (в CI — VPS-2), PIN — из XDIARY_E2E_PIN.
// ВАЖНО: дефолт — ТЕСТОВЫЙ стенд (VPS-2). Прод (VPS-1) — реальный дневник владельца;
// случайный локальный прогон без env не должен в него писать.
const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps2.taile0b6ee.ts.net').replace(/\/+$/, '');
const PIN = process.env.XDIARY_E2E_PIN || '1234';

// Приложение по умолчанию ходит на DEFAULT_BASE (VPS-1) — переключаем его на тестовый
// стенд, иначе UI-прогон будет наполовину против прода.
async function installBase(page) {
  await page.addInitScript((base) => {
    try { localStorage.setItem('xdiary.apiBase', base); } catch (e) {}
  }, API_BASE);
}

async function waitToken(page) {
  try {
    await page.waitForFunction(() => !!localStorage.getItem('xdiary.accessToken'), null, { timeout: 30000 });
  } catch (e) {
    const failed = [];
    page.on('requestfailed', (r) => {
      const f = r.failure();
      failed.push(r.url() + ' :: ' + ((f && f.errorText) || 'failed'));
    });
    throw new Error('JWT не получен. Сетевые ошибки: ' + JSON.stringify(failed.slice(0, 5)));
  }
}

// Реальный вход: ставим локальный PIN (как в Настройках), перезагружаемся,
// на экране блокировки вводим PIN → приложение берёт JWT (ensureToken → login).
async function loginWithPin(page, pin) {
  const p = pin || PIN;
  await installBase(page);
  await page.goto('/');
  await page.evaluate(({ base, v }) => {
    try { localStorage.setItem('xdiary.apiBase', base); } catch (e) {}
    return window.Pin.setPin(v);
  }, { base: API_BASE, v: p });
  await page.goto('/');
  await page.waitForSelector('#view-lock:not(.hidden)', { timeout: 20000 });
  await page.fill('#pin-input', p);
  await page.click('#pin-submit');
  await waitToken(page);
}

// После reload локальный PIN уже стоит → приложение показывает экран блокировки.
// Разблокируем, чтобы попасть в журнал.
async function unlockIfLocked(page, pin) {
  const p = pin || PIN;
  if (!(await page.locator('#view-lock').isVisible())) return;
  await page.fill('#pin-input', p);
  await page.click('#pin-submit');
  await waitToken(page);
}

async function removeByName(page, kind, name) {
  try {
    return await page.evaluate(async ({ base, kind, name }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const headers = t ? { Authorization: 'Bearer ' + t } : {};
      const listPath = kind === 'partner' ? '/api/v1/partners' : '/api/v1/catalog';
      const r = await fetch(base + listPath, { headers });
      if (!r.ok) return { ok: false, listStatus: r.status };
      const list = await r.json();
      const it = (list || []).find((x) => x.name === name);
      if (!it) return { ok: true, removed: false };
      const rem = kind === 'partner'
        ? base + '/api/v1/partners/' + it.id + '/remove'
        : base + '/api/v1/catalog/' + it.id + '/remove';
      const d = await fetch(rem, { method: 'POST', headers });
      return { ok: d.ok, removed: d.ok, status: d.status };
    }, { base: API_BASE, kind, name });
  } catch (e) {
    return { ok: false, error: String(e && e.message) };
  }
}

module.exports = { API_BASE, PIN, installBase, waitToken, loginWithPin, unlockIfLocked, removeByName };
