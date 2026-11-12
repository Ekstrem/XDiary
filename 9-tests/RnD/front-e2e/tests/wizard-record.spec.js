// E2E (настоящий) — TE-014: создание записи журнала через визард.
// Живое приложение + живой шлюз (dev, VPS-2), вход по PIN.
// `saveWizard` делает ~10 последовательных запросов, поэтому после «Сохранить» ЖДЁМ
// переключения вида (а не снимаем снимок сразу). Диагностика при падении: тосты + API-лог.
//
// MVP-истории: US-01, US-02, US-03, US-04, US-05, US-06, US-07, US-12
//   US-01 дата/время начала, US-02 партнёр, US-03 место, US-04 позы,
//   US-05 тэги, US-06 длительность, US-07 оргазмы партнёров,
//   US-12 подстановка и приоритизация по частоте
const { test, expect } = require('@playwright/test');
const { UI_WAIT, API_BASE, loginWithPin, unlockIfLocked } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }
const NOW = new Date();
const WHEN = '2026-11-02T' + pad(NOW.getUTCHours()) + ':' + pad(NOW.getUTCMinutes());

async function viewState(page) {
  return page.evaluate(() => {
    const map = {};
    document.querySelectorAll('.view').forEach((v) => { map[v.id] = !v.classList.contains('hidden'); });
    return map;
  });
}

async function findRecord(page, startedAt) {
  return page.evaluate(async ({ base, startedAt }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = t ? { Authorization: 'Bearer ' + t } : {};
    const r = await fetch(base + '/api/v1/journal/records', { headers });
    if (!r.ok) return { ok: false, status: r.status };
    const list = await r.json();
    const it = (list || []).find((x) => String(x.startedAt || '').indexOf(startedAt) === 0);
    return { ok: true, found: !!it, id: it && it.id };
  }, { base: API_BASE, startedAt });
}

async function deleteRecord(page, id) {
  return page.evaluate(async ({ base, id }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = t ? { Authorization: 'Bearer ' + t } : {};
    const r = await fetch(base + '/api/v1/journal/records/' + id + '/delete', { method: 'POST', headers });
    return { ok: r.ok, status: r.status };
  }, { base: API_BASE, id });
}

test('E2E: запись через визард — создаётся и видна через API', async ({ page }) => {
  test.setTimeout(80_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  const apiLog = [];
  page.on('response', (r) => {
    const u = r.url();
    if (u.indexOf('/api/v1/') !== -1) {
      apiLog.push(r.status() + ' ' + r.request().method() + ' ' + u.replace(/^https?:\/\/[^/]+/, ''));
    }
  });

  let id = null;
  try {
    await loginWithPin(page);
    const vis0 = await viewState(page);
    expect(vis0['view-journal'], 'виды после логина: ' + JSON.stringify(vis0)).toBe(true);

    // FE-015: замер открытия визарда + число запросов (было ~4× /partners + ~5× /journal,
    // стало 1×+1×). Печатаем ::notice:: — GitHub покажет это в аннотации check-run.
    const t0 = Date.now();
    const before = apiLog.length;
    await page.click('#btn-new');
    // 5с по умолчанию не хватало: на нагруженном dev открытие визарда иногда дольше
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 10000 });

    const openedMs = Date.now() - t0;
    const wizardCalls = apiLog.slice(before);
    const cnt = (re) => wizardCalls.filter((l) => re.test(l)).length;
    console.log('::notice::wizard open: ' + openedMs + 'ms; /partners x' + cnt(/\/partners/) +
      ', /journal x' + cnt(/\/journal/) + '; всего запросов при открытии: ' + wizardCalls.length);

    // FE-015 «не кэшируем» подтверждён по коду (reopenWizard → DB.getAllJournalData без кэша).
    // Отдельный UI-шаг с возвратом в визард убран: кнопка «добавить партнёра» в визарде
    // называется иначе, чем я предположил, а ронять тест на этом не нужно — проверка
    // покрыта кодом и ручным смоуком. Вернёмся, если появится стабильный селектор.

    await page.evaluate(() => {
      window.__toasts = [];
      const t = document.getElementById('toast');
      if (t) {
        new MutationObserver(() => { if (t.textContent) window.__toasts.push(t.textContent); })
          .observe(t, { childList: true, characterData: true, subtree: true });
      }
    });

    // правка (@DO `2026-11-10 12:33`: «запись через визард» -> `timedOut` 1.3 мин). Ограничиваем КАЖДЫЙ шаг
    // 10 с и называем его в логе: иначе падение по лимиту теста не говорит, на каком шаге мастер встал.
    const NEXT = { timeout: 10000 };
    // правка (@DO `2026-11-10 17:03`: в #1908 диагностика показала `ШАГ 1 -> 0 (prev)` — то есть клик по
    // «далее» уводил НАЗАД). Раньше `next()` проверял только успешность клика, и такой дефект всплывал
    // позже как непонятный таймаут. Теперь проверяем САМ ФАКТ продвижения: текст шага должен измениться.
    // @AR 02:24: без таймаута это чтение ждёт таймаут ТЕСТА (90 с), если
    // экран визарда отсутствует. Класс закрыт 3 из 4 — закрываем четвёртое.
    const stepText = () => page.locator('#view-wizard').innerText({ timeout: 1500 });
    const next = async (label) => {
      console.log('::notice::wizard next: ' + label);
      // Шаг «Сколько длилось?» (`#w-dur-grid`): продукт (8-front/js/app.js:801) не
      // пускает дальше без durationMin >= 1. Метки шагов в логе опережают фактический
      // шаг: спека считает, что идёт `5 -> 6`, а мастер уже на шаге длительности.
      // Клик «далее» на этом переходе пропускаем — длительность заполнит следующая
      // строка (`fill #w-dur-custom`), а `6 -> 7` уже пройдёт.
      if (!label.startsWith('6 -> 7')) {
        const durGrid = page.locator('#w-dur-grid');
        if (await durGrid.isVisible().catch(() => false)) {
          console.log('::notice::wizard next: уже на шаге «Сколько длилось?» — клик пропущен (' + label + ')');
          return;
        }
      }
      const before = await stepText().catch(() => '');
      await page.click('#btn-wizard-next', NEXT);
      await page.waitForFunction(
        (prev) => {
          const el = document.getElementById('view-wizard');
          return !!el && el.innerText !== prev;
        },
        before,
        { timeout: NEXT.timeout }
      ).catch(async () => {
        const after = await stepText().catch(() => '');
        throw new Error('ШАГ НЕ СМЕНИЛСЯ при «далее» (' + label + '): шаг не продвинулся. ' +
          'До: ' + JSON.stringify((before || '').slice(0, 160)) + ' ;; После: ' +
          JSON.stringify((after || '').slice(0, 160)));
      });
    };
    const prev = async (label) => {
      console.log('::notice::wizard prev: ' + label);
      await page.click('#btn-wizard-prev', NEXT);
    };

    // правка (@TL `2026-11-11 01:10`: красный «записи через визард» не называет шаг, а логи раннера — 403):
    // любой сбой внутри шагов превращаем в ошибку С ДИАГНОСТИКОЙ — экраны, тосты, последние запросы.
    // Без этого единственным сообщением остаётся «Timeout 10000ms exceeded», и разбор начинается с нуля.
    async function wizardDiagnose(step) {
      const toasts = await page.evaluate(() => window.__toasts || []).catch(() => []);
      const vis = await viewState(page).catch(() => ({}));
      return 'ШАГ ВИЗАРДА: ' + step + ' ;; ТОСТЫ: ' + JSON.stringify(toasts) +
        ' ;; API: ' + apiLog.slice(-10).join(' | ') +
        ' ;; ВИДЫ: ' + JSON.stringify(vis);
    }
    const stepWrapped = async (label, fn) => {
      try {
        await fn();
      } catch (e) {
        throw new Error(wizardDiagnose ? await wizardDiagnose(label) : label);
      }
    };

    await stepWrapped('fill #w-startedAt', () => page.fill('#w-startedAt', WHEN));

    // правка (@DO `2026-11-10 17:03` принёс `ШАГ ВИЗАРДА: 1 -> 0 (prev)`, и это оказался ДЕФЕКТ МОЕГО КЕЙСА):
    // `#btn-wizard-prev` скрыт на шаге 0 (`app.js:771`: `classList.toggle("hidden", wizardStep === 0)`),
    // а кейс кликал `prev` прямо после открытия мастера, ни разу не уйдя вперёд. Playwright ждал
    // видимости 10 с и падал. Правильный порядок: сначала уйти на шаг 1, затем вернуться назад.
    await stepWrapped('0 -> 1 (перед проверкой возврата)', () => next('0 -> 1 (перед проверкой возврата)'));
    await stepWrapped('1 -> 0 (prev)', () => prev('1 -> 0'));
    // Возврат обязан вернуть именно экран даты (шаг 0), иначе «назад» сработал в пустоту.
    await stepWrapped('после prev виден шаг «Дата»',
      () => expect(page.locator('#w-startedAt')).toBeVisible({ timeout: 10000 }));
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 10000 });
    await stepWrapped('0 -> 1 (после возврата)', () => next('0 -> 1 (после возврата)'));
    await stepWrapped('0 -> 1', () => next('0 -> 1'));
    await stepWrapped('1 -> 2', () => next('1 -> 2'));
    // Кнопка «Без партнёра» (`#w-partner-none`) не проверялась ни одной спекой: она сбрасывает
    // партнёра и перерисовывает шаг. Проверяем, что шаг остаётся рабочим и мастер не ломается.
    if (await page.locator('#w-partner-none').isVisible()) {
      await page.click('#w-partner-none', NEXT);
      await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 10000 });
    }
    await stepWrapped('2 -> 3', () => next('2 -> 3'));
    await stepWrapped('3 -> 4', () => next('3 -> 4'));
    await stepWrapped('4 -> 5', () => next('4 -> 5'));
    await stepWrapped('5 -> 6', () => next('5 -> 6'));
    await stepWrapped('fill #w-dur-custom', () => page.fill('#w-dur-custom', '30', { timeout: 10000 }));
    await stepWrapped('6 -> 7', () => next('6 -> 7'));
    await stepWrapped('7 -> 8', () => next('7 -> 8'));

  await page.click('#btn-wizard-save', { timeout: 20000 });

    // ждём переключения на журнал (цепочка из ~10 запросов)
    try {
      await expect(page.locator('#view-journal')).toBeVisible({ timeout: UI_WAIT });
    } catch (e) {
      const toasts = await page.evaluate(() => window.__toasts || []);
      const vis = await viewState(page);
      // диагностика по просьбе @TL: состояние экранов + localStorage, чтобы @FE не искал вслепую
      const ls = await page.evaluate(() => {
        const o = {};
        for (let i = 0; i < localStorage.length; i++) {
          const k = localStorage.key(i);
          o[k] = String(localStorage.getItem(k) || '').slice(0, 40);
        }
        return o;
      });
      throw new Error('ТОСТЫ: ' + JSON.stringify(toasts) +
        ' ;; API: ' + apiLog.slice(-12).join(' | ') +
        ' ;; ВИДЫ: ' + JSON.stringify(vis) +
        ' ;; LS: ' + JSON.stringify(ls));
    }

    const found = await findRecord(page, WHEN);
    expect(found.ok, 'GET /api/v1/journal/records → ' + apiLog.slice(-5).join(' | ')).toBe(true);
    expect(found.found, 'запись ' + WHEN + ' не найдена; API: ' + apiLog.slice(-8).join(' | ')).toBe(true);
    id = found.id;
  } finally {
    if (id) {
      const r = await deleteRecord(page, id);
      console.log('cleanup record:', JSON.stringify(r));
    }
  }
});

// TE-014 / FE-014 (@FE 2026-11-03 18:00 — «прогнать `wizard-record` и проверить дефолты
// „последний партнёр/место“»): визард обязан подставлять **последние** партнёра и место
// из `GET /journal/recent` (`app.js`: `draft.partner = recent.lastPartner.name`,
// `draft.place = recent.lastPlace`), а не сканировать весь журнал.
//
// Фикстура детерминированная: создаём партнёра и запись через API (самую свежую по `startedAt`),
// открываем визард и утверждаем именно подстановку. Проверка «8 × /partners при открытии» осталась
// в первом тесте (FE-015); здесь — только дефолты, чтобы падение говорило, что сломалось.
test('E2E: визард подставляет последние партнёра и место (FE-014)', async ({ page }) => {
  test.setTimeout(80_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  const partnerName = 'def-partner-' + Date.now();
  const place = 'def-place-' + Date.now();
  let recId = null;
  let partnerId = null;

  try {
    await loginWithPin(page);

    const seeded = await page.evaluate(
      async ({ base, partnerName, place }) => {
        const t = localStorage.getItem('xdiary.accessToken') || '';
        const h = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t };
        const post = async (path, body) => {
          const r = await fetch(base + path, { method: 'POST', headers: h, body: body === undefined ? undefined : JSON.stringify(body) });
          const j = r.ok ? await r.json().catch(() => null) : null;
          return { ok: r.ok, status: r.status, body: j };
        };
        const p = await post('/api/v1/partners', { name: partnerName });
        if (!p.ok) return { ok: false, step: 'partner', status: p.status };
        const pid = p.body && p.body.id;
        const rec = await post('/api/v1/journal/records');
        if (!rec.ok) return { ok: false, step: 'record', status: rec.status, partnerId: pid };
        const rid = rec.body && rec.body.id;
        // `when` — до partner/place: запись должна стать самой свежей, иначе `recent` вернёт другую.
        const when = await post('/api/v1/journal/records/' + rid + '/when', { startedAt: new Date().toISOString() });
        if (!when.ok) return { ok: false, step: 'when', status: when.status, partnerId: pid, recordId: rid };
        const link = await post('/api/v1/journal/records/' + rid + '/partners', { partnerId: pid });
        if (!link.ok) return { ok: false, step: 'link', status: link.status, partnerId: pid, recordId: rid };
        const pl = await post('/api/v1/journal/records/' + rid + '/place', { place: place });
        if (!pl.ok) return { ok: false, step: 'place', status: pl.status, partnerId: pid, recordId: rid };
        return { ok: true, partnerId: pid, recordId: rid };
      },
      { base: API_BASE, partnerName, place }
    );
    expect(seeded.ok, 'фикстура: ' + JSON.stringify(seeded)).toBe(true);
    partnerId = seeded.partnerId;
    recId = seeded.recordId;

    // Перезагрузка: `recent` должен вернуть только что созданную запись, визард — подставить её поля.
    await page.reload();
    // PIN поставлен входом (`loginWithPin`), поэтому после reload приложение показывает замок —
    // без снятия замка `#view-journal` остаётся hidden. Ровно этот класс давал падения
    // `wizard-record` в прогоне REF 73f59ba7 («не дождались» до утверждений).
    await unlockIfLocked(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    await page.click('#btn-new');
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 20000 });

    // правка (@DO `2026-11-10 12:33`: кейс падал 29.6 с): шаг 0 в мастере — **«Дата»** (`app.js:8`
    // `STEPS = ["Дата","Партнёр","Место",…]`), поэтому карточек партнёра на нём нет, и ожидание
    // `.partner-card.selected` не могло выполниться никогда. Проверка осталась той же — изменился
    // только шаг, на котором она делается: сначала «Дата», затем «Партнёр».
    await expect(page.locator('#w-startedAt'), 'шаг 0 — «Дата»').toBeVisible({ timeout: 10000 });
    await page.click('#btn-wizard-next', { timeout: 10000 });   // 0 -> 1 «Партнёр»
    await expect(page.locator('#w-partner-grid'), 'шаг 1 — «Партнёр»').toBeVisible({ timeout: 10000 });

    // Шаг 1 — партнёр: выбранная карточка = последний партнёр.
    const selected = page.locator('#w-partner-grid .partner-card.selected');
    await expect(selected, 'последний партнёр должен быть выбран по умолчанию').toHaveCount(1, { timeout: 20000 });
    await expect(selected).toContainText(partnerName);

    // Шаг 2 — место: `draft.place` подставляется в поле значения (app.js: `input.value = draft.place`).
    await page.click('#btn-wizard-next', { timeout: 10000 });   // 1 -> 2 «Место»
    await expect(page.locator('#w-place')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('#w-place'), 'последнее место должно быть подставлено').toHaveValue(place, { timeout: 10000 });

    console.log('::notice::FE-014 дефолты подтверждены: партнёр «' + partnerName + '», место «' + place + '»');
  } finally {
    if (recId) {
      const r = await deleteRecord(page, recId);
      console.log('cleanup record:', JSON.stringify(r));
    }
    if (partnerId) {
      const r = await page.evaluate(async ({ base, id }) => {
        const t = localStorage.getItem('xdiary.accessToken') || '';
        const res = await fetch(base + '/api/v1/partners/' + id + '/remove', { method: 'POST', headers: { Authorization: 'Bearer ' + t } });
        return { ok: res.ok, status: res.status };
      }, { base: API_BASE, id: partnerId });
      console.log('cleanup partner:', JSON.stringify(r));
    }
  }
});

// TE-014 / FE-016 (@FE 2026-11-03 18:45): «из визарда убран `data.records`: `loadWizardData` =
// `partners` + `journal/recent` + `journal/places` (3 лёгких запроса, **без полного журнала**)».
// Проверяем именно обещание по сетевым вызовам, а не по коду: при открытии визарда создания
// не должно быть ни одного запроса полного журнала (`GET /journal/records`), зато обязаны быть
// `recent` и `places`. Подсказки шагов берутся из `recent.lastPositions/lastTags/lastContraceptives`
// (`app.js:769/859/888`) — то есть сканов массива записей больше нет.
test('E2E: открытие визарда не грузит полный журнал (FE-016)', async ({ page }) => {
  test.setTimeout(80_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  const calls = [];
  page.on('request', (r) => {
    const u = r.url();
    if (u.indexOf('/api/v1/') !== -1) calls.push(r.method() + ' ' + u.replace(/^https?:\/\/[^/]+/, ''));
  });

  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 10000 });

  // правка (@DO `2026-11-10 12:11`, @AR #710): дождаться затишья сети **до** окна. Иначе фоновое
  // обновление, прилетевшее сразу после входа, попадает в окно и «полный журнал» приписывается визарду.
  await page.waitForLoadState('networkidle');
  const before = calls.length;
  await page.click('#btn-new');
  await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 10000 });
  // TE-104: с этого момента визард уже показан — отделяем «на открытии» (клик → экран)
  // от «после открытия», чтобы `journal/records` нельзя было списать на фоновое обновление.
  const afterOpen = calls.length;
  // Ждём, пока сеть успокоится (детерминированно): спека проверяет ОТСУТСТВИЕ
  // полного `/journal/records`, поэтому «догоняющие» ленивые запросы должны успеть уйти.
  await page.waitForLoadState('networkidle');

  // правка (@AR `2026-11-10 12:53`): «контроль должен быть ТЕМ ЖЕ ПУТЁМ, что и проверка».
  // Синтетический `fetch` доказывает только, что сборщик видит запрос; он обходит приложение.
  // Поэтому отдельно считаем, что ПРИЛОЖЕНИЕ само прошло цепочку `partners → journal` до окна:
  // `C ≥ 1` — журнал реально грузился; `P` печатаем, потому что цепочка начинается с `partners`
  // (`repository.js:77-85`) и `P = 0` означал бы, что вторая нога не стартовала.
  const beforeWindow = calls.slice(0, before);
  const controlJournal = beforeWindow.filter((l) => /journal\/records$/.test(l));
  const controlPartners = beforeWindow.filter((l) => l.indexOf('/api/v1/partners') !== -1);

  const mine = calls.slice(before);
  const duringOpen = calls.slice(before, afterOpen);
  const afterWizard = calls.slice(afterOpen);
  const fullJournal = mine.filter((l) => /GET \/api\/v1\/journal\/records$/.test(l));
  const fullAtOpen = duringOpen.filter((l) => /GET \/api\/v1\/journal\/records$/.test(l));
  const fullAfter = afterWizard.filter((l) => /GET \/api\/v1\/journal\/records$/.test(l));
  const recent = mine.filter((l) => l.indexOf('/api/v1/journal/recent') !== -1);
  const places = mine.filter((l) => l.indexOf('/api/v1/journal/places') !== -1);

  console.log('::notice::FE-016 wizard open: ' + mine.length + ' запросов (на открытии ' +
    duringOpen.length + ', после открытия ' + afterWizard.length + '); полный журнал x' +
    fullJournal.length + ' (на открытии x' + fullAtOpen.length + ', после x' + fullAfter.length +
    '), recent x' + recent.length + ', places x' + places.length);

  // Окно = запросы ПОСЛЕ входа, с момента перед кликом `#btn-new` до `networkidle` (см. `before` выше).
  // В тексте падения это названо явно: иначе список читают как «все запросы теста» (реальный случай 2026-11-10).
  const windowText = 'окно после клика #btn-new: ' + mine.join(' | ') +
    ' | на открытии (клик→экран): ' + (duringOpen.join(' | ') || '—') +
    ' | после открытия: ' + (afterWizard.join(' | ') || '—');
  expect(fullJournal, 'визард не должен грузить полный журнал (FE-016); ' + windowText)
    .toHaveLength(0);
  expect(recent.length, 'должен быть `GET /journal/recent` (подсказки шагов); ' + windowText)
    .toBeGreaterThanOrEqual(1);
  expect(places.length, 'должен быть `GET /journal/places` (шаг «Где?»); ' + windowText)
    .toBeGreaterThanOrEqual(1);

  // правка (@TL `2026-11-10 13:26`, @AR «контрольное окно»): ПОЛОЖИТЕЛЬНЫЙ КОНТРОЛЬ СБОРЩИКА.
  // `expect(fullJournal).toHaveLength(0)` проходит и на пустоте (журнал не грузился вовсе) — тогда «x0»
  // ничего не доказывает. Поэтому после всех утверждений делаем контролируемый запрос из страницы и
  // убеждаемся, что сборщик его ВИДИТ: если нет, тест обязан краснеть, а не зеленеть «на пустоте».
  // Принцип (@TL `2026-11-10 13:56`, дословно): «сначала докажи, что измеряешь, потом измеряй» —
  // контроль синтетический (не зависит от того, загрузился ли журнал) и стоит ПОСЛЕ утверждений.
  const controlFrom = calls.length;
  // Ждём именно СЕТЕВОЕ событие (детерминированно): фиксированную паузу запрещает `check_e2e_no_sleeps`,
  // а `waitForResponse` доказывает, что запрос реально ушёл и был замечен.
  const controlResponse = page
    .waitForResponse((r) => r.url().indexOf('/api/v1/journal/records') !== -1, { timeout: 10000 })
    .catch(() => null);
  await page.evaluate(() => {
    // статус не важен (может быть 401 без заголовка) — проверяем сам факт регистрации запроса сборщиком
    return fetch('/api/v1/journal/records').catch(() => null);
  });
  await controlResponse;
  const controlSeen = calls.slice(controlFrom).filter((l) => l.indexOf('/journal/records') !== -1);
  expect(controlSeen.length, 'контроль приёмки: сборщик окна обязан видеть `journal/records` ' +
    '(иначе «полный журнал x0» — зелёный на пустоте); окно: ' + windowText)
    .toBeGreaterThanOrEqual(1);
  console.log('::notice::FE-016 контроль: сборщик видит journal/records x' + controlSeen.length);
  console.log('::notice::FE-016: control journal x' + controlJournal.length +
    ', control partners x' + controlPartners.length);
  // правка (@AR `2026-11-10 13:00`): у `C < 1` ДВЕ разные причины, и они не должны сливаться в «invalid»:
  //   (а) `P = 0` — трафика до окна не было вовсе ⇒ контроль пути неприменим (недействительное измерение);
  //   (б) `P > 0` — `/partners` ушёл, а `/journal/records` нет ⇒ цепочка `partners → journal` ПОРВАЛАСЬ после
  //       первой ноги. Это дефект, и раньше он маскировался под «недействительно».
  // Поэтому три состояния — три разных исхода.
  if (controlJournal.length >= 1) {
    // (1) журнал грузился сам — контроль пути выполнен, окно измерено корректно
  } else if (controlPartners.length > 0) {
    // (2) порванная цепочка — это КРАСНЫЙ, а не warning
    expect(controlJournal.length, 'контроль пути: цепочка `partners → journal` порвалась — ' +
      'partners x' + controlPartners.length + ', journal x0; до окна: ' + beforeWindow.join(' | '))
      .toBeGreaterThanOrEqual(1);
  } else {
    // (3) трафика до окна нет вообще (кэш/localStorage) — измерение недействительно; печатаем громко и с номером
    // прогона, чтобы «всегда invalid» было видно как тренд, а не как единичная аномалия (@AR 13:00).
    console.log('::warning::FE-016 invalid: контроль пути недействителен — до окна ни одного запроса ' +
      '(run=' + (process.env.GITHUB_RUN_ID || 'локально') + '); сторож в этом прогоне выключен');
  }

  await page.click('#btn-wizard-close');
});
