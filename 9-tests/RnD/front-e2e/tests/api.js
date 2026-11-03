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
    // TE-036: ::warning:: — гарантированная аннотация (docs/CI-RULES.md, @LM 22:35). ::notice:: в прогонах не наблюдался.
    console.log('::warning::' + JSON.stringify({ title: 'views' + (where ? ' (' + where + ')' : ''), views }));
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

/**
 * TE-044: уникальный телефон для прогона.
 *
 * Раньше спеки собирали номер как `'+7999777' + String(Date.now()).slice(-4)` — это
 * всего 10 000 вариантов, и при 1751 накопленных клиентах вероятность коллизии за
 * прогон ≈ `1 - exp(-1751/10000)` ≈ 16%. Дубликат телефона приводил к 500 от
 * `POST /api/v1/clients` (BE-046), UI не переходил к OTP, и это выглядело как
 * «флейк» на шаге «телефон → OTP».
 *
 * Здесь 10 млн вариантов (`+7999` + 7 цифр = 11 цифр, валидный РФ-номер);
 * при том же объёме данных вероятность коллизии ≈ 0,02%.
 */
function uniquePhone(prefix) {
  return (prefix || '+7999') + String(Date.now()).slice(-7);
}


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
    console.log('::warning::' + JSON.stringify({ title: 'storage snapshot' + (where ? ' (' + where + ')' : ''), storage: snap }));
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
    // TE-049 (P1, @TL 2026-11-10 04:45): канон E7/ADR-024 — «я» это ПРОФИЛЬ, а не партнёр.
    // Шаг аддитивный: профиль ставим ВСЕГДА (по нему онбординг 0.88 пускает в журнал),
    // а старый двухшаговый путь (партнёр + шим `link-partner`) пока оставляем — он нужен
    // образам до 0.88 и спекам, которые ожидают партнёра. Режим печатаем, чтобы
    // "зелёный на старом образе" не выглядел как "новый контракт проверен".
    const prof = await j(await fetch(base + "/api/v1/clients/me/profile", {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: "Bearer " + token },
      body: JSON.stringify({ displayName: "TE-SEED", gender: "female" }),
    }));
    const profileOk = prof.status >= 200 && prof.status < 300;
    const profileMode = profileOk ? "profile" : (prof.status === 404 || prof.status === 405 ? "pre-profile-image" : "profile-rejected:" + prof.status);
    // партнёр-«я» + привязка (легаси-путь: нужен до 0.88 и спекам, ожидающим партнёра)
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
    // TE-047-разбор (@DO/@FE 18:00): после BE-048 `GET /partners` фильтруется по `sub`.
    // Проверяем ЭТИМ ЖЕ токеном, что созданный партнёр виден клиенту — иначе следующий
    // прогон снова придётся разбирать «401 или пустой список?» по догадкам.
    const lp = await j(await fetch(base + '/api/v1/partners', {
      headers: { Authorization: 'Bearer ' + token },
    }));
    // TE-047 (P0, @TL 18:45): явная проверка auth-контура — «200 с токеном / 401 без».
    // Запрос БЕЗ Authorization: после BE-048 должен быть 401, и это видно по числу,
    // а не по `hidden`-экрану.
    const anon = await j(await fetch(base + '/api/v1/partners'));
    return {
      ok: true,
      clientId,
      profileMode,
      partnerId: p.data && p.data.id,
      partnerCreated: !!(p.data && p.data.id),
      hasRefresh: !!refreshToken,
      partnersStatus: lp.status,
      anonStatus: anon.status,
      partnersCount: Array.isArray(lp.data) ? lp.data.length : null,
      partnersOwn: Array.isArray(lp.data) ? lp.data.some((x) => x && x.id === (p.data && p.data.id)) : null,
    };
  }, { base: API_BASE, phone, otp: TEST_OTP, partnerName });

  if (!seeded.ok) {
    throw new Error('seedSession: шаг ' + seeded.step + ' вернул ' + seeded.status);
  }
  await page.goto('/');
  await page.waitForFunction(() => !!localStorage.getItem('xdiary.accessToken'), null, { timeout: 20000 });

  // TE-048-проба (@TL 2026-11-09 01:05): без этих трёх полей мы двое суток не могли
  // отличить «нет токена» от «экран блокировки остался из-за легаси-PIN».
  // Предикат pinVerified — ровно как во фронте (`app.js:154/246`): сначала `pin.verified`,
  // затем `pin.lastSuccess`; `pin.verified` фронт пока НИКОГДА не пишет, пишет `lastSuccess`
  // (app.js:507) — поэтому читаем оба, иначе поле всегда false и снова вводит в заблуждение.
  const screen = await page.evaluate(async () => {
    const out = { lockVisible: null, hasPin: null, pinVerified: null };
    try {
      const l = document.getElementById('view-lock');
      out.lockVisible = !!l && !l.classList.contains('hidden');
    } catch (e) {}
    try {
      out.hasPin = await window.Pin.hasPin();
    } catch (e) {}
    try {
      out.pinVerified = !!localStorage.getItem('xdiary.pin.verified') ||
                        !!localStorage.getItem('xdiary.pin.lastSuccess');
    } catch (e) {}
    return out;
  }).catch(() => null);

  if (seeded.ok) {
    const tokenAccepted = seeded.partnersStatus === 200;
    console.log('::warning::' + JSON.stringify({
      title: 'seedSession: auth-контур /partners + экран',
      profileMode: seeded.profileMode,          // TE-049: 'profile' (0.88+) | 'pre-profile-image' (старый образ)
      anonStatus: seeded.anonStatus,              // ожидание после BE-048: 401
      withTokenStatus: seeded.partnersStatus,     // ожидание: 200
      authEnforced: seeded.anonStatus === 401,
      tokenAccepted,
      partnersCount: seeded.partnersCount,
      partnerCreated: seeded.partnerCreated,
      partnersOwnVisible: seeded.partnersOwn,
      lockVisible: screen && screen.lockVisible,  // если true при tokenAccepted — это UI, не auth
      hasPin: screen && screen.hasPin,
      pinVerified: screen && screen.pinVerified,
    }));
    // Падаем только по делу: если client-JWT не принимается — это auth, а не «скрытый экран».
    if (!tokenAccepted) {
      throw new Error(
        'client-JWT не принят: GET /partners с токеном вернул ' + seeded.partnersStatus +
        ' (без токена: ' + seeded.anonStatus + '). Это auth-контур, а не hidden-экран (TE-047).'
      );
    }
    // TE-048: токен принят, но приложение осталось на замке — это дефект фронта (FE-032),
    // и в логе он должен быть назван, а не выглядеть как «фикстура не залогинилась».
    if (screen && screen.lockVisible && screen.hasPin && !screen.pinVerified) {
      console.log('::warning::' + JSON.stringify({
        title: 'seedSession: замок при валидном токене',
        verdict: 'UI: view-lock видим, токен принят (200), PIN есть, но не подтверждён — легаси-PIN (FE-032)',
        hasPin: screen.hasPin,
        pinVerified: screen.pinVerified,
        lockVisible: screen.lockVisible,
      }));
    }
  }

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
  const lock = page.locator('#view-lock');
  if (!(await lock.isVisible())) return;
  await page.fill('#pin-input', p);
  await page.click('#pin-submit');
  await waitToken(page);
  // TE-036 (@TL 03:30): токен к этому моменту УЖЕ есть (его не удаляли), поэтому waitToken
  // возвращается мгновенно, а приложение ещё на #view-lock → спек падал на «журнал hidden».
  // Ждём именно ИСЧЕЗНОВЕНИЯ замка, а не наличия токена.
  try {
    await lock.waitFor({ state: 'hidden', timeout: 15000 });
  } catch (e) {
    const views = await dumpViews(page, 'замок не ушёл после PIN').catch(() => null);
    throw new Error('PIN введён, но #view-lock не скрылся за 15с. views=' + JSON.stringify(views));
  }
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

// TE-036 (@TL 03:50): «шаг пройден» = СЛЕДУЮЩИЙ экран виден И нет тоста с ошибкой.
// Фронт показывает ошибки запросов только через `#toast` (`toast("Ошибка: …")` в app.js),
// поэтому без чтения тоста падение «#view-journal hidden» одинаково выглядит и при
// неотрисованном экране, и при `verify` 400 (гипотеза @DO про CLIENTS__TESTOTP на dev).
async function reportStep(page, where) {
  try {
    const toast = page.locator('#toast');
    const text = ((await toast.innerText().catch(() => '')) || '').trim();
    const state = await toast.evaluate((el) => ({
      hidden: el.classList.contains('hidden'),
      cls: String(el.className || ''),
    })).catch(() => null);
    if (text && state && !state.hidden) {
      // TE-046 (@TL 00:20): не всё, что попало в тост, — ошибка. На зелёном прогоне #12
      // «Это ты: <партнёр>» печаталось как `::warning::тост ошибки` и читалось как ложный
      // красный. Warning, который врёт, приучает игнорировать warning — поэтому уровень
      // выбираем по ПРИЗНАКУ ошибки, а формулировка нейтральная.
      //
      // Во фронте (`8-front/js/app.js:106`) `toast()` классов ошибки не ставит — только
      // `hidden`, поэтому класс смотрим на будущее, а основной признак — текст:
      // ошибочные тосты начинаются с «Ошибка:» (`app.js:279, 320, 327, 336, 341`).
      const isError = /error|danger|fail/i.test(state.cls) || /^(ошибк|error)/i.test(text);
      const line = 'тост (' + where + '): ' + text.slice(0, 200);
      console.log((isError ? '::warning::' : '::notice::') + line);
    } else {
      console.log('::notice::тост пуст (' + where + ')');
    }
  } catch (e) {}
  await dumpViews(page, where).catch(() => {});
  return true;
}

// TE-036 (@TL 03:40): падение шага должно НАЗЫВАТЬ шаг, а не «журнал hidden».
// `expect(locator).toBeVisible()` даёт общий таймаут, и по нему нельзя отличить «OTP не принят»
// (verify 400 → тост «Ошибка: …») от «экран не отрисовался». Здесь — имя шага + тост + экраны.
async function expectScreen(page, selector, where, timeout) {
  const ms = timeout || 20000;
  try {
    await page.locator(selector).waitFor({ state: 'visible', timeout: ms });
  } catch (e) {
    const toast = ((await page.locator('#toast').innerText().catch(() => '')) || '').trim();
    const views = await dumpViews(page, 'НЕ ДОШЛИ: ' + where).catch(() => null);
    throw new Error(
      'Шаг не пройден: ' + where + '. Ожидали видимый ' + selector + ' за ' + ms + 'мс. ' +
      'Тост=' + JSON.stringify(toast.slice(0, 300)) + ' views=' + JSON.stringify(views)
    );
  }
}

// TE-037 (@TL 07:06): белое пятно «уходит ли POST /link-partner из UI».
// В логе #17 сетевых вызовов нет, а `#onb-link-error` остался `hidden` — то есть
// невозможно отличить «запрос не отправлялся» от «отправлялся и вернул ошибку».
// Поэтому перехватываем ВСЁ, что матчит `/link-partner`: метод+URL, статус, тело (200 симв.).
function watchLinkPartner(page) {
  const events = [];
  const match = (url) => String(url || '').indexOf('/link-partner') >= 0;
  const onReq = (r) => {
    if (!match(r.url())) return;
    events.push('REQ ' + r.method() + ' ' + r.url() + ' body=' + String(r.postData() || '').slice(0, 200));
  };
  const onRes = (r) => {
    if (!match(r.url())) return;
    r.text().then((b) => {
      events.push('RES ' + r.status() + ' ' + r.url() + ' body=' + String(b).slice(0, 200));
    }).catch(() => {
      events.push('RES ' + r.status() + ' ' + r.url() + ' (тело не прочиталось)');
    });
  };
  const onFail = (r) => {
    if (!match(r.url())) return;
    const f = r.failure();
    events.push('FAIL ' + r.url() + ' ' + ((f && f.errorText) || 'failed'));
  };
  page.on('request', onReq);
  page.on('response', onRes);
  page.on('requestfailed', onFail);
  return {
    report(where) {
      page.off('request', onReq);
      page.off('response', onRes);
      page.off('requestfailed', onFail);
      const text = events.length ? events.join(' | ') : 'запросов не было';
      console.log('::warning::link-partner (' + (where || 'без метки') + '): ' + text);
      return events.slice();
    },
  };
}

module.exports = { API_BASE, PIN, TEST_OTP, uniquePhone, installBase, seedSession, waitToken, loginWithPin, unlockIfLocked, removeByName, dumpViews, snapshotStorage, reportStep, expectScreen, watchLinkPartner };
