// helpers.test.js — поведенческая проверка помощников спек БЕЗ браузера.
//
// Зачем: `waitAnyScreen` (TE-072) появился как замена `.or(...).first()`, который выбирал скрытый
// `#view-lock` по порядку в DOM и валил два теста. Проверить это браузером в песочнице нельзя
// (`spawn EPERM`), а статикой — недостаточно: важно, ЧТО помощник возвращает и ЧТО бросает.
// Здесь `tests/api.js` загружается как есть, но с подставным `page`.
//
// Проверяется:
//   1) `waitAnyScreen` возвращает id экрана, который реально показан;
//   2) список экранов передаётся в `waitForFunction` в том же порядке (контракт вызова);
//   3) таймаут по умолчанию 20000 мс, явный — используется как передан;
//   4) если ни один экран не показался — ошибка содержит список ожидаемых И список видимых
//      (а не пустой timeout); по умолчанию видимых ноль;
//   5) ошибка не теряет причину: имя помощника и перечень экранов присутствуют в тексте.
//
// Код возврата: 0 — все случаи как ожидалось; 1 — расхождение.

const path = require('path');
const assert = require('assert');

const API_PATH = path.join(__dirname, 'tests', 'api.js');

function loadApi() {
  // api.js при загрузке пытается зарегистрировать afterEach из @playwright/test; в чистом Node
  // этого модуля нет — там try/catch, поэтому загрузка безопасна.
  delete require.cache[require.resolve(API_PATH)];
  return require(API_PATH);
}

function fakePage(opts = {}) {
  const calls = [];
  return {
    calls,
    waitForFunction: async (fn, arg, options) => {
      calls.push({ fn, arg, options });
      if (opts.reject) throw new Error(opts.reject);
      return { jsonValue: async () => opts.returns || 'view-journal' };
    },
    evaluate: async (fn, arg) => (opts.visible || []),
  };
}

const cases = [];
function test(name, fn) {
  cases.push({ name, fn });
}

test('возвращает id показанного экрана', async () => {
  const { waitAnyScreen } = loadApi();
  // показанный экран — ПОСЛЕДНИЙ в списке: так «вернуть ids[0]» ловится этим же кейсом
  const page = fakePage({ returns: 'view-journal' });
  const id = await waitAnyScreen(page, ['view-lock', 'view-onboarding', 'view-journal'], 5000);
  assert.strictEqual(id, 'view-journal', 'должен вернуться именно показанный экран, а не первый в списке');
});

test('передаёт список экранов в том же порядке', async () => {
  const { waitAnyScreen } = loadApi();
  const page = fakePage({ returns: 'view-journal' });
  const ids = ['view-lock', 'view-onboarding', 'view-journal'];
  await waitAnyScreen(page, ids, 5000);
  assert.deepStrictEqual(page.calls[0].arg, ids, 'порядок экранов не должен меняться');
});

test('таймаут по умолчанию 20000 и явный используется как есть', async () => {
  const { waitAnyScreen } = loadApi();
  const def = fakePage({});
  await waitAnyScreen(def, ['view-journal']);
  assert.deepStrictEqual(def.calls[0].options, { timeout: 20000 }, 'дефолтный таймаут 20000');

  const explicit = fakePage({});
  await waitAnyScreen(explicit, ['view-journal'], 3500);
  assert.deepStrictEqual(explicit.calls[0].options, { timeout: 3500 }, 'явный таймаут должен пройти как есть');
});

test('при провале ошибка называет ожидаемые и видимые экраны', async () => {
  const { waitAnyScreen } = loadApi();
  const page = fakePage({ reject: 'waitForFunction: Timeout 20000ms exceeded', visible: [] });
  let error = null;
  try {
    await waitAnyScreen(page, ['view-lock', 'view-journal'], 20000);
  } catch (e) {
    error = e;
  }
  assert.ok(error, 'при провале обязан быть выброшен Error');
  assert.ok(/view-lock/.test(error.message) && /view-journal/.test(error.message),
    'в тексте должны быть ожидаемые экраны: ' + error.message);
  assert.ok(/видимые/.test(error.message), 'в тексте должен быть блок «видимые»: ' + error.message);
  assert.ok(/20000/.test(error.message), 'в тексте должен быть таймаут: ' + error.message);
});

test('если видимый экран есть, он попадает в сообщение о провале', async () => {
  const { waitAnyScreen } = loadApi();
  const page = fakePage({ reject: 'boom', visible: ['view-onboarding'] });
  let error = null;
  try {
    await waitAnyScreen(page, ['view-journal'], 1000);
  } catch (e) {
    error = e;
  }
  assert.ok(error && /view-onboarding/.test(error.message),
    'видимый экран должен быть назван: ' + (error && error.message));
});

(async () => {
  let ok = 0;
  for (const c of cases) {
    try {
      await c.fn();
      ok += 1;
      console.log('  ok   ' + c.name);
    } catch (e) {
      console.log('  FAIL ' + c.name + ': ' + (e && e.message));
    }
  }
  console.log(`helpers.test.js: ${ok}/${cases.length} (waitAnyScreen: id показанного экрана, таймаут, диагностика при провале)`);
  process.exit(ok === cases.length ? 0 : 1);
})();
