// E2E (настоящий) — TE-018: тренды.
// ВАЖНО: сейчас тренды читают ЛОКАЛЬНЫЙ IndexedDB (db.js: weekly/sleep_weekly, наполняется
// только CSV-импортом), а не API (somatic-series/daily-metrics). Это разрыв миграции FE-003,
// зафиксирован в TE-018. Сценарий покрывает наблюдаемое: вид открывается, вкладки/чипы
// рендерятся, переключение метрик и «Корреляции» не падает, ошибок консоли нет.
const { test, expect } = require('@playwright/test');
const { loginWithPin } = require('./api');

test('E2E: тренды — открываются и рендерятся без ошибок консоли', async ({ page }) => {
  const errors = [];
  // лог API-ответов: без него в аннотации не видно, КАКОЙ эндпоинт даёт 401
  const apiLog = [];
  page.on('response', (r) => {
    if (r.url().includes('/api/')) apiLog.push(r.status() + ' ' + r.request().method() + ' ' + r.url());
  });
  page.on('requestfailed', (r) => apiLog.push('FAILED ' + r.url()));
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
    // #trend-corr может быть пустым (нулевая высота), если API не дал точек для
    // корреляций — проверяем, что он не hidden, а не геометрическую видимость.
    await expect(page.locator('#trend-corr')).not.toHaveClass(/hidden/);
    await page.click('#trend-tabs button[data-tab="trends"]');
    await expect(page.locator('#trend-trends')).not.toHaveClass(/hidden/);
  });

  await test.step('ошибок в консоли нет', async () => {
    expect(errors, 'консольные ошибки: ' + JSON.stringify(errors) +
      ' ;; API: ' + apiLog.slice(-15).join(' | ')).toEqual([]);
  });
});
