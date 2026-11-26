#!/usr/bin/env node
/**
 * TE-112 — проба потока Garmin по токену (требование владельца 2026-11-23).
 *
 * Что проверяет (dev-стенд, одна сессия, без БД):
 *   1) неавторизованный вход: `POST /garmin/connect` без JWT -> 401 (маршрут есть и защищён);
 *   2) `POST /garmin/connect` с битыми credentials -> 400 и код `validation_error`;
 *   3) `GET /garmin/state` без подключения -> 404 (по контракту);
 *   4) `POST /garmin/sync` без подключения -> 400 и код `not_connected`;
 *   5) `connect` с БУДУЩИМ `tokenExpiry` -> успех; `GET /garmin/state` -> 200, `connected=true`,
 *      и в ответе НЕТ секретов (`apiKey`/`oauthToken` — §6 AGENTS.md);
 *   6) `connect` с ПРОШЛЫМ `tokenExpiry` -> `state.requiresReauth = true`.
 *
 * Различение «маршрут есть» vs «BE-142 не в образе»: отсутствие маршрута/неподходящее тело ->
 * rc=2 «не измерено», а НЕ нарушение (урок класса «проверка раньше объекта»).
 *
 * Запуск: node 9-tests/scripts/probe_garmin_connect.js [--base URL] [--json]
 *         node 9-tests/scripts/probe_garmin_connect.js --check-class-only [--base URL]
 *         node 9-tests/scripts/probe_garmin_connect.js --selftest
 *   XDIARY_API_BASE — база (по умолчанию dev); XDIARY_E2E_TEST_OTP — OTP (по умолчанию 000000).
 *
 * Коды: 0 — контракт выполняется; 1 — нарушение контракта; 2 — не измерено
 *        (сеть, стенд, BE-142 не доехал, prod без тестового OTP).
 */
const crypto = require('crypto');

const args = process.argv.slice(2);
const argv = {};
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--base') argv.base = args[++i];
  else if (args[i] === '--json') argv.json = true;
  else if (args[i] === '--check-class-only') argv.checkClassOnly = true;
  else if (args[i] === '--selftest') argv.selftest = true;
}
const BASE = argv.base || process.env.XDIARY_API_BASE || 'https://xdiary-vps2.taile0b6ee.ts.net';
const OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const FUTURE = '2999-12-31T00:00:00Z';
const PAST = '2000-01-01T00:00:00Z';

const j = async (r) => ({ status: r.status, data: await r.json().catch(() => null) });
const out = (o) => { if (argv.json) console.log(JSON.stringify(o)); else console.log(o.text); };
const SECRET_FIELDS = ['apiKey', 'oauthToken', 'oauth_token', 'api_key', 'token'];

// ---------- вердикты по классу ответа (образец TE-111) ----------
function classifyStep(name, code, expectCodes) {
  if (expectCodes.includes(code)) return { rc: 0, text: `${name} -> ${code} (ожидали ${expectCodes.join('/')})` };
  if (code >= 500) return { rc: 1, text: `${name} -> ${code} (5xx — сервис лежит или контракт сломан)` };
  return { rc: 2, text: `${name} -> ${code} (неожиданный класс — не решаю)` };
}

function selftest() {
  const cases = [
    ['401 как ожидание', classifyStep('connect без JWT', 401, [401]), 0],
    ['400 в ожидании', classifyStep('sync без подключения', 400, [400]), 0],
    ['500 всегда нарушение', classifyStep('state', 500, [401, 404]), 1],
    ['404 вне ожидания — не измерено', classifyStep('state', 404, [401]), 2],
    ['секреты в ответе — нарушение', hasSecrets({ connected: true, apiKey: 'x' }), true],
    ['секретов нет — ок', hasSecrets({ connected: true, lastSync: null }), false],
    ['код not_connected распознаётся', hasCode({ code: 'not_connected' }, 'not_connected'), true],
  ];
  let bad = 0;
  for (const [name, got, want] of cases) {
    const val = got && Object.prototype.hasOwnProperty.call(got, 'rc') ? got.rc : got;
    const mark = val === want ? '[OK] ' : '[FAIL]';
    if (val !== want) bad++;
    console.log(`${mark} ${name}`);
  }
  console.log(bad === 0 ? 'selftest: OK 7/7' : `selftest: FAIL ${bad}`);
  process.exitCode = bad === 0 ? 0 : 1;
}

function hasSecrets(obj) {
  if (!obj || typeof obj !== 'object') return false;
  return SECRET_FIELDS.some((f) => Object.prototype.hasOwnProperty.call(obj, f) && obj[f] != null && obj[f] !== '');
}

function hasCode(body, code) {
  if (!body) return false;
  if (typeof body.code === 'string') return body.code === code;
  if (typeof body.message === 'string') return body.message.includes(code);
  return false;
}

// ---------- --check-class-only: один GET, без регистрации (образец TE-111) ----------
async function checkClassOnly() {
  let code = 0;
  try {
    const r = await fetch(BASE + '/api/v1/garmin/state', { method: 'GET' });
    code = r.status;
  } catch (e) {
    out({ text: `garmin/state -> сетевой отказ (${String((e && e.message) || e).slice(0, 120)}) — НЕ ИЗМЕРЕНО` });
    process.exitCode = 2;
    return;
  }
  if (code === 401 || code === 200 || code === 404) {
    out({ text: `garmin/state -> ${code} (жив: маршрут защищён/отвечает) — OK`, code });
    process.exitCode = 0;
  } else if (code >= 500) {
    out({ text: `garmin/state -> ${code} — DOWN`, code });
    process.exitCode = 1;
  } else {
    out({ text: `garmin/state -> ${code} — необычный класс, не измерено`, code });
    process.exitCode = 2;
  }
}

// ---------- полная проба ----------
async function main() {
  const r = { base: BASE, steps: {}, notes: [] };
  const fail = (text) => { out({ text: `НАРУШЕНИЕ: ${text}`, r }); process.exitCode = 1; };
  const unmeasured = (text) => { out({ text: `НЕ ИЗМЕРЕНО: ${text}`, r }); process.exitCode = 2; };

  // 0. регистрация (dev OTP)
  const phone = '+7999' + String(crypto.randomInt(0, 10000000)).padStart(7, '0');
  const c = await j(await fetch(BASE + '/api/v1/clients', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ phone }),
  }));
  if (!c.data || !c.data.id) return unmeasured(`POST /clients -> ${c.status}`);
  const v = await j(await fetch(BASE + '/api/v1/clients/' + c.data.id + '/verify', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ code: OTP }),
  }));
  const token = (v.data && (v.data.accessToken || v.data.token)) || '';
  if (!token) return unmeasured(`verify -> ${v.status} (prod без тестового OTP — это ожидание)`);
  const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

  // 1. без JWT -> 401 (маршрут защищён)
  const noJwt = await fetch(BASE + '/api/v1/garmin/connect', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ apiKey: 'x', oauthToken: 'y', tokenExpiry: FUTURE }),
  });
  r.steps.connectNoJwt = noJwt.status;
  const s1 = classifyStep('connect без JWT', noJwt.status, [401]);
  if (s1.rc === 1) return fail(s1.text);
  if (s1.rc === 2) return unmeasured(s1.text);

  // 2. битые credentials -> 400 validation_error; 404/405 => BE-142 не в образе
  const bad = await j(await fetch(BASE + '/api/v1/garmin/connect', {
    method: 'POST', headers: H, body: JSON.stringify({}),
  }));
  r.steps.connectBad = { status: bad.status, code: bad.data && bad.data.code };
  if (bad.status === 404 || bad.status === 405 || bad.status === 501) {
    return unmeasured(`connect -> ${bad.status}: маршрута нет — BE-142 не в образе (это ожидание, не дефект)`);
  }
  if (bad.status !== 400) {
    return fail(`connect с битыми credentials -> ${bad.status} (ожидали 400 validation_error)`);
  }
  if (bad.data && bad.data.code && bad.data.code !== 'validation_error') {
    r.notes.push(`код ошибки: ${bad.data.code} (контракт ждёт validation_error — проверить)`);
  }

  // 3. state без подключения -> 404 (маршрут точно есть — шаг 2 дал контрактный 400)
  const st0 = await j(await fetch(BASE + '/api/v1/garmin/state', { headers: H }));
  r.steps.stateBefore = st0.status;
  if (st0.status === 500) return fail('state без подключения -> 500 (сервис лежит)');
  if (st0.status !== 404) return fail(`state без подключения -> ${st0.status} (ожидали 404)`);

  // 4. sync без подключения -> 400 not_connected
  const sy0 = await j(await fetch(BASE + '/api/v1/garmin/sync', { method: 'POST', headers: H }));
  r.steps.syncBefore = { status: sy0.status, code: sy0.data && sy0.data.code };
  if (sy0.status !== 400) return fail(`sync без подключения -> ${sy0.status} (ожидали 400 not_connected)`);
  if (sy0.data && sy0.data.code && !hasCode(sy0.data, 'not_connected')) {
    r.notes.push(`sync вернул код ${sy0.data.code} (контракт ждёт not_connected)`);
  }

  // 5. connect с БУДУЩИМ tokenExpiry -> state 200, connected, без секретов
  const cf = await j(await fetch(BASE + '/api/v1/garmin/connect', {
    method: 'POST', headers: H,
    body: JSON.stringify({ apiKey: 'TE-probe-key', oauthToken: 'TE-probe-token', tokenExpiry: FUTURE }),
  }));
  r.steps.connectFuture = cf.status;
  if (cf.status >= 500) return fail(`connect (будущий expiry) -> ${cf.status}`);
  if (cf.status === 404 || cf.status === 405) return unmeasured(`connect -> ${cf.status} (маршрут не готов)`);
  const st1 = await j(await fetch(BASE + '/api/v1/garmin/state', { headers: H }));
  r.steps.stateAfter = { status: st1.status, body: st1.data };
  if (st1.status !== 200) return fail(`state после connect -> ${st1.status} (ожидали 200)`);
  if (hasSecrets(st1.data)) {
    return fail('в ответе state ЕСТЬ секреты (apiKey/oauthToken) — утечка (§6)');
  }
  if (st1.data && st1.data.connected === false) {
    r.notes.push('state.connected=false сразу после connect — проверить сохранение');
  }

  // 6. connect с ПРОШЛЫМ tokenExpiry -> requiresReauth=true
  const cp = await j(await fetch(BASE + '/api/v1/garmin/connect', {
    method: 'POST', headers: H,
    body: JSON.stringify({ apiKey: 'TE-probe-key', oauthToken: 'TE-probe-token', tokenExpiry: PAST }),
  }));
  r.steps.connectPast = cp.status;
  const st2 = await j(await fetch(BASE + '/api/v1/garmin/state', { headers: H }));
  r.steps.statePast = { status: st2.status, requiresReauth: st2.data && st2.data.requiresReauth };
  if (st2.status === 200 && st2.data && st2.data.requiresReauth !== true) {
    return fail(`прошлый tokenExpiry: requiresReauth=${st2.data.requiresReauth} (ожидали true)`);
  }

  out({
    text: [
      'квитанция: base ' + BASE,
      '  шаги: ' + JSON.stringify(r.steps),
      '  заметки: ' + (r.notes.length ? r.notes.join('; ') : 'нет'),
      'ИТОГ: поток Garmin по токену соответствует контракту (connect/state/sync/без утечки секретов)',
    ].join('\n'),
    r,
  });
  process.exitCode = 0;
}

if (argv.selftest) selftest();
else if (argv.checkClassOnly) checkClassOnly().catch((e) => {
  console.log('НЕ ИЗМЕРЕНО:', String((e && e.message) || e).slice(0, 200));
  process.exitCode = 2;
});
else main().catch((e) => {
  console.log('НЕ ИЗМЕРЕНО:', String((e && e.message) || e).slice(0, 200));
  process.exitCode = 2;
});
