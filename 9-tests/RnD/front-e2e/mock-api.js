// mock-api.js — локальный стенд-двойник API для прогона front-e2e без сети и без dev-стенда.
//
// Зачем: спеки ходят в `XDIARY_E2E_BASE_URL`; в песочнице внешняя сеть закрыта (TLS-обрыв),
// поэтому прогон был «не измерено». Этот сервер отвечает теми же путями, что и реальный API,
// и держит состояние в памяти — значит «создал → увидел» проверяется по-настоящему,
// но локально. Он НЕ заменяет замер на стенде: прогон против него — это проверка
// спек и фронта, а не контракта с живым бэкендом (см. `front-e2e.md`, §«что доказывает»).
//
// Использование:
//   node mock-api.js            # слушает 127.0.0.1:4180
//   PORT=4181 node mock-api.js
//
// Подключение к фронту: `XDIARY_E2E_BASE_URL=http://127.0.0.1:4180` — фикстура `installBase`
// кладёт этот адрес в `localStorage.xdiary.apiBase`.

const http = require('http');

const PORT = Number(process.env.PORT || 4180);
const OTP = process.env.XDIARY_E2E_OTP || '0000';

const db = {
  clients: new Map(), // id -> {id, phone, displayName, gender, verified}
  partners: [],
  records: [],
  seq: 0,
};

const uid = (prefix) => `${prefix}-${(++db.seq).toString().padStart(4, '0')}`;
const token = () => 'mock.jwt.' + Math.random().toString(36).slice(2, 10);

function send(res, code, body, extra = {}) {
  const payload = body === undefined ? '' : JSON.stringify(body);
  res.writeHead(code, {
    'Content-Type': 'application/json',
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Headers': '*',
    'Access-Control-Allow-Methods': 'GET,POST,PUT,PATCH,DELETE,OPTIONS',
    ...extra,
  });
  res.end(payload);
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (c) => (data += c));
    req.on('end', () => {
      try {
        resolve(data ? JSON.parse(data) : {});
      } catch {
        resolve({});
      }
    });
  });
}

function currentClient(req) {
  const auth = req.headers.authorization || '';
  const m = /^Bearer\s+(.+)$/.exec(auth);
  if (!m) return null;
  const id = m[1].split('.')[2] || 'cl-0001';
  return db.clients.get(id) || [...db.clients.values()][0] || null;
}

function partnerView(p) {
  // `partner-edit` проверяет по API: `name`, `notes`, `gender` — значит двойник обязан их отдавать
  return {
    id: p.id, name: p.name, clientId: p.clientId, createdAt: p.createdAt,
    notes: p.notes || null, gender: p.gender || null, displayName: p.name || null,
  };
}

function recordView(r) {
  // Поля — те, которые спеки реально читают из ответа API и проверяют числами/строками:
  // `edit-delete-record` → `durationMin`, `place`; каталог/визард → `positions`, `tags`.
  // Если поле не отдавать, локальный прогон падал бы на `expect(undefined).toBe(45)`.
  return {
    id: r.id, clientId: r.clientId, partnersNames: r.partnersNames || [],
    partnerNames: r.partnersNames || [], place: r.place || null,
    contraceptiveCount: r.contraceptiveCount || 0, contraception: r.contraception || null,
    durationMin: r.durationMin === undefined ? null : r.durationMin,
    startedAt: r.startedAt, endedAt: r.endedAt || null,
    places: r.places || [], positions: r.positions || [], tags: r.tags || [],
    costumes: r.costumes || [], toys: r.toys || [], fantasies: r.fantasies || [],
    orgasms: r.orgasms || [], notes: r.notes || null, kind: r.kind || 'record',
  };
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://127.0.0.1:${PORT}`);
  const path = url.pathname;
  const method = req.method.toUpperCase();

  if (method === 'OPTIONS') return send(res, 204, undefined);

  // ── контракт стенда, который проверяет `tests/global-setup.js` ────────────────────────────
  // Без этих ответов локальный прогон падал бы в globalSetup («стенд отвечает не так»),
  // то есть двойник был бы непригоден, хотя спеки к нему готовы.
  if (path === '/health') return send(res, 200, { status: 'ok' });
  if (method === 'GET' && path === '/api/v1/clients') {
    // стенд на GET списка клиентов отвечает 405 — globalSetup принимает 200/400/401/405
    return send(res, 405, { error: 'method not allowed' });
  }

  // --- клиенты и вход ---
  if (method === 'POST' && path === '/api/v1/clients') {
    const b = await readBody(req);
    const c = { id: uid('cl'), phone: b.phone || '', displayName: null, gender: null, verified: false };
    db.clients.set(c.id, c);
    return send(res, 200, { id: c.id, phone: c.phone });
  }
  let m = /^\/api\/v1\/clients\/([^/]+)\/verify$/.exec(path);
  if (method === 'POST' && m) {
    const c = db.clients.get(m[1]);
    if (c) c.verified = true;
    return send(res, 200, { accessToken: token(), refreshToken: token(), id: m[1] });
  }
  if (method === 'POST' && path === '/api/v1/clients/refresh') {
    const b = await readBody(req);
    const rt = String((b && b.refreshToken) || '');
    // стенд на битый refresh отвечает 4xx; двойник держит тот же контракт
    if (!/^mock\.jwt\./.test(rt)) {
      return send(res, 401, { error: 'invalid refresh token' }, { 'WWW-Authenticate': 'Bearer' });
    }
    return send(res, 200, { accessToken: token(), refreshToken: token() });
  }
  if (method === 'GET' && path === '/api/v1/clients/me') {
    const c = currentClient(req);
    if (!c) return send(res, 401, { error: 'unauthorized' }, { 'WWW-Authenticate': 'Bearer' });
    return send(res, 200, {
      id: c.id, phone: c.phone, displayName: c.displayName, gender: c.gender,
      verified: c.verified, linkedPartnerId: null,
    });
  }
  if (method === 'POST' && path === '/api/v1/clients/me/profile') {
    const c = currentClient(req) || { id: uid('cl') };
    const b = await readBody(req);
    c.displayName = b.displayName || c.displayName;
    c.gender = b.gender || c.gender;
    db.clients.set(c.id, c);
    return send(res, 200, { id: c.id, displayName: c.displayName, gender: c.gender });
  }

  // --- PIN (локальный замок; сервер только отвечает «нет PIN») ---
  if (path === '/api/v1/privacy/pin/has') return send(res, 200, { has: false });
  if (method === 'POST' && /^\/api\/v1\/privacy\/pin\/(set|verify|remove)$/.test(path)) {
    return send(res, 200, { ok: true, has: false });
  }
  if (method === 'POST' && path === '/api/v1/privacy/token') {
    return send(res, 200, { token: token(), kind: 'device' });
  }

  // --- партнёры ---
  if (path === '/api/v1/partners' || path === '/api/v1/partners/') {
    const c = currentClient(req);
    if (method === 'GET') {
      // без токена — 401: иначе globalSetup не подтвердит, что client-JWT обязателен
      if (!c) return send(res, 401, { error: 'unauthorized' }, { 'WWW-Authenticate': 'Bearer' });
      const mine = db.partners.filter((p) => p.clientId === c.id).map(partnerView);
      return send(res, 200, mine);
    }
    if (method === 'POST') {
      const b = await readBody(req);
      const p = {
        id: uid('pt'), name: b.name || 'TE', clientId: (c && c.id) || 'cl-0001',
        createdAt: new Date().toISOString(), notes: b.notes, gender: b.gender,
      };
      db.partners.push(p);
      return send(res, 200, partnerView(p));
    }
  }
  m = /^\/api\/v1\/partners\/([^/]+)\/remove$/.exec(path);
  if (method === 'POST' && m) {
    db.partners = db.partners.filter((p) => p.id !== m[1]);
    return send(res, 200, { ok: true });
  }

  // --- журнал ---
  if (path === '/api/v1/journal/records' || path === '/api/v1/journal/records/') {
    const c = currentClient(req);
    if (method === 'GET') {
      const mine = db.records.filter((r) => !c || r.clientId === c.id).map(recordView);
      return send(res, 200, mine);
    }
    if (method === 'POST') {
      const b = await readBody(req);
      const r = {
        id: uid('rc'), clientId: (c && c.id) || 'cl-0001',
        partnersNames: b.partnersNames || [], contraceptiveCount: b.contraceptiveCount || 0,
        startedAt: b.startedAt || new Date().toISOString(), places: b.places || [],
        place: b.place, durationMin: b.durationMin, positions: b.positions, tags: b.tags,
        costumes: b.costumes, toys: b.toys, fantasies: b.fantasies, orgasms: b.orgasms,
        notes: b.notes, kind: b.kind || 'record',
      };
      db.records.push(r);
      return send(res, 200, recordView(r));
    }
  }
  m = /^\/api\/v1\/journal\/records\/([^/]+)(\/remove)?$/.exec(path);
  if (m && (method === 'POST' || method === 'DELETE' || method === 'PUT' || method === 'PATCH')) {
    if (m[2] || method === 'DELETE') {
      db.records = db.records.filter((r) => r.id !== m[1]);
      return send(res, 200, { ok: true });
    }
    const b = await readBody(req);
    const r = db.records.find((x) => x.id === m[1]);
    if (r) Object.assign(r, b);
    return send(res, 200, r ? recordView(r) : { ok: true });
  }
  if (method === 'GET' && path === '/api/v1/journal/recent') {
    return send(res, 200, db.records.slice(-5).map(recordView));
  }
  if (method === 'GET' && path === '/api/v1/journal/places') {
    const places = [...new Set(db.records.flatMap((r) => r.places || []))];
    return send(res, 200, places.map((name, i) => ({ id: `pl-${i}`, name })));
  }

  // --- каталог ---
  if (path === '/api/v1/catalog' || path.startsWith('/api/v1/catalog/')) {
    if (method === 'GET') return send(res, 200, []);
    if (method === 'POST' && path === '/api/v1/catalog') {
      const b = await readBody(req);
      return send(res, 200, { id: uid('ct'), name: b.name || 'TE', kind: b.kind || 'costume' });
    }
    return send(res, 200, { ok: true });
  }

  // --- метрики/аналитика: пустые, но валидные ---
  if (path.startsWith('/api/v1/somatic-series/') || path.startsWith('/api/v1/daily-metrics/')
      || path.startsWith('/api/v1/analytics/')) {
    return send(res, 200, []);
  }

  return send(res, 404, { error: 'not found', path });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`mock-api on http://127.0.0.1:${PORT} (OTP=${OTP})`);
});
