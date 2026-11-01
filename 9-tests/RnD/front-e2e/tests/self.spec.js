// E2E (настоящий) — TE-025, API-часть (BE-028): `self` — «сделать партнёра собой».
// Онбординг-UI (FE-018) ещё не в коде, поэтому проверяем контракт, на который он обопрётся:
// POST /api/v1/partners/{id}/self { self: bool } → Self в read-model партнёра.
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin, removeByName } = require('./api');

const NAME = 'TE-SELF-' + Date.now();

async function createPartner(page) {
  return page.evaluate(async ({ base, name }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/partners', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
      body: JSON.stringify({ name }),
    });
    if (!r.ok) throw new Error('POST /partners -> ' + r.status);
    return (await r.json()).id;
  }, { base: API_BASE, name: NAME });
}

async function getPartner(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/partners/' + id, { headers: { Authorization: 'Bearer ' + t } });
    return r.ok ? await r.json() : null;
  }, { base: API_BASE, id });
}

async function setSelf(page, id, self) {
  return page.evaluate(async ({ base, id, self }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + '/api/v1/partners/' + id + '/self', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
      body: JSON.stringify({ self }),
    });
    return r.status;
  }, { base: API_BASE, id, self });
}

test('E2E: self — сделать/снять партнёра «собой» (BE-028)', async ({ page }) => {
  let id = null;
  try {
    await test.step('login по PIN', async () => {
      await loginWithPin(page);
    });

    await test.step('setup: партнёр через API', async () => {
      id = await createPartner(page);
      expect(id, 'id партнёра').toBeTruthy();
    });

    await test.step('по умолчанию self=false', async () => {
      const p = await getPartner(page, id);
      expect(p, 'GET /partners/{id}').toBeTruthy();
      expect(p.self, 'новый партнёр не «я»').toBe(false);
    });

    await test.step('сделать «собой» → self=true в read-model', async () => {
      const st = await setSelf(page, id, true);
      expect(st, 'POST /partners/{id}/self {self:true}').toBe(200);
      const p = await getPartner(page, id);
      expect(p.self, 'self=true в read-model (сервер, не локально)').toBe(true);
    });

    await test.step('снять «я» → self=false', async () => {
      const st = await setSelf(page, id, false);
      expect(st, 'POST /partners/{id}/self {self:false}').toBe(200);
      const p = await getPartner(page, id);
      expect(p.self, 'self=false в read-model').toBe(false);
    });
  } finally {
    const r = await removeByName(page, 'partner', NAME);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
