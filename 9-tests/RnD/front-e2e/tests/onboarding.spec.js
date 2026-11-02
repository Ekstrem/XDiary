// E2E (настоящий) — TE-025: онбординг «телефон → SMS OTP → client-JWT → link-partner» (FE-018 ч.2, 0.48).
//
// Путь нового устройства (после 0.47/0.48):
//   чистый контекст (нет локального PIN и нет xdiary.legacy) → afterAuth() → Pin.hasPin()=false → openOnboarding().
// Если на устройстве PIN уже есть — путь legacy (журнал), поэтому чистим оба флага перед стартом.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, installBase, waitToken, dumpViews, reportStep, expectScreen, watchLinkPartner } = require('./api');

const PHONE = '+7999000' + String(Date.now()).slice(-4);
const TEST_OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';

async function clearFlags(page) {
  await page.evaluate(() => {
    try {
      localStorage.removeItem('xdiary.clientId');
      localStorage.removeItem('xdiary.legacy');
      localStorage.removeItem('xdiary.accessToken');
    } catch (e) {}
  });
}

test('E2E: онбординг — телефон → OTP → link-partner → «я» по clientId', async ({ page }) => {
  await installBase(page);
  await page.goto('/');
  await clearFlags(page);

  await test.step('чистое устройство → экран онбординга «кто ты»', async () => {
    await page.reload();
    await expect(page.locator('#view-onboarding')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#onb-phone')).toBeVisible();
  });

  await test.step('шаг 1: телефон → запрос OTP', async () => {
    await page.fill('#onb-phone-input', PHONE);
    await page.click('#btn-onb-phone');
    await expect(page.locator('#onb-otp')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#onb-otp-phone')).toContainText(PHONE);
  });

  await test.step('шаг 2: OTP ' + TEST_OTP + ' → верификация и client-JWT', async () => {
    await page.fill('#onb-otp-input', TEST_OTP);
    await page.click('#btn-onb-otp');
    await expectScreen(page, '#onb-link', 'OTP принят (verify вернул 200)', 20000);
    // TE-036 (@TL 03:50): шаг пройден = #onb-link виден И нет тоста ошибки. Порядок важен:
    // если бы verify вернул 400, спека упала бы ЗДЕСЬ, а не на «#view-journal hidden».
    await reportStep(page, 'шаг 2: OTP → привязка');
    const clientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId'));
    expect(clientId && clientId !== 'pending', 'clientId сохранён после верификации: ' + clientId).toBe(true);
  });

  await test.step('шаг 3: привязка «я» → журнал', async () => {
    // TE-043: карточка выбирается ДЕТЕРМИНИРОВАННО — по имени партнёра, созданного этим
    // прогоном. Раньше брался `.first()`: на стенде с сотнями чужих карточек это
    // произвольная старая запись (2635 партнёров → «флейк 20%», DO-044/TE-043).
    const partnerName = 'e2e-auto-' + Date.now();
    const created = await page.evaluate(async ({ base, name }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      if (!t) return { ok: false, noToken: true };
      const r = await fetch(base + '/api/v1/partners', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + t },
        body: JSON.stringify({ name }),
      });
      const j = r.ok ? await r.json().catch(() => null) : null;
      return { ok: r.ok, status: r.status, id: (j && j.id) || null };
    }, { base: API_BASE, name: partnerName }).catch((e) => ({ ok: false, err: String(e).slice(0, 120) }));
    console.log('::warning::партнёр через API: ' + JSON.stringify(created) + ' name=' + partnerName);

    const allCards = page.locator('#onboarding-list .partner-card');
    const ownCards = allCards.filter({ hasText: partnerName });
    const countOwn = async () => await ownCards.count();

    let ownCount = 0;
    for (let i = 0; i < 10 && ownCount === 0; i++) {
      ownCount = await countOwn();
      if (ownCount === 0) await page.waitForTimeout(500);
    }
    if (ownCount === 0) {
      // список мог не обновиться после создания через API — один reload и ждём свою карточку
      await page.reload();
      await expectScreen(page, '#onb-link', 'после reload: шаг привязки виден', 20000);
      for (let i = 0; i < 30 && ownCount === 0; i++) {
        ownCount = await countOwn();
        if (ownCount === 0) await page.waitForTimeout(500);
      }
    }
    const totalCards = await allCards.count();
    console.log('::warning::карточек всего: ' + totalCards + ', своих по имени ' + partnerName + ': ' + ownCount);
    if (ownCount !== 1) {
      throw new Error(
        'Своя карточка не найдена однозначно: совпадений ' + ownCount + ' из ' + totalCards +
        ', партнёр создан=' + JSON.stringify(created) + ', имя=' + partnerName +
        '. Клик не выполняется — привязка к чужому партнёру недопустима (TE-043).'
      );
    }
    const card = ownCards;
    await card.scrollIntoViewIfNeeded().catch(() => {});
    let mode = 'link-partner по своей карточке (по имени)';
    // TE-037 (@TL 07:06): перехват сети вокруг клика. Без него нельзя отличить
    // «POST /link-partner не отправлялся» от «отправлялся и вернул ошибку» —
    // в #17 сетевых вызовов в логе не было, а #onb-link-error остался hidden.
    const linkWatch = watchLinkPartner(page);
    // TE-032 (@TL 08:31): два дешёвых факта для «клик не доходит до обработчика».
    // (1) кто реально под курсором в центре карточки — `elementFromPoint`;
    // (2) перерисовывался ли `#onboarding-list` между поиском карточки и кликом —
    //     `renderLinkPartners()` делает `innerHTML=""`, и клик может уйти в отсоединённый узел.
    await page.evaluate(() => {
      const list = document.querySelector('#onboarding-list');
      window.__e2eListMut = 0;
      try { if (window.__e2eListObs) window.__e2eListObs.disconnect(); } catch (e) {}
      if (list && window.MutationObserver) {
        window.__e2eListObs = new MutationObserver((ms) => { window.__e2eListMut += ms.length; });
        window.__e2eListObs.observe(list, { childList: true, subtree: true });
      }
    }).catch(() => {});
    // TE-036: карточка может быть видима, но не кликабельна (overlay/pointer-events) —
    // тогда «клик не доходит» и «FE не переключает экран» выглядят одинаково.
    if (await card.count()) {
      const vis = await card.isVisible();
      const box = await card.boundingBox().catch(() => null);
      const clickable = await card.evaluate((el) => {
        const cs = getComputedStyle(el);
        return { pointerEvents: cs.pointerEvents, display: cs.display, visibility: cs.visibility };
      }).catch(() => null);
      console.log('::notice::карточка партнёра: visible=' + vis + ', box=' + JSON.stringify(box) + ', style=' + JSON.stringify(clickable));
      // (1) КТО под курсором — до клика, по реальным координатам центра карточки.
      const under = await card.evaluate((el) => {
        const r = el.getBoundingClientRect();
        const x = r.left + r.width / 2;
        const y = r.top + r.height / 2;
        const hit = document.elementFromPoint(x, y);
        if (!hit) return { point: { x, y }, hit: null };
        const cardHit = hit.closest('.partner-card');
        return {
          point: { x, y },
          hit: { tag: hit.tagName, id: hit.id || '', cls: String(hit.className || '').slice(0, 60) },
          inCard: !!cardHit,
          sameCard: cardHit === el,
        };
      }).catch(() => null);
      console.log('::warning::под курсором в центре карточки: ' + JSON.stringify(under));
      await card.click();
      mode = 'link-partner по карточке';
    }
    // Дать сети шанс отработать и зафиксировать факт: уходил ли POST /link-partner.
    await page.waitForTimeout(700);
    // (2) Перерисовывался ли список — счётчик мутаций `#onboarding-list` за время клика.
    const mutations = await page.evaluate(() => {
      try { if (window.__e2eListObs) window.__e2eListObs.disconnect(); } catch (e) {}
      return window.__e2eListMut || 0;
    }).catch(() => null);
    console.log('::warning::перерисовок списка за клик: ' + mutations);
    linkWatch.report('после клика по карточке');
    // Снимок экранов ДО ожидания: видно, дошло ли приложение до журнала и что осталось висеть.
    await dumpViews(page, 'после привязки');
    // Маскированный ответ me(): пуст ли linkedPartnerId фактически.
    const meNow = await page.evaluate(async (base) => {
      const id = localStorage.getItem('xdiary.clientId') || '';
      const t = localStorage.getItem('xdiary.accessToken') || '';
      if (!id || !t) return { noSession: true };
      const r = await fetch(base + '/api/v1/clients/' + id, { headers: { Authorization: 'Bearer ' + t } });
      const j = r.ok ? await r.json().catch(() => null) : null;
      return { status: r.status, verified: !!(j && j.verified), linked: !!(j && j.linkedPartnerId) };
    }, API_BASE).catch(() => null);
    console.log('::notice::me() после привязки: ' + JSON.stringify(meNow));
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    console.log('::notice::TE-025 онбординг пройден: ' + mode + ', phone=' + PHONE);
  });

  await test.step('«я» с сервера после перезагрузки', async () => {
    const clientId = await page.evaluate(() => localStorage.getItem('xdiary.clientId'));
    expect(clientId, 'clientId сохранён').toBeTruthy();
    await page.reload();
    // после reload — снова журнал (клиент верифицирован), либо PIN-экран
    const lock = page.locator('#view-lock');
    if (await lock.isVisible()) {
      await page.fill('#pin-input', PIN);
      await page.click('#pin-submit');
      await waitToken(page);
    }
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

    const me = await page.evaluate(async ({ base, id }) => {
      const t = localStorage.getItem('xdiary.accessToken') || '';
      const r = await fetch(base + '/api/v1/clients/' + id, { headers: t ? { Authorization: 'Bearer ' + t } : {} });
      return { status: r.status, data: r.ok ? await r.json() : null };
    }, { base: API_BASE, id: clientId });
    expect(me.status, 'GET /clients/{id}').toBe(200);
    expect(me.data && me.data.verified, 'клиент верифицирован на сервере').toBe(true);
    console.log('::notice::TE-025 «я» с сервера после reload: clientId=' + clientId + ', verified=' + (me.data && me.data.verified));
  });
});
