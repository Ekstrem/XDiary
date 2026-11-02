// Глобальный сетап прогона.
//
// TE-033: по новой модели (FE-025/BE-036/BE-038) PIN перестал быть первичным входом —
// E2E получает client-JWT через `seedSession` (телефон → OTP → client-JWT), а PIN это
// локальный замок, который спеки ставят сами через `Pin.setPin`. Поэтому требование
// «PIN стенда = XDIARY_E2E_PIN» из сетапа убрано: оно валило весь прогон на свежем dev.
// Проверки PIN остались в спеках про PIN (`pin-first-login`, `change-pin`, `remove-pin`).
//
// DO-017 (P0): до 01.11 E2E гонялся против ПРОДА — `api.js` дефолтился на VPS-1, и UI-тесты
// писали тестовые записи/меняли PIN в реальном дневнике. Здесь жёсткий предохранитель:
// ни один прогон не стартует, если стенд не dev/локальный.
const { request } = require('@playwright/test');
const { API_BASE } = require('./api');

const ALLOWED_HOSTS = ['xdiary-vps2.taile0b6ee.ts.net', 'localhost', '127.0.0.1'];

module.exports = async function globalSetup() {
  try {
    assertNotProd();
    await checkApi();
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

// TE-033: вместо PIN проверяем, что гейтвей и авторизация живы.
// `/health` — стенд поднят; `/partners` без токена — 401, значит client-JWT обязателен
// и `seedSession` действительно нужен (а не «всё открыто»).
async function checkApi() {
  const ctx = await request.newContext();
  let health;
  let partners;
  try {
    health = await ctx.get(API_BASE + '/health');
    partners = await ctx.get(API_BASE + '/api/v1/partners');
  } finally {
    await ctx.dispose();
  }

  if (health.status() !== 200) {
    throw new Error('GET /health -> ' + health.status() + ' (стенд ' + API_BASE + ' не отвечает)');
  }
  if (partners.status() !== 401) {
    throw new Error(
      'GET /api/v1/partners без токена ожидали 401, получили ' + partners.status() +
      ' — проверь гейтвей/авторизацию на ' + API_BASE
    );
  }
  console.log('API жив: /health=200, /partners без токена=401 (client-JWT обязателен)');
}
