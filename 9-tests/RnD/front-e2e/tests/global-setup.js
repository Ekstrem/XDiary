// Глобальный сетап прогона: гарантируем, что PIN dev-стенда = XDIARY_E2E_PIN.
// PIN на стенде один на всех, а `change-pin` его меняет; если прогон падал раньше
// восстановления в `finally`, остаётся чужой PIN и все тесты валятся «JWT не получен».
//
// DO-017 (P0): до 01.11 E2E гонялся против ПРОДА — `api.js` дефолтился на VPS-1, и UI-тесты
// писали тестовые записи/меняли PIN в реальном дневнике. Здесь жёсткий предохранитель:
// ни один прогон не стартует, если стенд не dev/локальный.
const { request } = require('@playwright/test');
const { API_BASE, PIN } = require('./api');

const RESIDUALS = ['5678', '0000', '1111', '4321', '12345678'];
const ALLOWED_HOSTS = ['xdiary-vps2.taile0b6ee.ts.net', 'localhost', '127.0.0.1'];

module.exports = async function globalSetup() {
  try {
    assertNotProd();
    await checkPin();
  } catch (e) {
    // ошибка глобального сетапа не попадает в onTestEnd — печатаем ::error:: сами,
    // иначе job падает «пустым» (как прогон 8efbc80: 56с, ни одной аннотации по тесту)
    console.log('::error::' + JSON.stringify({ title: 'globalSetup', error: String((e && e.message) || e) }));
    throw e;
  }
};

// DO-017: E2E разрешены ТОЛЬКО против dev или локального стенда. VPS-1 — реальный
// дневник владельца; любой прогон туда = порча данных, поэтому падаем до первого теста.
function assertNotProd() {
  const ok = ALLOWED_HOSTS.some((h) => API_BASE.includes(h));
  if (!ok) {
    throw new Error(
      'E2E разрешены только против dev/локального стенда (' + ALLOWED_HOSTS.join(', ') + '), ' +
      'а XDIARY_E2E_BASE_URL=' + API_BASE + '. Прод (VPS-1) — реальный дневник, прогон остановлен (DO-017).'
    );
  }
  console.log('стенд E2E: ' + API_BASE + ' (не прод) — ок');
}

async function checkPin() {
  const ctx = await request.newContext();
  const tokenStatus = async (pin) =>
    (await ctx.post(API_BASE + '/api/v1/privacy/token', { data: { pin } })).status();

  let st = await tokenStatus(PIN);

  if (st !== 200) {
    // пробуем вернуть нужный PIN, подставляя известные остатки прошлых прогонов
    for (const old of RESIDUALS) {
      const r = await ctx.post(API_BASE + '/api/v1/privacy/pin/set', { data: { pin: PIN, currentPin: old } });
      if (r.status() === 200 && (await tokenStatus(PIN)) === 200) { st = 200; break; }
    }
  }

  await ctx.dispose();
  if (st !== 200) {
    throw new Error(
      'PIN на стенде ' + API_BASE + ' не ' + PIN + ' и не восстанавливается известными остатками — ' +
      'нужен серверный сброс PIN на dev (@DO)'
    );
  }
  console.log('PIN стенда проверен: ' + PIN);
}
