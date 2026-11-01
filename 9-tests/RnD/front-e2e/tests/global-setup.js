// Глобальный сетап прогона: гарантируем, что PIN dev-стенда = XDIARY_E2E_PIN.
// PIN на стенде один на всех, а `change-pin` его меняет; если прогон падал раньше
// восстановления в `finally`, остаётся чужой PIN и все тесты валятся «JWT не получен».
const { request } = require('@playwright/test');
const { API_BASE, PIN } = require('./api');

const RESIDUALS = ['5678', '0000', '1111', '4321', '12345678'];

module.exports = async function globalSetup() {
  try {
    await checkPin();
  } catch (e) {
    // ошибка глобального сетапа не попадает в onTestEnd — печатаем ::error:: сами,
    // иначе job падает «пустым» (как прогон 8efbc80: 56с, ни одной аннотации по тесту)
    console.log('::error::' + JSON.stringify({ title: 'globalSetup', error: String((e && e.message) || e) }));
    throw e;
  }
};

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
