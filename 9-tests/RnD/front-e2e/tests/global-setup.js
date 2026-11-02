// Глобальный сетап прогона.
//
// TE-035: dev передеплоивают, пока идёт E2E — тогда `/health` несколько секунд отдаёт
// 502/503, и прогон падал в globalSetup за 43 с, до спеков (#936). Такой прогон уже
// дважды приняли не за то: сначала за «новый баг», потом за «вытеснение из очереди».
// Теперь:
//   - `/health` ретраится до 60 с (шаг 2 с);
//   - 502/503/504 и сетевые ошибки помечаются как `[infra]` — это стенд, а не продукт;
//   - то же ожидание применяется к `/api/v1/clients`.
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
const WAIT_MS = 60_000; // TE-035: сколько ждём подъёма стенда после передеплоя
const STEP_MS = 2_000;
const INFRA_STATUSES = [0, 502, 503, 504]; // 0 — сетевая ошибка (ECONNREFUSED/timeout)

module.exports = async function globalSetup() {
  try {
    assertNotProd();
    await checkApi();
  } catch (e) {
    // ошибка глобального сетапа не попадает в onTestEnd — печатаем ::error:: сами,
    // иначе job падает «пустым» (как прогон 8efbc80: 56с, ни одной аннотации по тесту)
    const msg = String((e && e.message) || e);
    const title = msg.startsWith('[infra]') ? 'globalSetup [infra]' : 'globalSetup';
    console.log('::error::' + JSON.stringify({ title, error: msg }));
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

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Ждёт ответа от эндпоинта до WAIT_MS. Возвращает последний статус.
// Инфраструктурные статусы (502/503/504/сеть) означают «стенд перезапускается» — ждём.
// Любой другой неожиданный статус — повод прекратить ожидание и разобраться.
async function waitHealthy(ctx, path, acceptStatuses, what) {
  const deadline = Date.now() + WAIT_MS;
  let last = null;
  let lastErr = null;
  let attempts = 0;

  while (Date.now() < deadline) {
    attempts += 1;
    let res = null;
    try {
      res = await ctx.get(API_BASE + path);
    } catch (e) {
      last = 0;
      lastErr = String((e && e.message) || e);
    }
    if (res) {
      last = res.status();
      lastErr = null;
    }
    if (acceptStatuses.includes(last)) {
      console.log(what + ': ' + path + ' -> ' + last + ' (попыток: ' + attempts + ')');
      return last;
    }
    if (!INFRA_STATUSES.includes(last)) {
      throw new Error(
        what + ': ' + path + ' вернул ' + last + ' — это не инфраструктурное состояние, ' +
        'стенд поднят, но отвечает не так, как ожидалось'
      );
    }
    await sleep(STEP_MS);
  }

  throw new Error(
    '[infra] ' + what + ': ' + path + ' не поднялся за ' + (WAIT_MS / 1000) + ' с ' +
    '(последний статус: ' + last + (lastErr ? ', ошибка: ' + lastErr : '') + ', попыток: ' + attempts + '). ' +
    'Похоже, стенд перезапускается после деплоя — это не регрессия продукта.'
  );
}

// TE-035: вместо PIN проверяем, что гейтвей и авторизация живы, с ожиданием подъёма.
//   `/health` — стенд поднят (ждём до 60 с);
//   `/api/v1/clients` — маршрут шлюза доступен (400/401/405 — норма, главное не 502/503).
// 401 на `/partners` без токена подтверждает, что client-JWT обязателен и `seedSession` нужен.
async function checkApi() {
  const ctx = await request.newContext();
  try {
    await waitHealthy(ctx, '/health', [200], 'стенд');
    await waitHealthy(ctx, '/api/v1/clients', [200, 400, 401, 405], 'гейтвей /clients');
    const partners = await ctx.get(API_BASE + '/api/v1/partners');
    if (partners.status() !== 401) {
      throw new Error(
        'GET /api/v1/partners без токена ожидали 401, получили ' + partners.status() +
        ' — проверь гейтвей/авторизацию на ' + API_BASE
      );
    }
    console.log('API жив: /health=200, /clients доступен, /partners без токена=401 (client-JWT обязателен)');
  } finally {
    await ctx.dispose();
  }
}
