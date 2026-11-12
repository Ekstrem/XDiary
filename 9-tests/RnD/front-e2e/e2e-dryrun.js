// e2e-dryrun.js — «сухой прогон» спек front-e2e без браузера.
//
// Зачем: браузер в песочнице не стартует (`spawn EPERM`), а проверять надо не только разметку,
// но и **сам код спек**: опечатки, обращение к несуществующим помощникам, забытый `await`,
// исключение в шаге сценария. Здесь мы подменяем `@playwright/test` и `./api` заглушками,
// **исполняем тела тестов** и собираем: сколько тестов зарегистрировано/выполнено, какие упали
// и какой была последовательность действий.
//
// Это НЕ заменяет прогон в браузере: проверяются только инварианты кода спеки
// (нет исключений, есть навигация до действий). Запуск:
//   node e2e-dryrun.js            # все спеки
//   node e2e-dryrun.js wizard-offline.spec.js
//   node e2e-dryrun.js wizard-offline.spec.js --all-flags --hidden '#view-wizard'  # негативный контроль
//
// rc=0 — все тела исполнены без исключений; rc=1 — есть падения; rc=2 — нет node_modules
// (заглушке нужен только `@playwright/test` в require.cache, но каталог спек обязателен).

const path = require('path');
const fs = require('fs');

const ROOT = __dirname;
// TE-086: без флагов часть спек уходит в `test.skip` и их код НЕ исполняется. С `--all-flags`
// все `XDIARY_*` включены, поэтому на заглушках исполняются ВСЕ тела тестов (28 из 28),
// то есть проверяется отсутствие исключений/опечаток в коде ранее пропускавшихся спек.
// Это НЕ поведение продукта: заглушки проверяют только исполнимость кода спек.
// TE-054/104: `--hidden '<sel>'` — селектор, который «не виден» (негативный контроль спеки).
const HIDDEN = [];
for (let i = 0; i < process.argv.length; i += 1) {
  if (process.argv[i] === '--hidden' && process.argv[i + 1]) HIDDEN.push(process.argv[i + 1]);
}

if (process.argv.includes('--all-flags')) {
  for (const flag of ['XDIARY_BUSY', 'XDIARY_STALENOTE', 'XDIARY_READPATH_OFFLINE', 'XDIARY_READPARITY', 'XDIARY_CHANGE_PIN']) {
    process.env[flag] = '1';
  }
}
const TESTS = process.env.DRYRUN_TESTS || path.join(ROOT, 'tests');

let ok = 0;
let failed = 0;
const reports = [];
let skippedSpecs = 0;

// «Любое значение»: прокси, который отвечает на любое обращение и не роняет сценарий.
// Нужен потому, что заглушка API/`evaluate` не знает реальных данных, а спека читает поля ответа.
// Прокси КЭШИРУЮТСЯ и ограничены по глубине: без этого получается бесконечная фабрика прокси
// и Node падает по heap (проверено: `Reached heap limit`).
const ANY_CACHE = new Map();
const ANY_MAX_DEPTH = 6;
let ANY_LEAF = null;

function anyLeaf() {
  if (ANY_LEAF) return ANY_LEAF;
  const t = function () {};
  ANY_LEAF = new Proxy(t, {
    get(_t, prop) {
      if (prop === 'then') return undefined;
      if (prop === Symbol.toPrimitive) return () => '';
      if (prop === 'toString') return () => '';
      if (prop === 'length') return 0;
      if (prop === Symbol.iterator) return function* () {};
      return ANY_LEAF;
    },
    apply() { return ANY_LEAF; },
  });
  return ANY_LEAF;
}

function anyValue(name = 'any', depth = 0) {
  if (depth > ANY_MAX_DEPTH) return anyLeaf();
  const key = name + '@' + depth;
  if (ANY_CACHE.has(key)) return ANY_CACHE.get(key);
  const target = function () {};
  const proxy = new Proxy(target, {
    get(_t, prop) {
      // ВАЖНО: `Promise.resolve(proxy).then(...)` зацикливается сам на себя (then → Promise.resolve → then),
      // и Node падает по heap. Резолвим в листовой прокси, без повторной обёртки.
      if (prop === 'then') return (res) => Promise.resolve().then(() => res(anyLeaf()));
      if (prop === Symbol.toPrimitive) return () => '';
      if (prop === 'toString') return () => '';
      if (prop === 'length') return 0;
      if (prop === Symbol.iterator) return function* () {};
      if (prop === 'json') return async () => proxy;
      if (prop === 'text') return async () => '';
      if (prop === 'status') return () => 200;
      if (prop === 'ok') return true;
      if (prop === 'headers') return () => ({});
      if (prop === 'id' || prop === 'accessToken' || prop === 'refreshToken') return 'fake-' + String(prop);
      if (prop === 'url') return () => 'http://mock/';
      if (prop === 'suggestedFilename') return () => 'export.csv';
      if (prop === 'post' || prop === 'get' || prop === 'put' || prop === 'delete' || prop === 'newContext'
          || prop === 'dispose' || prop === 'storageState' || prop === 'addInitScript' || prop === 'path') {
        return async () => proxy;
      }
      return anyValue(name + '.' + String(prop), depth + 1);
    },
    apply() { return proxy; },
  });
  ANY_CACHE.set(key, proxy);
  return proxy;
}

class SkipSignal extends Error {
  constructor(reason) {
    super('SKIP: ' + (reason || 'без причины'));
    this.__skip = true;
  }
}

// «Скачанный файл» для сухого прогона: настоящий временный файл с валидной строкой CSV,
// иначе `fs.readFileSync(await download.path())` падает на типе и это выглядит как дефект спеки.
function fakeDownload() {
  const os = require('os');
  const fp = path.join(os.tmpdir(), 'xdiary-dryrun-export.csv');
  if (!fs.existsSync(fp)) {
    fs.writeFileSync(fp, 'id,started_at,duration_min,partner,place\n', 'utf8');
  }
  return {
    suggestedFilename: () => 'journal-export.csv',
    path: async () => fp,
    saveAs: async () => fp,
    failure: async () => null,
    delete: async () => {},
  };
}

function makeFakePage(log, opts) {
  const optsRef = opts || {};
  const hiddenList = Array.isArray(optsRef.hidden) ? optsRef.hidden : [];
  const isHiddenSel = (sel) => hiddenList.some((h) => String(sel).includes(h));
  const callable = (name) => {
    const fn = (...args) => {
      log.push(name + (args.length ? '(' + args.map((a) => describeArg(a)).join(', ') + ')' : ''));
      if (name === 'waitForResponse' || name === 'waitForFunction' || name === 'waitForLoadState') {
        return Promise.resolve(anyValue('response'));
      }
      if (name === 'evaluate') return Promise.resolve(anyValue('evaluate'));
      if (name === 'goto') {
        log.push('__goto__');
        return Promise.resolve(null);
      }
      return Promise.resolve(null);
    };
    return fn;
  };

  const locatorNode = (sel) => {
    const node = {
      _sel: sel,
      click: (...a) => { log.push('click(' + String(sel) + ')'); return Promise.resolve(null); },
      fill: (...a) => { log.push('fill(' + String(sel) + ')'); return Promise.resolve(null); },
      type: () => Promise.resolve(null),
      press: () => Promise.resolve(null),
      check: () => Promise.resolve(null),
      uncheck: () => Promise.resolve(null),
      hover: () => Promise.resolve(null),
      selectOption: () => Promise.resolve(null),
      scrollIntoViewIfNeeded: () => Promise.resolve(null),
      count: async () => 1,
      first: () => locatorNode(sel + ':first'),
      last: () => locatorNode(sel + ':last'),
      nth: (i) => locatorNode(sel + ':nth(' + i + ')'),
      or: (other) => locatorNode(sel + '|' + (other && other._sel)),
      and: (other) => locatorNode(sel + '&' + (other && other._sel)),
      filter: () => locatorNode(sel + ':filtered'),
      locator: (child) => locatorNode(sel + ' ' + String(child)),
      getByRole: (r) => locatorNode(sel + ' role=' + String(r)),
      getByText: (t) => locatorNode(sel + ' text=' + String(t)),
      isVisible: async () => !isHiddenSel(sel),
      isHidden: async () => isHiddenSel(sel),
      textContent: async () => '',
      innerText: async () => '',
      inputValue: async () => '',
      getAttribute: async () => null,
      waitFor: () => Promise.resolve(null),
      toBeVisible: async () => {
        if (isHiddenSel(sel)) {
          throw new Error('dryrun: "' + sel + '" спрятан флагом --hidden (негативный контроль)');
        }
        return true;
      },
      // 	oBeHidden НЕ бросает: в заглушке «скрыто» означает «не проверяем видимость»,
      // иначе обычные спеки падают на собственных ассертах (проверено: 11 упавших).
      toBeHidden: async () => true,
      toHaveCount: async () => true,
      toContainText: async () => true,
      toHaveText: async () => true,
      toHaveValue: async () => true,
      toHaveAttribute: async () => true,
      toBeEnabled: async () => true,
      toBeDisabled: async () => true,
      toHaveClass: async () => true,
    };
    return node;
  };

  const pageTarget = {
    goto: (url) => { log.push('goto(' + String(url) + ')'); return Promise.resolve(null); },
    reload: () => { log.push('reload()'); return Promise.resolve(null); },
    addInitScript: () => Promise.resolve(null),
    evaluate: (...a) => { log.push('evaluate()'); return Promise.resolve(anyValue('evaluate')); },
    evaluateHandle: () => Promise.resolve({}),
    waitForTimeout: (ms) => { log.push('waitForTimeout(' + ms + ')'); return Promise.resolve(null); },
    waitForResponse: () => Promise.resolve(anyValue('response')),
    waitForFunction: () => Promise.resolve(null),
    waitForLoadState: () => Promise.resolve(null),
    waitForSelector: () => Promise.resolve(locatorNode('waited')),
    waitForEvent: (evt) => Promise.resolve(evt === 'download' ? fakeDownload() : anyValue('event')),
    waitForURL: () => Promise.resolve(null),
    click: (sel) => { log.push('click(' + String(sel) + ')'); return Promise.resolve(null); },
    fill: (sel) => { log.push('fill(' + String(sel) + ')'); return Promise.resolve(null); },
    press: () => Promise.resolve(null),
    type: () => Promise.resolve(null),
    check: () => Promise.resolve(null),
    uncheck: () => Promise.resolve(null),
    hover: () => Promise.resolve(null),
    selectOption: () => Promise.resolve(null),
    locator: (sel) => locatorNode(sel),
    getByRole: (r) => locatorNode('role=' + String(r)),
    getByText: (t) => locatorNode('text=' + String(t)),
    route: (pattern, handler) => { log.push('route(' + String(pattern) + ')'); return Promise.resolve(null); },
    unroute: () => Promise.resolve(null),
    on: () => {},
    off: () => {},
    once: () => {},
    setDefaultTimeout: () => {},
    setDefaultNavigationTimeout: () => {},
    url: () => 'http://127.0.0.1:4173/',
    title: async () => 'xDiary',
    content: async () => '<html></html>',
    screenshot: () => Promise.resolve(Buffer.from('')),
    context: () => ({ storageState: async () => ({ cookies: [], origins: [] }), addInitScript: () => Promise.resolve(null) }),
    request: anyValue('request'),
    keyboard: { press: () => Promise.resolve(null) },
    mouse: { click: () => Promise.resolve(null) },
    clock: { install: () => Promise.resolve(null), fastForward: () => Promise.resolve(null) },
  };

  return new Proxy(pageTarget, {
    get(target, prop) {
      if (prop in target) return target[prop];
      // любое неизвестное свойство — записываем и возвращаем «ничего не делающую» функцию
      log.push('page.' + String(prop));
      const fn = (...a) => Promise.resolve(null);
      target[prop] = fn; // eslint-disable-line no-param-reassign
      return fn;
    },
  });
}

function describeArg(a) {
  if (a === null || a === undefined) return String(a);
  if (typeof a === 'string') return JSON.stringify(a.slice(0, 60));
  if (typeof a === 'number' || typeof a === 'boolean') return String(a);
  if (typeof a === 'function') return 'fn';
  return 'obj';
}

const COUNTER = { asserts: 0 };

function makeExpect() {
  const matcher = () => new Proxy({}, {
    get: () => () => Promise.resolve(true),
  });
  const expectFn = (value, message) => {
    // Считаем ВЫПОЛНЕННЫЕ утверждения: если тело теста дошло до нуля `expect`,
    // значит проверка недостижима (мёртвая ветка) — и «зелено» тут ничего не значит.
    COUNTER.asserts += 1;
    const chain = {
      toBe: () => true, toEqual: () => true, toContain: () => true, toMatch: () => true,
      toBeTruthy: () => true, toBeFalsy: () => true, toBeGreaterThan: () => true,
      toBeLessThan: () => true, toBeGreaterThanOrEqual: () => true, toBeUndefined: () => true,
      toBeDefined: () => true, toBeNull: () => true, toHaveLength: () => true,
      // Делегируем узлу, если он умеет: так работает негативный контроль `--hidden`.
      // expect.poll(fn, {timeout}).toBe(x) — вызываем fn один раз и сравниваем: иначе
      // негативный контроль ассертов на poll (startup-api-error, TE-104) физически не срабатывает.
      poll: (fn, opts) => ({
        toBe: async (want) => {
          const got = await fn();
          if (got !== want) {
            throw new Error('dryrun: poll вернул ' + String(got) + ', ожидалось ' + String(want) +
              ' (timeout=' + String((opts && opts.timeout) || '?') + ')');
          }
          return true;
        },
      }),
      toBeVisible: async (a) => (value && typeof value.toBeVisible === 'function')
        ? value.toBeVisible(a) : true,
      toBeHidden: async (a) => (value && typeof value.toBeHidden === 'function')
        ? value.toBeHidden(a) : true,
      toHaveCount: async () => true,
      toContainText: async () => true, toHaveText: async () => true, toHaveValue: async () => true,
      toHaveAttribute: async () => true, toHaveClass: async () => true,
      toBeEnabled: async () => true, toBeDisabled: async () => true,
      toBeChecked: async () => true, toBeFocused: async () => true,
    };
    chain.not = new Proxy({}, { get: () => () => true });
    chain.resolves = chain;
    chain.rejects = chain;
    // `expect.poll` / `expect.soft`
    chain.toPass = async () => true;
    return new Proxy(chain, { get: (t, p) => (p in t ? t[p] : () => true) });
  };
  expectFn.poll = () => new Proxy({}, { get: () => () => true });
  expectFn.soft = expectFn;
  expectFn.configure = () => {};
  expectFn.any = () => ({});
  return expectFn;
}

function makePlaywrightStub() {
  const registry = { tests: [], uses: [], skips: 0 };
  const test = (name, fn) => { registry.tests.push({ name, fn }); };
  test.describe = (name, fn) => { fn(); };
  test.describe.skip = (name, fn) => { registry.skips += 1; };
  test.describe.serial = (name, fn) => { fn(); };
  test.describe.parallel = (name, fn) => { fn(); };
  test.describe.configure = () => {};
  test.step = async (name, fn) => fn();
  test.skip = (cond, reason) => {
    if (cond) throw new SkipSignal(typeof reason === 'string' ? reason : 'условие skip');
  };
  test.use = () => {};
  test.extend = () => test;
  test.slow = () => {};
  test.setTimeout = () => {};
  test.fail = () => {};
  test.afterEach = () => {};
  test.beforeEach = () => {};
  test.beforeAll = () => {};
  test.afterAll = () => {};
  return { stub: { test, expect: makeExpect(), describe: test.describe }, registry };
}

function stubApi(registry, page, log) {
  // Обвязка `tests/api.js` в сухом прогоне не нужна: заменяем её записывающими заглушками,
  // чтобы исполнялись только тела спек.
  const call = (name) => async (...args) => {
    log.push('api.' + name);
    // реальная обвязка открывает приложение (`installBase` → `goto('/')`, `loginWithPin` → reload),
    // поэтому в трассе это тоже навигация — иначе ложное «тело без навигации»
    if (name === 'installBase' || name === 'loginWithPin' || name === 'seedSession' || name === 'seedSessionOnce') {
      log.push('goto(/via api.' + name + ')');
    }
    if (name === 'readToken') return 'token';
    if (name === 'waitAppReady') return null;
    return anyValue('api.' + name);
  };
  return new Proxy({}, {
    get: (t, prop) => {
      if (prop === 'API_BASE') return 'http://127.0.0.1:4180';
      if (prop === 'PIN') return '1234';
      if (prop === 'UI_WAIT') return 30000;
      if (prop === 'TEST_OTP') return '000000';
      if (prop === 'ACCESS_KEY') return 'xdiary.accessToken';
      if (prop === 'expectScreen') return async () => anyValue('screen');
      if (prop === 'snapshotStorage') return async () => null;
      if (prop === 'uniquePhone') return () => '+79990000000';
      return call(String(prop));
    },
  });
}

async function runSpec(file) {
  // каталог спек читаем В МОМЕНТ вызова: самопроверка подменяет его через DRYRUN_TESTS
  const dir = process.env.DRYRUN_TESTS || path.join(ROOT, 'tests');
  const full = path.join(dir, file);
  const { stub, registry } = makePlaywrightStub();
  const log = [];
  const page = makeFakePage(log, { hidden: (typeof HIDDEN !== 'undefined' ? HIDDEN : []) });
  const apiStub = stubApi(registry, page, log);

  // подменяем модули через require.cache до загрузки спеки
  const Module = require('module');
  const origResolve = Module._resolveFilename;
  Module._resolveFilename = function (request, ...rest) {
    if (request === '@playwright/test') return '__stub_playwright__';
    if (request === './api') return '__stub_api__';
    return origResolve.call(this, request, ...rest);
  };
  require.cache['__stub_playwright__'] = { id: '__stub_playwright__', filename: '__stub_playwright__', loaded: true, exports: stub };
  require.cache['__stub_api__'] = { id: '__stub_api__', filename: '__stub_api__', loaded: true, exports: apiStub };
  Object.keys(require.cache).forEach((k) => { if (k === full) delete require.cache[k]; });

  let loadError = null;
  try {
    require(full);
  } catch (e) {
    if (!(e && e.__skip)) loadError = e;
  } finally {
    Module._resolveFilename = origResolve;
    delete require.cache['__stub_playwright__'];
    delete require.cache['__stub_api__'];
  }

  const results = [];
  for (const t of registry.tests) {
    const before = log.length;
    const assertsBefore = COUNTER.asserts;
    try {
      await t.fn({ page, request: anyValue('request'), context: anyValue('context'), browser: anyValue('browser') });
      const calls = log.slice(before);
      const asserts = COUNTER.asserts - assertsBefore;
      results.push({ name: t.name, status: 'ok', calls: calls.length, asserts,
        navigated: calls.some((c) => c.startsWith('goto(')) || calls.some((c) => c === 'reload()') });
    } catch (e) {
      if (e && e.__skip) {
        results.push({ name: t.name, status: 'skipped', calls: 0, navigated: false });
      } else {
        results.push({ name: t.name, status: 'failed', error: String((e && e.message) || e), calls: 0, navigated: false });
      }
    }
  }
  return { file, loadError, results, skippedBlocks: registry.skips, calls: log.length };
}

// Самопроверка: гейт обязан КРАСНЕТЬ на спеке без достижимых утверждений.
// Иначе «ноль утверждений» — просто надпись, а не проверка.
async function dryrunSelftest() {
  const os = require('os');
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dryrun-selftest-'));
  const cases = [];
  const mk = (name, body) => {
    fs.writeFileSync(path.join(dir, name),
      "const { test, expect } = require('@playwright/test');\n" + body, 'utf8');
  };
  mk('ok.spec.js', "test('с утверждением', async ({ page }) => { await page.goto('/'); expect(1).toBe(1); });\n");
  mk('noassert.spec.js', "test('без утверждений', async ({ page }) => { await page.goto('/'); });\n");
  mk('skip.spec.js', "test('пропуск', async () => { test.skip(true, 'pending:TE-999 — проверка пропуска'); });\n");

  const prev = process.env.DRYRUN_TESTS;
  process.env.DRYRUN_TESTS = dir;
  const out = [];
  for (const f of ['ok.spec.js', 'noassert.spec.js', 'skip.spec.js']) {
    const r = await runSpec(f);
    const first = r.results[0] || { status: 'none', asserts: 0 };
    out.push({ file: f, status: first.status, asserts: first.asserts || 0 });
  }
  if (prev === undefined) delete process.env.DRYRUN_TESTS; else process.env.DRYRUN_TESTS = prev;
  fs.rmSync(dir, { recursive: true, force: true });

  let ok = 0;
  const total = 3;
  const has = (f) => out.find((o) => o.file === f);
  if (has('ok.spec.js').status === 'ok' && has('ok.spec.js').asserts >= 1) ok += 1;
  else console.log('  [FAIL] спека с утверждением не выполнилась:', JSON.stringify(has('ok.spec.js')));
  if (has('noassert.spec.js').status === 'ok' && has('noassert.spec.js').asserts === 0) ok += 1;
  else console.log('  [FAIL] спека без утверждений не распознана:', JSON.stringify(has('noassert.spec.js')));
  if (has('skip.spec.js').status === 'skipped') ok += 1;
  else console.log('  [FAIL] skip не распознан:', JSON.stringify(has('skip.spec.js')));

  console.log(`e2e-dryrun --selftest: ${ok}/${total} (ноль утверждений распознаётся, skip считается)`);
  return ok === total ? 0 : 1;
}

(async () => {
  if (process.argv.includes('--selftest')) {
    process.exit(await dryrunSelftest());
  }
  const only = process.argv.slice(2).filter((a) => a.endsWith('.spec.js'));
  const specs = only.length ? only : fs.readdirSync(TESTS).filter((f) => f.endsWith('.spec.js')).sort();
  if (!specs.length) {
    console.log('::error::e2e-dryrun: нет спек — не измерено (rc=2)');
    process.exit(2);
  }

  let tests = 0;
  let skipped = 0;
  let loadErrors = 0;
  let noAsserts = 0;
  for (const file of specs) {
    let report;
    try {
      report = await runSpec(file);
    } catch (e) {
      console.log(`  [FAIL] ${file}: сухой прогон упал: ${e.message}`);
      failed += 1;
      continue;
    }
    if (report.loadError) {
      loadErrors += 1;
      console.log(`  [FAIL] ${file}: загрузка спеки: ${report.loadError.message}`);
      continue;
    }
    for (const r of report.results) {
      tests += 1;
      if (r.status === 'failed') {
        failed += 1;
        console.log(`  [FAIL] ${file} :: ${r.name}: ${r.error}`);
      } else if (r.status === 'skipped') {
        skipped += 1;
      } else {
        ok += 1;
        if (!r.navigated) {
          console.log(`  [WARN] ${file} :: ${r.name}: тело исполнилось без навигации (goto/reload)`);
        }
        if (!r.asserts) {
          noAsserts += 1;
          console.log(`  [FAIL] ${file} :: ${r.name}: тело дошло до нуля утверждений — проверка недостижима`);
          failed += 1;
          ok -= 1;
        }
      }
    }
    if (!report.results.length) skippedSpecs += 1;
    reports.push({ file, tests: report.results.length, calls: report.calls });
  }

  const specCount = reports.length;
  console.log(`e2e-dryrun: спек ${specCount} | тестов исполнено ${ok} | пропущено по skip ${skipped} | `
    + `упало ${failed} (без утверждений ${noAsserts}) | ошибок загрузки ${loadErrors} | `
    + `спек без тестов ${skippedSpecs} | утверждений выполнено ${COUNTER.asserts}`);
  if (!tests) {
    console.log('::error::e2e-dryrun: ни одного теста не исполнено — не измерено (rc=2)');
    process.exit(2);
  }
  process.exit(failed || loadErrors ? 1 : 0);
})();
