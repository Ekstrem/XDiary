// E2E (настоящий) — TE-018: тренды.
// ВАЖНО: сейчас тренды читают ЛОКАЛЬНЫЙ IndexedDB (db.js: weekly/sleep_weekly, наполняется
// только CSV-импортом), а не API (somatic-series/daily-metrics). Это разрыв миграции FE-003,
// зафиксирован в TE-018. Сценарий покрывает наблюдаемое: вид открывается, вкладки/чипы
// рендерятся, переключение метрик и «Корреляции» не падает, ошибок консоли нет.
const { test, expect } = require('@playwright/test');
const { loginWithPin } = require('./api');

test('E2E: тренды — открываются и рендерятся без ошибок консоли', async ({ page }) => {
  const errors = [];
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console: ' + m.text()); });

  await test.step('login по PIN', async () => {
    await loginWithPin(page);
  });

  await test.step('открыть «Тренды» и проверить структуру', async () => {
    await page.click('#tab-trends');
    await expect(page.locator('#view-trends')).toBeVisible();
    await expect(page.locator('#trend-tabs button')).toHaveCount(2);
    await expect(page.locator('#trend-chips button')).toHaveCount(4);
    await expect(page.locator('#trend-canvas')).toBeVisible();
  });

  await test.step('переключить все 4 метрики', async () => {
    for (let i = 0; i < 4; i += 1) {
      await page.locator('#trend-chips button').nth(i).click();
      await expect(page.locator('#trend-canvas')).toBeVisible();
    }
  });

  await test.step('вкладка «Корреляции»', async () => {
    await page.click('#trend-tabs button[data-tab="corr"]');
    await expect(page.locator('#trend-corr')).toBeVisible();
    await page.click('#trend-tabs button[data-tab="trends"]');
    await expect(page.locator('#trend-trends')).not.toHaveClass(/hidden/);
  });

  await test.step('ошибок в консоли нет', async () => {
    expect(errors, 'консольные ошибки: ' + JSON.stringify(errors)).toEqual([]);
  });
});
