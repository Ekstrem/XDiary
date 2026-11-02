// Хелпер настоящих E2E: живой API + реальный вход по PIN (как пользователь).
// Стенд берётся из XDIARY_E2E_BASE_URL (в CI — VPS-2), PIN — из XDIARY_E2E_PIN.
// ВАЖНО: дефолт — ТЕСТОВЫЙ стенд (VPS-2). Прод (VPS-1) — реальный дневник владельца;
// случайный локальный прогон без env не должен в него писать.
const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps2.taile0b6ee.ts.net').replace(/\/+$/, '');
const PIN = process.env.XDIARY_E2E_PIN || '1234';

// FE-027: диагностика браузера. Приложение «остаётся на #view-lock» без видимой ошибки,
// поэтому ловим JS-исключения и console.error — иначе причина угадывается.
// Вызывается из installBase, то есть во всех спеках автоматически.
function installDiagnostics(page) {
  const errors = [];
  page.on('pageerror', (e) => {
    const msg = (e && (e.stack || e.message)) || String(e);
    errors.push('pageerror: ' + msg);
    if (errors.length <= 3) console.log('::error::' + JSON.stringify({ title: 'browser pageerror', error: msg }));
  });
  page.on('console', (m) => {
    try {
      if (m.type() !== 'error') return;
      const txt = m.text();
      errors.push('console.error: ' + txt);
      if (errors.length <= 3) console.log('::error::' + JSON.stringify({ title: 'browser console.error', error: txt }));
    } catch (e) {}
  });
  // TE-036: сетевые ответы со статусом >= 400 и упавшие запросы. Без них «селектор
  // не найден» не отличить от «сервер вернул 500» — а это разные адресаты (фикстура/продукт).
  page.on('response', (r) => {
    try {
      const st = r.status();
      if (st < 400) return;
      const line = 'http ' + st + ' ' + r.url();
      errors.push(line);
      if (errors.length <= 3) console.log('::error::' + JSON.stringify({ title: 'browser http error', error: line }));
    } catch (e) {}
  });
  page.on('requestfailed', (r) => {
    try {
      const f = r.failure();
      const line = 'requestfailed ' + r.url() + ' :: ' + ((f && f.errorText) || '');
      errors.push(line);
      if (errors.length <= 3) console.log('::error::' + JSON.stringify({ title: 'browser requestfailed', error: line }));
    } catch (e) {}
  });
  return errors;
}

// TE-036: снимок экранов `#view-*` с их классами. Из-за одного `hidden` уже полдня
// искали не там — фиксируем это в аннотации, а не в догадках.
async function dumpViews(page, where) {
  try {
    const views = await page.evaluate(() => {
      const out = {};
      document.querySelectorAll('[id^="view-"]').forEach((el) => {
        out[el.id] = el.classList.contains('hidden') ? 'hidden' : 'visible';
      });
      return out;
    });
    // TE-036: ::error:: (а не ::notice::) — только error/warning попадают в аннотации check-run.
    console.log('::error::' + JSON.stringify({ title: 'views' + (where ? ' (' + where + ')' : ''), views }));
    return views;
  } catch (e) {
    return null;
  }
}

// Приложение по умолчанию ходит на DEFAULT_BASE (VPS-1) — переключаем его на тестовый
// стенд, иначе UI-прогон будет наполовину против прода.
// FE-024 (P1, решение владельца): фронт перестаёт читать localStorage['xdiary.apiBase']
// без флага — ставим window.__XDIARY_E2E__ = true ДО загрузки приложения. Без этого
// после FE-024 тесты уедут на ПРОД (реальный дневник владельца) — недопустимо.
async function installBase(page) {
  installDiagnostics(page);
  await page.addInitScript((base) => {
    try { localStorage.setItem('xdiary.apiBase', base); } catch (e) {}
    window.__XDIARY_E2E__ = true;
  }, API_BASE);
}

async function waitToken(page) {
  try {
    await page.waitForFunction(() => !!localStorage.getItem('xdiary.accessToken'), null, { timeout: 30000 });
  } catch (e) {
    const failed = [];
    page.on('requestfailed', (r) => {
      const f = r.failure();
      failed.push(r.url() + ' :: ' + ((f && f.errorText) || 'failed'));
    });
    throw new Error('JWT не получен. Сетевые ошибки: ' + JSON.stringify(failed.slice(0, 5)));
  }
}

// Реальный вход: ставим локальный PIN (как в Настройках), перезагружаемся,
// на экране блокировки вводим PIN → приложение берёт JWT (ensureToken → login).
//
// TE-032: после 0.59 `unlock()` больше не запрашивает бэкенд-токен по PIN (PIN = локальный
// замок поверх client-JWT). Поэтому сессия засевается через онбординг: телефон → OTP →
// client-JWT → link-partner. Без этого все спеки уходили в онбординг и падали «JWT не получен».
const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';

// TE-032/FE-027: снимок хранилища БЕЗ раскрытия значений (только наличиe/длины).
// Причина #926 — HTTP 401 от `/clients/me`, pageerror = 0: по логам нельзя было понять,
// что в localStorage. Печатаем маскированно, чтобы при следующем 401 это было видно.
async function snapshotStorage(page, where) {
  try {
    const snap = await page.evaluate(() => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const c = localStorage.getItem('xdiary.clientId') || '';
      const l = localStorage.getItem('xdiary.legacy') || '';
      const b = localStorage.getItem('xdiary.apiBase') || '';
      return {
        token: !!t, tokenLen: t.length,
        clientId: !!c, clientIdLen: c.length,
        legacy: !!l,
        apiBaseLen: b.length,
      };
    });
    console.log('::notice::' + JSON.stringify({ title: 'storage snapshot' + (where ? ' (' + where + ')' : ''), storage: snap }));
    return snap;
  } catch (e) {
    return null;
  }
}

async function seedSessionOnce(page) {
  // 1) первым шагом installBase — ставит window.__XDIARY_E2E__ = true ДО загрузки приложения
  //    (иначе после FE-024 фронт игнорирует apiBase и уедет на прод)
  await installBase(page);
  await page.goto('/');

  const phone = '+7999' + String(Date.now()).slice(-7);
  const partnerName = 'TE-SEED-' + Date.now();

  const seeded = await page.evaluate(async ({ base, phone, otp, partnerName }) => {
    const j = async (r) => ({ status: r.status, data: await r.json().catch(() => null) });
    // клиент
    const c = await j(await fetch(base + '/api/v1/clients', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phone }),
    }));
    if (!c.data || !c.data.id) return { ok: false, step: 'clients', status: c.status };
    const clientId = c.data.id;
    // верификация OTP → client-JWT
    const v = await j(await fetch(base + '/api/v1/clients/' + clientId + '/verify', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: otp }),
    }));
    // TE-036 (@TL 01:45): образ 17:35 отдаёт accessToken; старые — token. Обратная совместимость.
    const token = (v.data && (v.data.accessToken || v.data.token)) || '';
    const refreshToken = (v.data && v.data.refreshToken) || '';
    if (!token) return { ok: false, step: 'verify', status: v.status };
    // партнёр-«я» + привязка
    const p = await j(await fetch(base + '/api/v1/partners', {
      method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
      body: JSON.stringify({ name: partnerName }),
    }));
    if (p.data && p.data.id) {
      await fetch(base + '/api/v1/clients/' + clientId + '/link-partner', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
        body: JSON.stringify({ partnerId: p.data.id }),
      });
    }
    // сессия в localStorage (как после онбординга)
    try {
      localStorage.setItem('xdiary.clientId', clientId);
      localStorage.setItem('xdiary.accessToken', token);
      if (refreshToken) localStorage.setItem('xdiary.refreshToken', refreshToken);
      localStorage.setItem('xdiary.apiBase', base);
    } catch (e) {}
    return { ok: true, clientId, partnerId: p.data && p.data.id, hasRefresh: !!refreshToken };
  }, { base: API_BASE, phone, otp: TEST_OTP, partnerName });

  if (!seeded.ok) {
    throw new Error('seedSession: шаг ' + seeded.step + ' вернул ' + seeded.status);
  }
  await page.goto('/');
  await page.waitForFunction(() => !!localStorage.getItem('xdiary.accessToken'), null, { timeout: 20000 });
  await snapshotStorage(page, 'после seedSession');
  await dumpViews(page, 'после seedSession');
  return seeded;
}

// TE-036: узкий retry ТОЛЬКО на сетевые ошибки. Обоснование — из разбора @DO:
// `page.evaluate: TypeError: Failed to fetch` приходит из `POST /clients/{id}/verify`
// внутри seedSession, ответа не было вообще (SW исключён: не-GET + другой origin).
// На HTTP-ошибки (401/500) retry НЕ делаем — иначе начнём прятать реальные дефекты.
const NET_ERROR_RE = /Failed to fetch|ERR_FAILED|net::ERR|ECONNRESET|ETIMEDOUT/i;
const SEED_ATTEMPTS = 3; // 1 + 2 повтора

async function seedSession(page) {
  let lastErr;
  for (let attempt = 1; attempt <= SEED_ATTEMPTS; attempt++) {
    try {
      return await seedSessionOnce(page);
    } catch (e) {
      lastErr = e;
      const msg = String((e && e.message) || e);
      const isNet = NET_ERROR_RE.test(msg);
      if (!isNet) {
        // HTTP-ошибка или логическая — не ретраим, пусть падает честно
        throw e;
      }
      if (attempt === SEED_ATTEMPTS) break;
      console.log('::notice::seedSession: retry ' + attempt + ' на шаге старт (сеть): ' + msg.slice(0, 120));
      await page.waitForTimeout(500 * attempt);
    }
  }
  throw lastErr;
}

async function loginWithPin(page, pin) {
  // TE-032: основной путь — seedSession (client-JWT). Локальный PIN остаётся вторым слоем
  // для спеков, которые проверяют PIN-экран (remove-pin, change-pin, pin-first-login).
  await seedSession(page);
  const p = pin || PIN;
  // TE-036: ставим локальный PIN ВСЕГДА, а не только когда передан аргумент.
  // Было `if (pin) { setPin(pin) }` — а спеки зовут `loginWithPin(page)` без аргумента,
  // поэтому PIN не создавался, `unlockIfLocked` вводил 1234 в пустой замок,
  // `Pin.verifyPin` возвращал false → `showPinError()` → приложение оставалось на `#view-lock`.
  // Именно это давало `{"view-lock":true,…}` и `hasPinLocal = false`.
  try { await page.evaluate((v) => window.Pin.setPin(v), p); } catch (e) {}
  // TE-036 (@TL 20:25): setPin идёт ПОСЛЕ goto('/') из seedSession, поэтому приложение
  // уже показало #view-lock без PIN, а потом «сломалось» логикой загрузки — отсюда
  // 90-секундное залипание в #976. Правильный порядок:
  //   setPin → reload (init() видит PIN корректно) → дождаться ЛЮБОГО из двух состояний
  //   (#view-lock или #view-journal) → и только потом unlockIfLocked снимает замок.
  await page.reload();
  await Promise.race([
    page.locator('#view-lock').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
    page.locator('#view-journal').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
  ]);
  // TE-036: `seedSession` + локальный PIN = законный замок при загрузке. Спеки, которые
  // не проверяют сам замок, обязаны его снять, иначе `#view-journal`/`#view-catalog` hidden
  // 20–90 с. Делаем это здесь, а не в каждом спеке: забыть вызов — типовая ошибка
  // (именно так падали `catalog`, `csv`, `remove-pin`, `wizard-record`).
  await unlockIfLocked(page, p);
}

// После reload локальный PIN уже стоит → приложение показывает экран блокировки.
// Разблокируем, чтобы попасть в журнал.
async function unlockIfLocked(page, pin) {
  const p = pin || PIN;
  if (!(await page.locator('#view-lock').isVisible())) return;
  await page.fill('#pin-input', p);
  await page.click('#pin-submit');
  await waitToken(page);
}

async function removeByName(page, kind, name) {
  try {
    return await page.evaluate(async ({ base, kind, name }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const headers = t ? { Authorization: 'Bearer ' + t } : {};
      const listPath = kind === 'partner' ? '/api/v1/partners' : '/api/v1/catalog';
      const r = await fetch(base + listPath, { headers });
      if (!r.ok) return { ok: false, listStatus: r.status };
      const list = await r.json();
      const it = (list || []).find((x) => x.name === name);
      if (!it) return { ok: true, removed: false };
      const rem = kind === 'partner'
        ? base + '/api/v1/partners/' + it.id + '/remove'
        : base + '/api/v1/catalog/' + it.id + '/remove';
      const d = await fetch(rem, { method: 'POST', headers });
      return { ok: d.ok, removed: d.ok, status: d.status };
    }, { base: API_BASE, kind, name });
  } catch (e) {
    return { ok: false, error: String(e && e.message) };
  }
}

// TE-036 (@TL 20:20): снимок экранов при ЛЮБОМ падении, а не только в seedSession/onboarding.
// Причина: в #976 было 7 таймаутов по 90 с (catalog x3, csv, partner-edit, settings, trends)
// и мы не знали, какой экран висит. Один снимок в момент падения отвечает на все сразу.
// Регистрируется здесь, потому что каждый спек делает require('./api').
try {
  const { test } = require('@playwright/test');
  test.afterEach(async ({ page }, testInfo) => {
    const failed = testInfo.status !== testInfo.expectedStatus;
    if (!failed) return;
    await dumpViews(page, 'ПОСЛЕ ПАДЕНИЯ: ' + testInfo.title).catch(() => {});
    await snapshotStorage(page, 'ПОСЛЕ ПАДЕНИЯ').catch(() => {});
  });
} catch (e) {
  // если api.js подключён вне Playwright-контекста — просто не регистрируем хук
}

module.exports = { API_BASE, PIN, TEST_OTP, installBase, seedSession, waitToken, loginWithPin, unlockIfLocked, removeByName, dumpViews, snapshotStorage };
