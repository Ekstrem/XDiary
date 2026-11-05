// mock-smoke.js — самопроверка локального двойника API (без браузера).
//
// Зачем: `mock-api.js` нужен, чтобы гонять `front-e2e` без dev-стенда. Если сам двойник отвечает
// не теми формами, прогон спек «упадёт на стенде» и это будет ложный красный по продукту.
// Скрипт поднимает `mock-api.js` на свободном порту, дёргает те же пути, что и спеки
// (`/clients`, `/clients/{id}/verify`, `/clients/me/profile`, `/partners`, `/journal/records`,
// `/journal/recent`, `/privacy/pin/*`, `/somatic-series/*`, `/daily-metrics/*`, `/analytics/*`),
// и проверяет **формы ответов**, а не только коды. `rc=0` — двойник пригоден для прогона.
//
// Использование: node mock-smoke.js

const { spawn } = require('child_process');
const path = require('path');

const PORT = Number(process.env.SMOKE_PORT || 4181);
const BASE = `http://127.0.0.1:${PORT}`;

let ok = 0;
let total = 0;
let skipped = 0;

function check(name, cond, detail) {
  total += 1;
  if (cond) {
    ok += 1;
    console.log(`  [OK]   ${name}`);
  } else {
    console.log(`  [FAIL] ${name}${detail ? ' :: ' + JSON.stringify(detail) : ''}`);
  }
}

async function j(method, url, body, headers = {}) {
  const res = await fetch(BASE + url, {
    method,
    headers: { 'Content-Type': 'application/json', ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }
  return { status: res.status, data };
}

async function waitReady(timeoutMs = 15000) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    try {
      const r = await j('GET', '/api/v1/privacy/pin/has');
      if (r.status === 200) return true;
    } catch {
      /* сервер ещё поднимается */
    }
    await new Promise((r) => setTimeout(r, 150));
  }
  return false;
}

(async () => {
  const srv = spawn(process.execPath, [path.join(__dirname, 'mock-api.js')], {
    env: { ...process.env, PORT: String(PORT) },
    stdio: 'ignore',
  });
  try {
    const ready = await waitReady();
    check('двойник поднялся и отвечает', ready);
    if (!ready) throw new Error('not ready');

    const has = await j('GET', '/api/v1/privacy/pin/has');
    check('pin/has → 200 {has:false}', has.status === 200 && has.data && has.data.has === false, has);

    const cli = await j('POST', '/api/v1/clients', { phone: '+79990009999' });
    const clientId = cli.data && cli.data.id;
    check('POST /clients → id', cli.status === 200 && typeof clientId === 'string' && clientId.length > 0, cli);

    const ver = await j('POST', `/api/v1/clients/${clientId}/verify`, { code: '0000' });
    check('verify → accessToken + refreshToken',
      ver.status === 200 && ver.data && ver.data.accessToken && ver.data.refreshToken, ver);

    const auth = { Authorization: 'Bearer ' + (ver.data && ver.data.accessToken) };

    const prof = await j('POST', '/api/v1/clients/me/profile', { displayName: 'SMOKE', gender: 'female' }, auth);
    check('me/profile → displayName', prof.status === 200 && prof.data && prof.data.displayName === 'SMOKE', prof);

    const me = await j('GET', '/api/v1/clients/me', undefined, auth);
    check('me → id и linkedPartnerId=null',
      me.status === 200 && me.data && me.data.id && me.data.linkedPartnerId === null, me);

    const pCreate = await j('POST', '/api/v1/partners', { name: 'SMOKE-PARTNER' }, auth);
    check('POST /partners → id', pCreate.status === 200 && pCreate.data && pCreate.data.id, pCreate);

    const pList = await j('GET', '/api/v1/partners', undefined, auth);
    const list = Array.isArray(pList.data) ? pList.data : [pList.data];
    check('GET /partners содержит созданного',
      list.some((p) => p && p.name === 'SMOKE-PARTNER'), pList);

    const rec = await j('POST', '/api/v1/journal/records',
      { partnersNames: ['SMOKE-PARTNER'], contraceptiveCount: 1, places: ['SMOKE-PLACE'] }, auth);
    check('POST /journal/records → id', rec.status === 200 && rec.data && rec.data.id, rec);

    const recent = await j('GET', '/api/v1/journal/recent', undefined, auth);
    const rl = Array.isArray(recent.data) ? recent.data : [recent.data];
    check('GET /journal/recent содержит созданную запись',
      rl.some((r) => r && r.id === (rec.data && rec.data.id)), recent);

    const places = await j('GET', '/api/v1/journal/places', undefined, auth);
    check('GET /journal/places → 200', places.status === 200, places);

    for (const p of ['/api/v1/somatic-series/slice?fromT=2026-01-01&toT=2026-12-31',
                     '/api/v1/somatic-series/points',
                     '/api/v1/daily-metrics/series?kind=sleep',
                     '/api/v1/analytics/summary']) {
      const r = await j('GET', p, undefined, auth);
      check(`GET ${p.split('?')[0]} → 200`, r.status === 200, r);
    }

    const pinSet = await j('POST', '/api/v1/privacy/pin/set', { pin: '1234' }, auth);
    check('privacy/pin/set → 200', pinSet.status === 200, pinSet);
    const pinRm = await j('POST', '/api/v1/privacy/pin/remove', { currentPin: '1234' }, auth);
    check('privacy/pin/remove → 200', pinRm.status === 200, pinRm);

    // ── контракт стенда, который требует tests/global-setup.js ─────────────────────────────
    const health = await fetch(BASE + '/health');
    check('/health → 200', health.status === 200);

    const clientsGet = await j('GET', '/api/v1/clients');
    check('GET /clients → 405 (как стенд)', clientsGet.status === 405, clientsGet);

    // Браузеру нужен preflight: без него локальный прогон упадёт CORS-ошибкой, которую не видно
    // в контрактных проверках. Раньше двойник этого не проверял.
    const pre = await fetch(BASE + '/api/v1/partners', {
      method: 'OPTIONS',
      headers: { Origin: 'http://127.0.0.1:4173', 'Access-Control-Request-Method': 'POST' },
    });
    check('OPTIONS /partners   204 (preflight браузера)', pre.status === 204, { status: pre.status });
    check('preflight отдаёт Access-Control-Allow-Origin',
      pre.headers.get('access-control-allow-origin') === '*',
      { cors: pre.headers.get('access-control-allow-origin') });
    check('обычный ответ тоже с CORS',
      (await fetch(BASE + '/health')).headers.get('access-control-allow-origin') === '*');

    const unknown = await j('GET', '/api/v1/definitely-not-here');
    check('неизвестный путь   404 JSON (а не 200)',
      unknown.status === 404 && unknown.data && Boolean(unknown.data.error), unknown);

    const partnersNoAuth = await fetch(BASE + '/api/v1/partners');
    check('/partners без токена → 401', partnersNoAuth.status === 401);

    const refreshBad = await j('POST', '/api/v1/clients/refresh', { refreshToken: 'not-a-jwt' });
    check('refresh с битым токеном → 4xx', [400, 401, 403, 422].includes(refreshBad.status), refreshBad);

    const meNoAuth = await fetch(BASE + '/api/v1/clients/me');
    check('/clients/me без токена → 401 + WWW-Authenticate',
      meNoAuth.status === 401 && Boolean(meNoAuth.headers.get('www-authenticate')), { status: meNoAuth.status });

    // ── и главная проверка пригодности: настоящий globalSetup набора проходит на двойнике ───
    // globalSetup требует `@playwright/test`. В чистом worktree `node_modules` нет — тогда это
    // «не измерено», а не «сломано»: печатаем SKIP и не считаем проверку.
    const hasPw = require('fs').existsSync(path.join(__dirname, 'node_modules', '@playwright', 'test'));
    if (!hasPw) {
      skipped += 1;
      console.log('  [SKIP] global-setup.js: нет node_modules/@playwright/test — не измерено');
    } else {
    // `stdio: 'inherit'`, а не `pipe`: в песочнице агента захват вывода дочернего процесса даёт
    // `spawn EPERM`. Здесь достаточно КОДА возврата — вывод globalSetup и так идёт в наш stdout.
    const gs = spawn(process.execPath, ['-e',
      "require('./tests/global-setup.js')().then(()=>process.exit(0))"
      + ".catch((e)=>{console.log('GLOBALSETUP_FAIL: '+e.message); process.exit(1);})"],
      { cwd: __dirname, env: { ...process.env, XDIARY_E2E_BASE_URL: BASE, XDIARY_E2E_TEST_OTP: '000000' },
        stdio: 'inherit' });
    const gsCode = await new Promise((resolve) => gs.on('close', resolve));
    check('global-setup.js на двойнике проходит (rc=0)', gsCode === 0, { code: gsCode });
    }

    const skipNote = skipped ? ` | пропущено ${skipped} (нет node_modules)` : '';
    console.log(`mock-smoke: ${ok}/${total} проверок двойника пройдено${skipNote}`);
    process.exitCode = ok === total ? 0 : 1;
  } catch (e) {
    console.log(`  [FAIL] smoke прерван: ${e.message}`);
    process.exitCode = 1;
  } finally {
    srv.kill();
  }
})();
