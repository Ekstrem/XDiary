// Хелпер настоящих E2E: живой API + реальный вход по PIN (как пользователь).
// Стенд берётся из XDIARY_E2E_BASE_URL (в CI — VPS-2), PIN — из XDIARY_E2E_PIN.
// ВАЖНО: дефолт — ТЕСТОВЫЙ стенд (VPS-2). Прод (VPS-1) — реальный дневник владельца;
// случайный локальный прогон без env не должен в него писать.
const API_BASE = (process.env.XDIARY_E2E_BASE_URL || 'https://xdiary-vps2.taile0b6ee.ts.net').replace(/\/+$/, '');
// Единый переключатель ожиданий UI: по умолчанию 30 с (как было в спеках числом), но его можно
// поднять/опустить ОДНОЙ переменной окружения, не правя 22 файла. Нужен, когда стенд медленный
// (`XDIARY_E2E_UI_WAIT=45000`) или наоборот быстрый (`=15000`), и чтобы `check_e2e_timeout_sums`
// можно было уложить в лимит теста без «слепого» сокращения таймаутов.
const UI_WAIT = Number(process.env.XDIARY_E2E_UI_WAIT || 30000);

const PIN = process.env.XDIARY_E2E_PIN || '1234';

// Ключ, под которым приложение хранит JWT. Источник правды — `8-front/js/api.js:8`
// (`var ACCESS_KEY = "xdiary.accessToken"`). Держим литерал ЗДЕСЬ, а не в спеках:
// 2026-11-05 `partners-read-parity` читала несуществующий `xdiary.token` и падала до своей
// проверки (@DO, REF=7bf8c3e9). Один литерал в тестовой обвязке = одна точка правки при смене ключа.
const ACCESS_KEY = 'xdiary.accessToken';

/** Прочитать JWT из хранилища страницы (после `waitToken` он гарантированно есть). */
async function readToken(page) {
  return page.evaluate((key) => {
    try {
      return localStorage.getItem(key) || '';
    } catch (e) {
      return '';
    }
  }, ACCESS_KEY);
}

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
  page.__xdiaryErrors = errors;
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
    // TE-107 (@TL 2026-11-13 17:30): `views:{}` читалось двояко — «документ не загрузился»
    // или «разметка экранов не создана». Контекст печатаем ВСЕГДА.
    const diag = await page.evaluate(() => {
      const nav = (performance.getEntriesByType && performance.getEntriesByType('navigation')[0]) || {};
      const ids = Array.from(document.querySelectorAll('[id]')).slice(0, 5).map((el) => el.id);
      return {
        url: location.href,
        status: typeof nav.responseStatus === 'number' ? nav.responseStatus : -1,
        readyState: document.readyState,
        htmlLen: document.body ? document.body.innerHTML.length : -1,
        ids,
        viewEls: document.querySelectorAll('[id^="view-"]').length,
      };
    }).catch(() => null);
    // TE-036: ::warning:: — гарантированная аннотация (docs/CI-RULES.md, @LM 22:35).
    const payload = Object.assign(
      { title: 'views' + (where ? ' (' + where + ')' : ''), views },
      diag || {},
      { viewsCount: Object.keys(views || {}).length }
    );
    console.log('::warning::' + JSON.stringify(payload));
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
    await page.waitForFunction((key) => !!localStorage.getItem(key), ACCESS_KEY, { timeout: 30000 });
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

// TE-058: `seedSessionOnce` создавал ПАРТНЁРА на каждый вход, то есть ~29 партнёров за прогон
// (`TE-SEED-<ts>`), а шаг очистки в CI удаляет только клиентов `+7999…`. Список партнёров рос от
// прогона к прогону; при 2 600+ карточках `#partners-list` доходил до 588 КБ (см. `DO-044`), то есть
// это прямое топливо для «не дождались» в хуках. Теперь партнёр создаётся ОДИН раз на процесс
// (workers: 1 — процесс один), дальше переиспользуется; создание партнёра как функции проверяют
// спеки `add-partner` / `partner-edit` / `wizard-record`, а не фикстура.
let SEED_PARTNER = null;

// TE-059: сессию (clientId + токены) кэшируем на процесс. Раньше каждый тест проходил полный
// онбординг (`POST /clients` → `verify` → `profile` → `partners` → `goto/`), это 2 загрузки
// страницы и 5 запросов НА КАЖДЫЙ из 29 тестов. Именно время входа тратило лимит теста на
// прогоне `18 failed` (REF 73f59ba7). Кэш переиспользуется, пока стенд принимает токен; при
// `401/404` он сбрасывается и сессия засевается заново — «удалили клиента» по-прежнему лечится.
let SEED_SESSION = null;

async function seedSessionOnce(page) {
  // 1) первым шагом installBase — ставит window.__XDIARY_E2E__ = true ДО загрузки приложения
  //    (иначе после FE-024 фронт игнорирует apiBase и уедет на прод)
  await installBase(page);
  // TE-059: сессия уже засеяна в этом процессе — не проходить онбординг заново.
  if (SEED_SESSION) {
    const cached = SEED_SESSION;
    await page.addInitScript((s) => {
      try {
        localStorage.setItem('xdiary.clientId', s.clientId);
        localStorage.setItem('xdiary.accessToken', s.token);
        if (s.refreshToken) localStorage.setItem('xdiary.refreshToken', s.refreshToken);
        localStorage.setItem('xdiary.apiBase', s.apiBase);
      } catch (e) {}
    }, cached);
    await page.goto('/');
    const alive = await page.evaluate(async (base) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      if (!t) return false;
      const r = await fetch(base + '/api/v1/clients/me', { headers: { Authorization: 'Bearer ' + t } });
      return r.status === 200;
    }, cached.apiBase).catch(() => false);
    // Кэш принят API — но приложение могло НЕ открыть журнал (например, онбординг ждёт профиль
    // в localStorage, а не только токен). Тогда «кэш жив» ещё не значит «вход состоялся»: без этой
    // проверки систематический дефект кэша давал бы массовое «Вход не завершился», а повтор входа
    // не срабатывал (он ждёт 401/404). Поэтому даём приложению 5 с и при неудаче честно засеваем заново.
    const opened = alive
      ? await page.locator('#view-journal').waitFor({ state: 'visible', timeout: 5000 })
        .then(() => true).catch(() => false)
      : false;
    if (alive && !opened) {
      console.log('::warning::' + JSON.stringify({
        title: 'seedSession: кэш принят API, но журнал не открылся — полный онбординг',
      }));
      SEED_SESSION = null;
    } else if (alive) {
      console.log('::notice::' + JSON.stringify({ title: 'seedSession', cached: true }));
      return {
        ok: true, cached: true, clientId: cached.clientId,
        token: cached.token, refreshToken: cached.refreshToken,
        profileMode: cached.profileMode || 'profile', shimUsed: false,
        partnerId: SEED_PARTNER && SEED_PARTNER.id, partnerCreated: false,
        hasRefresh: !!cached.refreshToken, partnersStatus: 200, anonStatus: 401,
        partnersCount: null, partnersOwn: null,
      };
    }
    console.log('::warning::' + JSON.stringify({ title: 'seedSession: кэш не принят, полный онбординг' }));
    SEED_SESSION = null;
  }
  await page.goto('/');

  const phone = '+7999' + String(Date.now()).slice(-7);
  const partnerName = (SEED_PARTNER && SEED_PARTNER.name) || 'TE-SEED-' + Date.now();
  const needPartner = !SEED_PARTNER;

  const expectedPartnerId = (SEED_PARTNER && SEED_PARTNER.id) || null;
  const seeded = await page.evaluate(async ({ base, phone, otp, partnerName, needPartner, expectedPartnerId }) => {
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
    // @FE 2026-11-12 09:25: в 0.88 онбординг = профиль → журнал; партнёр — ОТДЕЛЬНОЕ действие.
    // Партнёра создаём всегда: это функция спек add-partner/wizard-record, а не путь онбординга.
    // Шим `link-partner` зовём ТОЛЬКО на старом образе (profileMode !== "profile"):
    // на 0.88+ он не нужен, и `grep -rn "linkPartner" 8-front` в сдаче @FE = 0.
    const p = needPartner
      ? await j(await fetch(base + '/api/v1/partners', {
        method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
        body: JSON.stringify({ name: partnerName }),
      }))
      : { status: 0, data: null, reused: true };
    const shimUsed = profileMode !== "profile";
    if (shimUsed && p.data && p.data.id) {
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
    // TE-064: пробу «без токена  401» делаем из Node (ниже, после evaluate): проба из страницы
    // давала `console.error: Failed to load resource: 401`, и спека `trends` (требует «ошибок консоли нет»)
    // падала из-за самой фикстуры. Контракт проверяется там же, шума в консоли нет.
    const anon = { status: null };
    return {
      ok: true,
      clientId,
      token,
      refreshToken,
      profileMode,
      shimUsed,
      partnerId: p.data && p.data.id,
      partnerCreated: !!(p.data && p.data.id),
      hasRefresh: !!refreshToken,
      partnersStatus: lp.status,
      anonStatus: anon.status,
      partnersCount: Array.isArray(lp.data) ? lp.data.length : null,
      partnersOwn: Array.isArray(lp.data)
        ? lp.data.some((x) => x && x.id === ((p.data && p.data.id) || expectedPartnerId))
        : null,
    };
  }, { base: API_BASE, phone, otp: TEST_OTP, partnerName, needPartner, expectedPartnerId });
  // TE-064: проба «без токена -> 401» из Node: контракт проверяем, консоль страницы не засоряем.
  let anonStatus = null;
  try {
    const anonRes = await fetch(API_BASE + '/api/v1/partners', { method: 'GET' });
    anonStatus = anonRes.status;
  } catch (e) {
    anonStatus = 'error';
  }
  if (seeded && typeof seeded === 'object') seeded.anonStatus = anonStatus;
  if (needPartner && seeded && seeded.partnerId) {
    SEED_PARTNER = { id: seeded.partnerId, name: partnerName };
    // TE-118 (@DO `2026-11-10 16:21`): сидер не убирал за собой, и очистка `DO-044` (по телефонам) его не
    // трогает — партнёр оставался на стенде. Пишем сид в файл, чтобы `global-teardown` удалил его сам.
    try {
      const fs = require('fs');
      const os = require('os');
      const path = require('path');
      const marker = path.join(os.tmpdir(), 'xdiary-e2e-seed-' + API_BASE.replace(/[^a-z0-9]+/gi, '_') + '.json');
      fs.writeFileSync(marker, JSON.stringify({ base: API_BASE, partnerId: seeded.partnerId,
                                                partnerName, token: seeded.token || null }), 'utf8');
    } catch (e) { /* маркер — удобство, а не условие прогона */ }
  }

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
  SEED_SESSION = {
    clientId: seeded.clientId,
    token: seeded.token,
    refreshToken: seeded.refreshToken,
    apiBase: API_BASE,
    profileMode: seeded.profileMode,
  };
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

// `seedSession` вызывается ВНУТРИ обвязки (installBase → seedSessionOnce → seedSession), поэтому
// в спеках его нет: экспорт оставлен для будущих сценариев и для отладки вручную.
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
      await page.waitForTimeout(500 * attempt); // backoff: сетевой ретрай seedSession, не догадка о времени
    }
  }
  // Сетевые попытки исчерпаны: причину дописываем, чтобы в логе было видно ЧИСЛО попыток,
  // а не голое `Failed to fetch` (по такому сообщению нельзя отличить «сеть упала один раз»
  // от «стенд не отвечает все 3 попытки»).
  const lastMsg = String((lastErr && lastErr.message) || lastErr);
  throw new Error('seedSession: засев не удался после ' + SEED_ATTEMPTS + ' попыток (сеть): ' +
    lastMsg.slice(0, 200));
}

// TE-056 (@DO 2026-11-06 02:34): прогон `18 failed, 3 skipped, 8 passed` — падения в
// `Before Hooks` со `status: "timedOut"`, то есть «не дождались», а не «продукт ответил не то».
// Чтобы следующий прогон сам ответил, ГДЕ время, логируем длительность каждой фазы входа
// (`::notice::` видно в логе джобы) и при провале входа прикладываем экран/токен/ошибки.
// TE-057 (@DO 2026-11-06 02:34): прогон дал `18 failed / 8 passed`, падения — `timedOut`
// в хуках, то есть время входа прямо влияло на результат. Поэтому:
//   1) быстрый путь: если журнал уже открыт и токен на месте — `reload` и ожидание замка не нужны
//      (экономит 3–5 с на каждом тесте);
//   2) если сессию убили на стороне стенда (`401/404` на `/clients/me` или `/journal`), это НЕ дефект
//      продукта: параллельный прогон мог удалить клиента (`Очистить E2E-данные` убирает `+7999…`).
//      В этом случае засеваем сессию заново с НОВЫМ телефоном и повторяем вход ОДИН раз;
//   3) фазы входа печатаются в `::notice::login`, чтобы лог сам отвечал «где время».
// TE-061: быстрый путь входа ставит PIN, но НЕ разблокирует через UI, поэтому приложение не знает,
// что PIN «реально вводился» (`app.js:464` пишет `xdiary.pin.lastSuccess` только в `unlock()`).
// Без этой отметки следующий `page.reload()` ВНУТРИ спеки показал бы замок (PIN нового формата, не
// legacy), и спеки, которые после входа перезагружают страницу (`settings`, `wizard-record`,
// `edit-delete-record`, `stale-note`, `somatic-detail`), падали бы на ожидании журнала.
// Отмечаем так же, как это делает приложение: verifyPin → lastSuccess.
async function markPinVerified(page, pin) {
  try {
    return await page.evaluate(async (v) => {
      const ok = await window.Pin.verifyPin(v);
      if (ok) {
        try { localStorage.setItem('xdiary.pin.lastSuccess', '1'); } catch (e) {}
      }
      return !!ok;
    }, pin);
  } catch (e) {
    return false;
  }
}

async function loginWithPin(page, pin) {
  const t0 = Date.now();
  const p = pin || PIN;
  const phases = { seedMs: 0, pinMs: 0, reloadMs: 0, waitMs: 0, retries: 0, fastPath: false };

  // Быстрый путь: сессия уже жива и журнал открыт.
  const journalOpen = await page.locator('#view-journal').isVisible().catch(() => false);
  const tokenAlive = await page
    .evaluate((k) => !!localStorage.getItem(k), ACCESS_KEY)
    .catch(() => false);
  if (journalOpen && tokenAlive) {
    phases.fastPath = true;
    console.log('::notice::' + JSON.stringify({ title: 'login', elapsedMs: Date.now() - t0, fastPath: true }));
    return;
  }

  const seed = async () => {
    const t = Date.now();
    await seedSession(page);
    phases.seedMs += Date.now() - t;
  };

  const unlock = async () => {
    const t = Date.now();
    // TE-036: локальный PIN ставим ВСЕГДА (спеки зовут `loginWithPin(page)` без аргумента),
    // затем reload → init() видит PIN корректно → снимаем замок.
    try { await page.evaluate((v) => window.Pin.setPin(v), p); } catch (e) {}
    phases.pinMs += Date.now() - t;
    const t2 = Date.now();
    await page.reload();
    phases.reloadMs += Date.now() - t2;
    const t3 = Date.now();
    await Promise.race([
      page.locator('#view-lock').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
      page.locator('#view-journal').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
    ]);
    await unlockIfLocked(page, p);
    await expectScreen(page, '#view-journal', 'после входа по PIN', UI_WAIT);
    phases.waitMs += Date.now() - t3;
  };

  try {
    await seed();
    // TE-060: если после засева журнал уже открыт (кэш сессии — токен приняли, замка нет),
    // `reload` + ожидание замка (до 15 с) не нужны. PIN всё равно ставим: спеки PIN-контура
    // (change-pin/remove-pin/pin-first-login) делают свой `reload` и должны увидеть замок.
    const openAfterSeed = await page.locator('#view-journal').isVisible().catch(() => false);
    if (openAfterSeed) {
      const tSet = Date.now();
      try { await page.evaluate((v) => window.Pin.setPin(v), p); } catch (e) {}
      // и отметка «PIN вводился», иначе следующий reload внутри спеки покажет замок
      phases.pinVerified = await markPinVerified(page, p);
      phases.pinMs += Date.now() - tSet;
      phases.fastPath = true;
      console.log('::notice::' + JSON.stringify({
        title: 'login', fastPath: 'after-seed', elapsedMs: Date.now() - t0, pinMs: phases.pinMs,
      }));
      return;
    }
    await unlock();
  } catch (e) {
    const errs = (page.__xdiaryErrors || []).slice(-8);
    const killed = errs.some((x) => /http (401|404)/.test(x) && /clients\/me|journal/.test(x));
    if (!killed || phases.retries > 0) {
      const views = await dumpViews(page, 'вход не завершился').catch(() => null);
      throw new Error(
        'Вход не завершился (' + Math.round((Date.now() - t0) / 1000) + ' с: seed ' +
        Math.round(phases.seedMs / 1000) + ' c, PIN ' + Math.round(phases.pinMs / 1000) +
        ' c, reload ' + Math.round(phases.reloadMs / 1000) + ' c, ожидание ' +
        Math.round(phases.waitMs / 1000) + ' c, повторов ' + phases.retries + '). ' +
        'views=' + JSON.stringify(views) + ' ошибки=' + JSON.stringify(errs) +
        ' причина=' + String((e && e.message) || e).slice(0, 300)
      );
    }
    phases.retries += 1;
    console.log('::warning::' + JSON.stringify({
      title: 'login: сессия потеряна, повтор с новым телефоном',
      reason: errs.filter((x) => /http (401|404)/.test(x)).slice(-2),
    }));
    try {
      await seed();
      await unlock();
    } catch (e2) {
      const views2 = await dumpViews(page, 'повтор входа тоже не удался').catch(() => null);
      const errs2 = (page.__xdiaryErrors || []).slice(-8);
      throw new Error(
        'Вход не завершился даже после одного повтора (' + Math.round((Date.now() - t0) / 1000) +
        ' с, повторов ' + phases.retries + '). views=' + JSON.stringify(views2) +
        ' ошибки=' + JSON.stringify(errs2) + ' причина=' + String((e2 && e2.message) || e2).slice(0, 300)
      );
    }
  }

  console.log('::notice::' + JSON.stringify({
    title: 'login',
    elapsedMs: Date.now() - t0,
    seedMs: phases.seedMs,
    pinMs: phases.pinMs,
    reloadMs: phases.reloadMs,
    waitMs: phases.waitMs,
    retries: phases.retries,
  }));
}

// После reload локальный PIN уже стоит → приложение показывает экран блокировки.
// Разблокируем, чтобы попасть в журнал.
// Ждать, пока приложение отрисует ЛЮБОЙ известный экран.
// Зачем: после `page.reload()` спеки спали фиксированные 1.5 с — это догадка
// о времени загрузки (и лишние 1.5 с, и флейк на медленном стенде). Здесь ждём настоящий признак:
// один из экранов реально виден. В `8-front/js/app.js` нет объекта готовности (`window.__ready`),
// поэтому признак — сам экран, а не переменная.
async function waitAppReady(page, timeout = 20000) {
  await page.waitForFunction(
    () => {
      const ids = ['view-lock', 'view-journal', 'view-onboarding', 'view-splash'];
      return ids.some((id) => {
        const el = document.getElementById(id);
        if (!el) return false;
        const st = window.getComputedStyle(el);
        return st.display !== 'none' && st.visibility !== 'hidden' && el.offsetHeight > 0;
      });
    },
    null,
    { timeout }
  );
}

/**
 * TE-072: дождаться ЛЮБОГО из экранов, который виден.
 *
 * Почему не `page.locator('#view-lock').or(locator('#view-onboarding')).first()`: `.first()` упорядочивает
 * по DOM, а не по видимости. `#view-lock` в `index.html` идёт раньше остальных, поэтому `.first()` всегда
 * возвращал скрытый `#view-lock` и проверка падала, **хотя приложение уже показало журнал** — именно так
 * упали `pin-first-login` («новое устройство») и `pin-fallback` («чистый профиль») в прогоне #775:
 * снимок показывал журнал, а ошибка — `locator resolved to <section id="view-lock" class="view hidden">`.
 *
 * Здесь ждём через `waitForFunction` по фактической видимости и возвращаем id экрана — удобно для лога.
 */
async function waitAnyScreen(page, ids, timeout = 20000) {
  try {
    return await page.waitForFunction(
      (screenIds) => {
        for (const id of screenIds) {
          const el = document.getElementById(id);
          if (!el) continue;
          const st = window.getComputedStyle(el);
          if (st.display !== 'none' && st.visibility !== 'hidden' && el.offsetHeight > 0) return id;
        }
        return false;
      },
      ids,
      { timeout }
    ).then((h) => h.jsonValue());
  } catch (e) {
    const visible = await page.evaluate((screenIds) => screenIds.filter((id) => {
      const el = document.getElementById(id);
      if (!el) return false;
      const st = window.getComputedStyle(el);
      return st.display !== 'none' && st.visibility !== 'hidden' && el.offsetHeight > 0;
    }), ids).catch(() => []);
    throw new Error('ни один из экранов не показан за ' + timeout + ' мс: ' + ids.join(', ') +
      ' | видимые: ' + JSON.stringify(visible));
  }
}

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
        // TE-077: уборка не должна «падать», если объект уже удалён. В прогонах #773/#775 в лог шло
        // `cleanup partner: {"ok":false,"removed":false,"status":400}`, а 400 у `/partners/{id}/remove`
        // означает `Partner not found.` (PartnerHandlers.cs: `Fail("Partner not found.")`).
        // Причина — объект удалил другой прогон/предыдущий шаг, то есть цель уборки уже достигнута.
        // Теперь такой ответ считается успехом (`alreadyGone`), а реальные ошибки по-прежнему видны.
        if (!d.ok) {
          const body = await d.text().catch(() => '');
          if ((d.status === 400 || d.status === 404) && /not found/i.test(body)) {
            return { ok: true, removed: false, alreadyGone: true, status: d.status };
          }
          return { ok: false, removed: false, status: d.status, body: String(body).slice(0, 120) };
        }
        return { ok: true, removed: true, status: d.status };
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
    const text = ((await toast.innerText({ timeout: 1500 }).catch(() => '')) || '').trim();
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
      // TE-046 (уточнение): во фронте часть отказов начинается не с «Ошибка:», а с «Не удалось…»
      // (`app.js`: «Не удалось сохранить», «Не удалось загрузить», «Не удалось привязать»).
      // Узкий шаблон `/^(ошибк|error)/` помечал их как ::notice:: — то есть настоящая ошибка
      // шла НИЖЕ уровня, чем информационный тост. Признак ошибки ищем по всему тексту и по коду.
      const isError = /error|danger|fail/i.test(state.cls) ||
        /(ошибк|не удалось|error|fail|forbidden|unauthori[sz]ed|timeout|timed out|40[13]|50[0234])/i.test(text);
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
    const toast = ((await page.locator('#toast').innerText({ timeout: 1500 }).catch(() => '')) || '').trim();
    const views = await dumpViews(page, 'НЕ ДОШЛИ: ' + where).catch(() => null);
    // @TL 2026-11-02 17:28: при падении нужен снимок хранилища — иначе при 401 снова не видно,
    // что лежит в localStorage. Хук afterEach печатает его в stdout, но в аннотацию CI попадает
    // именно ТЕКСТ ошибки, поэтому дублируем здесь. snapshotStorage отдаёт только bool + длины.
    const storage = await snapshotStorage(page, 'шаг не пройден').catch(() => null);
    throw new Error(
      'Шаг не пройден: ' + where + '. Ожидали видимый ' + selector + ' за ' + ms + 'мс. ' +
      'Тост=' + JSON.stringify(toast.slice(0, 300)) + ' views=' + JSON.stringify(views) + ' storage=' + JSON.stringify(storage)
    );
  }
}

// TE-037 (@TL 07:06): белое пятно «уходит ли POST /link-partner из UI».
// В логе #17 сетевых вызовов нет, а `#onb-link-error` остался `hidden` — то есть
// невозможно отличить «запрос не отправлялся» от «отправлялся и вернул ошибку».
// Поэтому перехватываем ВСЁ, что матчит `/link-partner`: метод+URL, статус, тело (200 симв.).
//
// LEGACY (`ADR-021`/`TE-037`): после E7 (0.88) шага привязки в UI нет, ни одна спека эту функцию
// не вызывает — она оставлена для разбора старых образов (< 0.88) и это видно в квитанции
// `check_e2e_fixture_contract.py` строкой «не используют спеки». Удалять — только с решением @TL.
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

// TE-121 (@TL `2026-11-11 01:40`: диагностику из `wizard-record` «надо тиражировать»).
// Любой сбой шага превращается в сообщение с фактом: шаг, тост, последние API, экраны, localStorage.
// Это не замена утверждению — это адрес падения, чтобы разбор не начинался с «воспроизведи у себя».
async function stepGuard(page, label, fn, apiLog) {
  try {
    return await fn();
  } catch (e) {
    const views = await dumpViews(page, 'stepGuard').catch(() => null);
    const ls = await snapshotStorage(page, 'stepGuard').catch(() => null);
    const toast = await page.locator('#toast').innerText({ timeout: 1500 }).catch(() => '');
    const tail = Array.isArray(apiLog) ? apiLog.slice(-10).join(' | ') : '';
    throw new Error(
      'ШАГ: ' + label + ' ;; ТОСТ: ' + JSON.stringify((toast || '').slice(0, 200)) +
      ' ;; API: ' + tail + ' ;; ВИДЫ: ' + JSON.stringify(views) +
      ' ;; LS: ' + JSON.stringify(ls).slice(0, 400)
    );
  }
}

module.exports = {
  waitAnyScreen, UI_WAIT, waitAppReady, API_BASE, PIN, TEST_OTP, ACCESS_KEY, readToken, uniquePhone, installBase, seedSession, waitToken, loginWithPin, unlockIfLocked, removeByName, dumpViews, snapshotStorage, reportStep, expectScreen, watchLinkPartner, stepGuard };
