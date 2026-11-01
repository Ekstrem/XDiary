// Общий стаб API для UI-тестов XDiary.
// Контракт BE-018: Command API — POST-only. Стаб повторяет реальные маршруты:
//   Partners: POST /partners, POST /partners/{id}/name|profile|remove
//   Catalog:  POST /catalog {kind,name}, POST /catalog/{id}/name|details|archive|restore|remove
//   PUT/DELETE к Command API -> 405.
// Журнал (GET) отдаёт пустой список. Всё пишется в window.__calls для диагностики.
async function installApiMock(page) {
  await page.addInitScript(() => {
    const partners = [];
    const catalog = [];
    let pseq = 0;
    let cseq = 0;
    window.__partners = partners;
    window.__catalog = catalog;
    window.__calls = [];

    const json = (body, status = 200) => new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' },
    });
    const notFound = (what) => json({ error: what + ' not found.' }, 404);
    const notAllowed = () => json({ error: 'Method Not Allowed' }, 405);

    window.fetch = function (input, init) {
      const opts = init || {};
      const url = String(typeof input === 'string' ? input : (input && input.url) || '');
      const method = String(opts.method || 'GET').toUpperCase();
      const path = url.replace(/^[a-z]+:\/\/[^/]+/i, '');
      window.__calls.push(method + ' ' + path);

      let body = {};
      try { body = JSON.parse(opts.body || '{}'); } catch (e) { body = {}; }

      // --- privacy / journal / legacy proxy ---
      if (path === '/api/v1/privacy/pin/has') return Promise.resolve(json(false));
      if (path === '/api/v1/privacy/token') return Promise.resolve(json({ accessToken: 't', refreshToken: 'r' }));
      if (path === '/api/v1/journal/records' && method === 'GET') return Promise.resolve(json([]));
      if (path.indexOf('/local/') === 0 || path.indexOf('/activities') === 0) return Promise.resolve(json([]));

      // --- partners ---
      if (path === '/api/v1/partners' && method === 'GET') return Promise.resolve(json(partners));
      if (path === '/api/v1/partners' && method === 'POST') {
        const created = { id: 'p-' + (++pseq), name: body.name || '', photo: '', gender: 'female', notes: '' };
        partners.push(created);
        return Promise.resolve(json(created));
      }
      const pm = path.match(/^\/api\/v1\/partners\/([^/]+)\/(name|profile|remove)$/);
      if (pm && method === 'POST') {
        const p = partners.find((x) => x.id === pm[1]);
        if (!p) return Promise.resolve(notFound('Partner'));
        if (pm[2] === 'name') p.name = body.name || p.name;
        else if (pm[2] === 'profile') {
          if (body.photo !== undefined) p.photo = body.photo;
          if (body.gender !== undefined) p.gender = body.gender;
          if (body.notes !== undefined) p.notes = body.notes;
        } else partners.splice(partners.indexOf(p), 1);
        return Promise.resolve(json(p));
      }

      // --- catalog ---
      if (path === '/api/v1/catalog' && method === 'GET') return Promise.resolve(json(catalog));
      if (path === '/api/v1/catalog' && method === 'POST') {
        const created = {
          id: 'c-' + (++cseq), kind: body.kind || '', name: body.name || '',
          photo: '', notes: '', brand: '', archived: false, fantasyPartner: '',
        };
        catalog.push(created);
        return Promise.resolve(json(created));
      }
      const cm = path.match(/^\/api\/v1\/catalog\/([^/]+)\/(name|details|archive|restore|remove)$/);
      if (cm && method === 'POST') {
        const it = catalog.find((x) => x.id === cm[1]);
        if (!it) return Promise.resolve(notFound('Catalog item'));
        if (cm[2] === 'name') it.name = body.name || it.name;
        else if (cm[2] === 'details') {
          if (body.photo !== undefined) it.photo = body.photo;
          if (body.notes !== undefined) it.notes = body.notes;
          if (body.brand !== undefined) it.brand = body.brand;
          if (body.fantasyPartner !== undefined) it.fantasyPartner = body.fantasyPartner;
        } else if (cm[2] === 'archive') it.archived = true;
        else if (cm[2] === 'restore') it.archived = false;
        else catalog.splice(catalog.indexOf(it), 1);
        return Promise.resolve(json(it));
      }

      // BE-018: Command API больше не принимает PUT/DELETE.
      if (method === 'PUT' || method === 'DELETE') return Promise.resolve(notAllowed());

      return Promise.resolve(json({}));
    };
  });
}

module.exports = { installApiMock };
