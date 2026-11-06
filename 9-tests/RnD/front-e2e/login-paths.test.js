// login-paths.test.js — поведенческая проверка обвязки входа БЕЗ браузера.
//
// Зачем: ускорения фикстуры (`SEED_SESSION`, «без лишнего reload», повтор при 401/404) нельзя проверить
// статикой, а браузер в песочнице не запускается (`spawn EPERM`). Здесь `tests/api.js` загружается
// как есть, но с подставным `page` и подставным окружением в `evaluate` (`localStorage`, `fetch`,
// `window.Pin`). Проверяется ЛОГИКА путей входа:
//
//   1) сессия закэширована, стенд её принял, журнал отрисован → вход БЕЗ `reload`, PIN поставлен;
//   2) кэш не принят (401) → полный онбординг и `reload` (старый путь не сломан);
//   3) журнал уже открыт и токен на месте → ни засева, ни `reload` (fast path до засева);
//   4) сессию убили посреди входа (401 на /clients/me) → ровно один повтор, затем падение с диагнозом.
//
// Код возврата: 0 — все пути как ожидалось; 1 — расхождение.

const path = require('path');
const assert = require('assert');

const API = path.join(__dirname, 'tests', 'api.js');

function makeEnv(opts = {}) {
  const state = {
    storage: Object.assign({}, opts.storage || {}),
    pinSet: false,
    reloads: 0,
    gotos: 0,
    fetches: [],
    journalVisible: !!opts.journalVisible,
    meStatus: opts.meStatus || 200,
    failJournal: !!opts.failJournal,
    openOnGoto: !!opts.openOnGoto,
    errors: opts.errors || [],
  };
  const ls = {
    getItem: (k) => (k in state.storage ? state.storage[k] : null),
    setItem: (k, v) => { state.storage[k] = String(v); },
    removeItem: (k) => { delete state.storage[k]; },
  };
  global.localStorage = ls;
  global.window = global.window || {};
  global.window.localStorage = ls;
  global.window.Pin = {
    setPin: async () => { state.pinSet = true; },
    hasPin: async () => state.pinSet,
    verifyPin: async () => true,
  };
  global.fetch = async (url, init = {}) => {
    const u = String(url);
    state.fetches.push(u.replace(/^https?:\/\/[^/]+/, ''));
    const ok = (status, body) => ({
      status,
      ok: status >= 200 && status < 300,
      json: async () => body,
      text: async () => JSON.stringify(body),
    });
    // порядок важен: `/clients/{id}/verify` содержит и `/clients`
    if (u.includes('/verify')) return ok(200, { accessToken: 'aa.bb.cc', refreshToken: 'rr' });
    if (u.includes('/api/v1/clients/me/profile')) return ok(200, { displayName: 'TE-SEED' });
    if (u.includes('/api/v1/clients/me')) {
      const st = state.cachedCheck ? state.meStatus : 200;
      return ok(st, st === 200 ? { id: 'client-1' } : { error: 'unauthorized' });
    }
    if (u.includes('/api/v1/clients') && (init.method || 'GET') === 'POST') return ok(200, { id: 'client-1' });
    if (u.includes('/partners') && (init.method || 'GET') === 'POST') return ok(200, { id: 'partner-1' });
    if (u.includes('/partners')) return ok(200, [{ id: 'partner-1' }]);
    if (u.includes('/link-partner')) return ok(200, {});
    return ok(404, {});
  };
  global.document = { getElementById: () => null, querySelectorAll: () => [] };
  global.indexedDB = { open: () => ({ onsuccess: null, onerror: null }) };

  const page = {
    on: () => {},
    off: () => {},
    addInitScript: async () => {},
    goto: async () => { state.gotos += 1; state.journalVisible = state.journalVisible || !!state.openOnGoto; },
    reload: async () => { state.reloads += 1; },
    waitForFunction: async () => true,
    fill: async () => {},
    click: async () => {},
    evaluate: async (fn, arg) => await fn(arg),
    locator: (sel) => ({
      isVisible: async () => (String(sel).includes('view-journal') ? state.journalVisible : false),
      waitFor: async () => {
        if (String(sel).includes('view-journal')) {
          if (state.failJournal) throw new Error('locator.waitFor: Timeout 30000ms exceeded');
          state.journalVisible = true;
        }
      },
      filter: () => ({ count: async () => 0 }),
      count: async () => 0,
      innerText: async () => '',
      fill: async () => {},
      click: async () => {},
      first: () => ({ click: async () => {}, isVisible: async () => false }),
    }),
  };
  // ошибки браузера, которые видит фикстура
  Object.defineProperty(page, '__xdiaryErrors', { get: () => state.errors, configurable: true });
  return { page, state };
}

function freshApi() {
  delete require.cache[require.resolve(API)];
  return require(API);
}

const results = [];
async function check(name, fn) {
  try {
    await fn();
    results.push([name, true]);
  } catch (e) {
    results.push([name, false, e]);
  }
}

(async () => {
  await check('после засева журнал открыт: без reload, PIN поставлен', async () => {
    const env = makeEnv({ journalVisible: false, openOnGoto: false });
    const api = freshApi();
    await api.seedSession(env.page);            // полный засев: кэш пуст, журнала ещё нет
    assert.strictEqual(env.state.reloads, 0, 'seedSession не должен перезагружать страницу');
    assert.ok(env.state.storage['xdiary.accessToken'], 'токен должен попасть в localStorage');
    env.state.openOnGoto = true;                 // приложение отрисует журнал при следующем goto
    await api.loginWithPin(env.page);
    assert.strictEqual(env.state.reloads, 0, 'после засева reload не нужен (журнал открыт)');
    assert.ok(env.state.pinSet, 'локальный PIN ставится и на быстром пути после засева');
  });

  await check('кэш не принят (401): полный онбординг и reload', async () => {
    const env = makeEnv({ journalVisible: false });
    const api = freshApi();
    await api.seedSession(env.page);
    env.state.cachedCheck = true;                // кэш-проверка пойдёт в /clients/me
    env.state.meStatus = 401;                    // стенд «забыл» клиента
    env.state.journalVisible = false;
    await api.loginWithPin(env.page);
    assert.ok(env.state.reloads >= 1, 'на медленном пути reload обязателен');
  });

  await check('журнал уже открыт: ни засева, ни reload, ни нового PIN', async () => {
    const env = makeEnv({ journalVisible: true, storage: { 'xdiary.accessToken': 'aa.bb.cc' } });
    const api = freshApi();
    const before = env.state.gotos;
    await api.loginWithPin(env.page);
    assert.strictEqual(env.state.reloads, 0, 'reload не нужен');
    assert.strictEqual(env.state.gotos, before, 'goto не нужен');
    assert.strictEqual(env.state.pinSet, false, 'на этом пути PIN не переставляется (он уже стоит)');
  });

  await check('401 посреди входа: один повтор, затем диагноз', async () => {
    const env = makeEnv({
      journalVisible: false,
      failJournal: true,
      errors: ['http 401 /api/v1/clients/me'],
    });
    const api = freshApi();
    let failed = false;
    try {
      await api.loginWithPin(env.page);
    } catch (e) {
      failed = true;
      assert.ok(/Вход не завершился/.test(String(e.message)), 'ошибка должна быть диагнозом');
      assert.ok(/повторов 1/.test(String(e.message)), 'в диагнозе должно быть число повторов');
    }
    assert.ok(failed, 'при двух неудачах вход обязан падать');
  });

  const failed = results.filter(([, ok]) => !ok);
  for (const [name, ok, err] of results) {
    console.log((ok ? '  [OK]   ' : '  [FAIL] ') + name + (ok ? '' : ' :: ' + String((err && err.message) || err)));
  }
  console.log(`login-paths: ${results.length - failed.length}/${results.length} путей входа как ожидалось`);
  process.exitCode = failed.length ? 1 : 0;
})();
