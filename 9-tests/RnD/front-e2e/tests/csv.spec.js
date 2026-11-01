// E2E (настоящий) — TE-021/3: CSV.
// Экспорт: `#btn-export` → скачивается `xdiary-YYYY-MM-DD.csv` (журнал, с BOM).
// Импорт: `#import-file` принимает Garmin-подобный CSV (`detectImportType` понимает
// somatic_minute/sleep_daily/sleep_weekly/stress_weekly/resting_weekly), показывает
// превью и применяет. Импорт пишет в локальный IndexedDB контекста — уборки на стенде не требует.
// ВАЖНО: экспорт журнала и импорт — РАЗНЫЕ форматы (журнал обратно не импортируется) — отмечено в TE-021.
const fs = require('fs');
const { test, expect } = require('@playwright/test');
const { loginWithPin } = require('./api');

test('E2E: CSV — экспорт журнала и импорт Garmin-CSV', async ({ page }) => {
  await test.step('login по PIN', async () => {
    await loginWithPin(page);
  });

  await test.step('настройки открыты', async () => {
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible();
  });

  await test.step('экспорт: скачивается валидный CSV журнала', async () => {
    const [download] = await Promise.all([
      page.waitForEvent('download', { timeout: 20000 }),
      page.click('#btn-export'),
    ]);
    expect(download.suggestedFilename(), 'имя файла').toMatch(/^xdiary-\d{4}-\d{2}-\d{2}\.csv$/);
    const p = await download.path();
    const text = fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, '');
    expect(text.length, 'CSV не пустой').toBeGreaterThan(0);
    expect(text.split(/\r?\n/).filter((l) => l.trim()).length, 'есть строки').toBeGreaterThanOrEqual(1);
  });

  await test.step('импорт: Garmin-подобный CSV (somatic_minute) принимается', async () => {
    const csv = 'timestamp,hr\n2026-11-02T10:00,60\n2026-11-02T10:01,62\n2026-11-02T10:02,61\n';
    await page.setInputFiles('#import-file', {
      name: 'somatic.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf8'),
    });
    await expect(page.locator('#view-import')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#btn-import-apply')).toBeVisible({ timeout: 20000 });
    await page.click('#btn-import-apply');
    await expect(page.locator('#import-result')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#import-result')).not.toHaveText(/^\s*$/);
  });
});
