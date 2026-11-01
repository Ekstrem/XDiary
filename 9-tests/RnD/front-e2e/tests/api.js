// Хелпер настоящих E2E: работаем с ЖИВЫМ API (без заглушек).
// Уборка: созданные тестом сущности удаляем через API, чтобы не мусорить в базе.
const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps1.taile0b6ee.ts.net').replace(/\/+$/, '');

async function waitToken(page) {
  // без PIN приложение берёт device-токен (FE-006/BE-019); без него все запросы 401
  const failed = [];
  page.on('requestfailed', (r) => {
    const f = r.failure();
    failed.push(r.url() + ' :: ' + ((f && f.errorText) || 'failed'));
  });
  try {
    await page.waitForFunction(() => !!localStorage.getItem('xdiary.accessToken'), null, { timeout: 30000 });
  } catch (e) {
    throw new Error('device-токен не получен. Сетевые ошибки: ' + JSON.stringify(failed.slice(0, 5)));
  }
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

module.exports = { API_BASE, waitToken, removeByName };
