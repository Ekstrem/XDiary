// E2E (настоящий) — TE-027: привязка партнёра к клиенту.
// После BE-028 контракт: POST /partners/{id}/link-client { clientId }, POST /partners/{id}/unlink-client,
// в read-model Partner.ClientId (Guid?). Прежний self.spec.js звал УДАЛЁННЫЙ POST /partners/{id}/self —
// он заменён этим файлом (находка @TL 2026-11-04 13:00).
const { test, expect } = require('@playwright/test');
const { API_BASE, uniquePhone, loginWithPin, removeByName } = require('./api');

const NAME = 'TE-LINK-' + Date.now();
const PHONE = uniquePhone('+7999'); // TE-044: 10 млн вариантов, а не 10 тыс.

function call(page, path, opts) {
  return page.evaluate(async ({ base, path, opts }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = { 'Content-Type': 'application/json' };
    if (t) headers.Authorization = 'Bearer ' + t;
    const r = await fetch(base + path, { method: (opts && opts.method) || 'GET', headers, body: opts && opts.body });
    let data = null;
    try { data = await r.json(); } catch (e) {}
    return { status: r.status, data };
  }, { base: API_BASE, path, opts });
}

test('E2E: link-client/unlink-client — партнёр привязан к клиенту (TE-027)', async ({ page }) => {
  let partnerId = null;
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('создан партнёр и клиент через API', async () => {
      const p = await call(page, '/api/v1/partners', { method: 'POST', body: JSON.stringify({ name: NAME }) });
      expect(p.status, 'POST /partners').toBeLessThan(300);
      partnerId = p.data && p.data.id;
      expect(partnerId, 'id партнёра').toBeTruthy();
    });

    await test.step('link-client: clientId появляется в read-model', async () => {
      const c = await call(page, '/api/v1/clients', { method: 'POST', body: JSON.stringify({ phone: PHONE }) });
      expect([200, 201], 'POST /clients -> ' + c.status + ' ' + JSON.stringify(c.data)).toContain(c.status);
      const clientId = c.data && c.data.id;
      expect(clientId, 'id клиента').toBeTruthy();

      const link = await call(page, '/api/v1/partners/' + partnerId + '/link-client', {
        method: 'POST', body: JSON.stringify({ clientId }),
      });
      expect(link.status, 'POST /partners/{id}/link-client').toBeLessThan(300);

      const p = await call(page, '/api/v1/partners/' + partnerId);
      expect(p.status, 'GET /partners/{id}').toBe(200);
      expect(p.data && p.data.clientId, 'partner.clientId после привязки').toBe(clientId);
      console.log('::notice::TE-027 link-client ок: partner=' + partnerId + ' client=' + clientId);
    });

    await test.step('unlink-client: clientId сбрасывается', async () => {
      const un = await call(page, '/api/v1/partners/' + partnerId + '/unlink-client', { method: 'POST' });
      expect(un.status, 'POST /partners/{id}/unlink-client').toBeLessThan(300);

      const p = await call(page, '/api/v1/partners/' + partnerId);
      expect(p.status, 'GET /partners/{id}').toBe(200);
      expect(p.data && (p.data.clientId || null), 'partner.clientId после отвязки').toBe(null);
      console.log('::notice::TE-027 unlink-client ок: partner=' + partnerId);
    });
  } finally {
    const r = await removeByName(page, 'partner', NAME);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
