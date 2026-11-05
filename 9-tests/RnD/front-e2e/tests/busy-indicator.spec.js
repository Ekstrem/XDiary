// E2E — `TE-059`: индикатор `busyRun` (`FE-040`) — появляется на медленном ответе, НЕ появляется на быстром.
//
// Почему эта спека существует: аудит `9-tests/reports/spec-coverage-gaps.md` — `busyRun` покрыт
// **0 спек** (только юнит-тест таймеров). То есть в браузере индикатор не проверялся вообще.
//
// Механика (`8-front/js/app.js`):
//   `busyStart(label, retry)` (строка 1983) вставляет `<div class="busy-banner" role="status">`
//   с `display:none` и таймером показа через **300 мс**; текст `"<label>…"`, а через **3000 мс** —
//   `"<label>: дольше обычного"` + кнопка «Повторить» (когда передан `retry`).
//   Тренды: клик по `#tab-trends` → `renderTrends()` → `busyRun("Считаем тренды", () => Api.daily.series(...))`
//   (строка 2064), то есть задерживать нужно `**/api/v1/daily-metrics/series**`, а не analytics.
//
// Критерий не «индикатор есть», а **порог**: на быстром ответе его не должно быть видно — иначе он мелькает.
//
// Включается `XDIARY_BUSY=1`; без признака — `skip`, чтобы не менять поведение обычных прогонов.
const { test, expect } = require('@playwright/test');
const { loginWithPin, reportStep } = require('./api');

const ENABLED = process.env.XDIARY_BUSY === '1';
const BANNER = '.busy-banner';
const SERIES = '**/api/v1/daily-metrics/series**';

// Задержка ответа трендов на `ms` мс (эндпоинт тот же, что читает `renderTrends`).
async function delaySeries(page, ms) {
  await page.route(SERIES, async (route) => {
    await new Promise((r) => setTimeout(r, ms));
    await route.continue();
  });
}

async function openTrends(page) {
  // Вкладка «Тренды» в нижней навигации; переход вызывает `renderTrends()` → `busyRun(...)`.
  await page.click('#tab-trends');
}

test('E2E: busyRun — появляется на 1000 мс, не появляется на 100 мс, исчезает (FE-040)', async ({ page }) => {
  test.skip(!ENABLED, 'pending:TE-059 — включается XDIARY_BUSY=1 (проверяет индикатор FE-040 в браузере)');

  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  await reportStep(page, 'шаг 1: сессия получена');

  // ── Критерий «порог»: быстрый ответ 100 мс → баннера быть не должно ────────────────────────
  await delaySeries(page, 100);
  try {
    await openTrends(page);
    await page.waitForTimeout(250);
    await expect(
      page.locator(BANNER + ':visible'),
      'индикатор показан на быстром ответе (100 мс) — порог 300 мс нарушен, он будет мелькать'
    ).toHaveCount(0);
    await reportStep(page, 'шаг 2: быстрый ответ 100 мс — индикатора нет (порог соблюдён)');
  } finally {
    await page.unroute(SERIES).catch(() => {});
  }

  // ── Критерий «появляется»: медленный ответ 1000 мс ─────────────────────────────────────────
  await delaySeries(page, 1000);
  try {
    await openTrends(page);
    const banner = page.locator(BANNER + ':visible');
    await expect(
      banner,
      'при задержке 1000 мс индикатор не появился — признак «считаем» не работает'
    ).toHaveCount(1, { timeout: 1500 });
    await expect(banner).toHaveAttribute('role', 'status');
    await expect(banner).toContainText(/Считаем тренды/i);

    // ── Критерий «исчезает»: после завершения не остаётся «вечного спиннера» ─────────────────
    await expect(
      page.locator(BANNER + ':visible'),
      'индикатор остался после завершения операции — «вечный спиннер»'
    ).toHaveCount(0, { timeout: 20000 });
    await reportStep(page, 'шаг 3: индикатор появился и исчез после завершения');
  } finally {
    await page.unroute(SERIES).catch(() => {});
  }
});

test('E2E: busyRun — на задержке > 3000 мс текст «дольше обычного» и кнопка «Повторить»', async ({ page }) => {
  test.skip(!ENABLED, 'pending:TE-059 — включается XDIARY_BUSY=1');

  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

  await delaySeries(page, 4000);
  try {
    await openTrends(page);
    const banner = page.locator(BANNER + ':visible');
    await expect(banner).toHaveCount(1, { timeout: 1500 });

    // Через 3 с текст меняется на «дольше обычного», и показывается кнопка повтора:
    // `renderTrends` передан как `retry`, значит кнопка обязана появиться.
    await expect(banner).toContainText(/дольше обычного/i, { timeout: 6000 });
    await expect(
      banner.locator('button'),
      'кнопка «Повторить» не показана, хотя retry передан (FE-040: 3 с → текст + повтор)'
    ).toBeVisible({ timeout: 6000 });
    await reportStep(page, 'шаг 4: на 4 с появились текст «дольше обычного» и кнопка повтора');
  } finally {
    await page.unroute(SERIES).catch(() => {});
  }
});
