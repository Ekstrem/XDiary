// E2E (настоящий) — TE-027: привязка партнёра к клиенту.
// После BE-028 контракт: POST /partners/{id}/link-client, POST /partners/{id}/unlink-client,
// в read-model Partner.ClientId (Guid?). BE-085 (@TL 2026-11-17 21:11): ClientId берётся
// из `sub` JWT, а НЕ из тела запроса — поэтому ожидание в спеке — id СЕССИИ, не нового клиента. Прежний self.spec.js звал УДАЛЁННЫЙ POST /partners/{id}/self —
// он заменён этим файлом (находка @TL 2026-11-04 13:00).
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, removeByName } = require('./api');

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NAME = 'TE-LINK-' + Date.now();

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
      expect(partnerId, 'id партнёра — UUID').toMatch(UUID_RE);
    });

    await test.step('link-client: clientId появляется в read-model', async () => {
      // BE-085: сервер берёт ClientId из `sub` (JWT), тело игнорируется — см.
      // `PartnersController.LinkClient` (`6-msa/BC-Partners`): `ClientId не из тела, а из sub`.
      // Поэтому ожидаем id СЕССИИ, а лишний `POST /clients` убран: он ничего не доказывал
      // и жёг OTP-лимит (BE-117/BE-121), из-за чего setup падал 429-м.
      const sessionClientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId') || '');
      expect(sessionClientId, 'clientId сессии — UUID').toMatch(UUID_RE);

      const link = await call(page, '/api/v1/partners/' + partnerId + '/link-client', {
        method: 'POST', body: JSON.stringify({}),
      });
      expect(link.status, 'POST /partners/{id}/link-client').toBeLessThan(300);

      const p = await call(page, '/api/v1/partners/' + partnerId);
      expect(p.status, 'GET /partners/{id}').toBe(200);
      expect(p.data && p.data.clientId, 'partner.clientId после привязки = клиент из sub')
        .toBe(sessionClientId);
      console.log('::notice::TE-027 link-client ок: partner=' + partnerId + ' client=' + sessionClientId);
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
