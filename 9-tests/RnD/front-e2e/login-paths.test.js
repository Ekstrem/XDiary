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
//   4) сессию убили посреди входа (401 на /clients/me) → ровно один повтор, затем падение с диагнозом;
//   5) PIN не снимает замок → диагноз с экранами (а не пустой timeout);
//   6) `expectScreen` несёт текст тоста и имя шага;
//   7) сетевой сбой `POST /verify` → повтор вытягивает засев;
//   8) сетевые сбои исчерпали попытки → падение с причиной, а не «пусто»;
//   9) кэш принят API, но журнал не открылся → полный онбординг (иначе массовое «вход не завершился»).
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
    lockVisible: !!opts.lockVisible,
    lockStuck: !!opts.lockStuck,
    blockJournal: false,
    toast: opts.toast || '',
    netFailVerify: opts.netFailVerify || 0,
    verifyCalls: 0,
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
    if (u.includes('/verify')) {
      state.verifyCalls += 1;
      if (state.verifyCalls <= state.netFailVerify) throw new TypeError('Failed to fetch');
      return ok(200, { accessToken: 'aa.bb.cc', refreshToken: 'rr' });
    }
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
    waitForTimeout: async () => {},
    fill: async () => {},
    click: async () => {},
    evaluate: async (fn, arg) => await fn(arg),
    locator: (sel) => ({
      isVisible: async () => {
        const s2 = String(sel);
        if (s2.includes('view-journal')) return state.journalVisible;
        if (s2.includes('view-lock')) return state.lockVisible;
        return false;
      },
      waitFor: async (o = {}) => {
        const s2 = String(sel);
        if (s2.includes('view-journal')) {
          if (state.failJournal) throw new Error('locator.waitFor: Timeout 30000ms exceeded');
          if (state.blockJournal) throw new Error('locator.waitFor: Timeout 5000ms exceeded');
          state.journalVisible = true;
        }
        if (s2.includes('view-lock') && o.state === 'hidden') {
          if (state.lockStuck) throw new Error('locator.waitFor: Timeout 15000ms exceeded');
          state.lockVisible = false;
        }
      },
      filter: () => ({ count: async () => 0 }),
      count: async () => 0,
      innerText: async () => state.toast,
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

  await check('замок не уходит после PIN: диагноз с экранами', async () => {
    const env = makeEnv({ journalVisible: false, lockVisible: true, lockStuck: true });
    const api = freshApi();
    let msg = '';
    try {
      await api.unlockIfLocked(env.page, '1234');
    } catch (e) {
      msg = String(e && e.message);
    }
    assert.ok(/PIN введён, но #view-lock не скрылся/.test(msg), 'ожидали диагноз про замок, получили: ' + msg.slice(0, 120));
    assert.ok(/views=/.test(msg), 'в диагнозе должен быть снимок экранов');
  });

  await check('expectScreen несёт текст тоста в ошибке', async () => {
    const env = makeEnv({ journalVisible: false, failJournal: true, toast: 'Ошибка: сервер недоступен' });
    const api = freshApi();
    let msg = '';
    try {
      await api.expectScreen(env.page, '#view-journal', 'проверка экрана', 100);
    } catch (e) {
      msg = String(e && e.message);
    }
    assert.ok(/Шаг не пройден: проверка экрана/.test(msg), 'ожидали «Шаг не пройден», получили: ' + msg.slice(0, 120));
    assert.ok(/сервер недоступен/.test(msg), 'в ошибке должен быть текст тоста');
  });

  await check('сетевой сбой засева: повтор вытягивает вход', async () => {
    const env = makeEnv({ journalVisible: false, openOnGoto: true, netFailVerify: 1 });
    const api = freshApi();
    await api.seedSession(env.page);            // первый /verify падает сетью
    assert.ok(env.state.storage['xdiary.accessToken'], 'после повтора токен должен появиться');
    assert.ok(env.state.verifyCalls >= 2, 'должен быть повтор запроса, вызовов: ' + env.state.verifyCalls);
  });

  await check('сетевые сбои исчерпали попытки: падение с причиной', async () => {
    const env = makeEnv({ journalVisible: false, netFailVerify: 99 });
    const api = freshApi();
    let msg = '';
    try {
      await api.seedSession(env.page);
    } catch (e) {
      msg = String(e && e.message);
    }
    assert.ok(/seedSession/.test(msg), 'ожидали ошибку засева, получили: ' + msg.slice(0, 120));
    assert.ok(/Failed to fetch|шаг verify|попыт/i.test(msg), 'в причине должна быть сеть, получили: ' + msg.slice(0, 160));
  });

  await check('кэш принят API, но журнал не открылся: полный онбординг', async () => {
    const env = makeEnv({ journalVisible: false, openOnGoto: false });
    const api = freshApi();
    await api.seedSession(env.page);             // полный засев, кэш заполнен
    const verifyBefore = env.state.verifyCalls;
    env.state.blockJournal = true;               // приложение журнал не открывает
    await api.seedSession(env.page);             // кэш-путь обязан отказаться от кэша
    assert.ok(env.state.verifyCalls > verifyBefore,
      'должен был пройти полный онбординг (/verify снова), вызовов: ' + env.state.verifyCalls);
  });

  const failed = results.filter(([, ok]) => !ok);
  for (const [name, ok, err] of results) {
    console.log((ok ? '  [OK]   ' : '  [FAIL] ') + name + (ok ? '' : ' :: ' + String((err && err.message) || err)));
  }
  console.log(`login-paths: ${results.length - failed.length}/${results.length} путей входа как ожидалось`);
  process.exitCode = failed.length ? 1 : 0;
})();
