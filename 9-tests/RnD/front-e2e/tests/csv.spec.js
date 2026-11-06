// E2E (настоящий) — TE-021/3: CSV.
// Экспорт: `#btn-export` → скачивается `xdiary-YYYY-MM-DD.csv` (журнал, с BOM).
// Импорт: `#import-file` принимает Garmin-подобный CSV (`detectImportType` понимает
// somatic_minute/sleep_daily/sleep_weekly/stress_weekly/resting_weekly), показывает
// превью и применяет. Импорт пишет в локальный IndexedDB контекста — уборки на стенде не требует.
// ВАЖНО: экспорт журнала и импорт — РАЗНЫЕ форматы (журнал обратно не импортируется) — отмечено в TE-021.
//
// MVP-истории: US-09
//   US-09 локальное хранение + выгрузка/загрузка CSV
const fs = require('fs');
const { test, expect } = require('@playwright/test');
const { loginWithPin, reportStep } = require('./api');

test('E2E: CSV — экспорт журнала и импорт Garmin-CSV', async ({ page }) => {
  test.setTimeout(200_000);
  // TE-062: бюджет теста = сумма объявленных ожиданий. При общем лимите 90 с такая спека
  // на медленном стенде падает по общему таймауту («не дождались»), хотя поведение верное.
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  await test.step('кнопка «Импорт CSV» в настройках открывает выбор файла (#btn-import)', async () => {
    // `#btn-import` до этого не трогала ни одна спека (карта `check_e2e_ui_coverage`), хотя это
    // видимая кнопка в настройках: её обработчик программно нажимает скрытый `#import-file`.
    // Проверяем саму связку — по клику обязан открыться выбор файла.
    await loginWithPin(page);
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible({ timeout: 20000 });
    const [chooser] = await Promise.all([
      page.waitForEvent('filechooser', { timeout: 15000 }),
      page.click('#btn-import'),
    ]);
    expect(chooser, 'по клику #btn-import открывается выбор файла').toBeTruthy();
    await chooser.setFiles([]); // диалог закрываем, файл не нужен — проверяли связку
  });

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

  await test.step('импорт: открыть экран импорта (Garmin-путь)', async () => {
    // #import-file (импорт журнала) идёт напрямую через importCsvFile и НЕ открывает
    // #view-import. Экран импорта — это Garmin-путь: #btn-import-open → #import-any-file.
    await page.click('#btn-import-open');
    await expect(page.locator('#view-import')).toBeVisible({ timeout: 20000 });
  });

  await test.step('импорт: Garmin-подобный CSV (somatic_minute) принимается', async () => {
    const csv = 'timestamp,hr\n2026-11-02T10:00,60\n2026-11-02T10:01,62\n2026-11-02T10:02,61\n';
    // `#btn-import-pick` («Выбрать CSV-файл») не проверялась: обязана открыть выбор файла.
    const [picker] = await Promise.all([
      page.waitForEvent('filechooser', { timeout: 15000 }),
      page.click('#btn-import-pick'),
    ]);
    expect(picker, 'по клику на «Выбрать CSV-файл» открывается выбор файла').toBeTruthy();
    await picker.setFiles([]);

    await page.setInputFiles('#import-any-file', {
      name: 'somatic.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf8'),
    });
    // Назад с экрана импорта (`#btn-import-back`) — не была покрыта: обязана вернуть в настройки.
    await page.click('#btn-import-back');
    await expect(page.locator('#view-settings')).toBeVisible({ timeout: 20000 });

    await page.click('#btn-import-open');
    await page.setInputFiles('#import-any-file', {
      name: 'somatic.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf8'),
    });
    await expect(page.locator('#btn-import-apply')).toBeVisible({ timeout: 20000 });
    await page.click('#btn-import-apply');
    await expect(page.locator('#import-result')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('#import-result')).not.toHaveText(/^\s*$/);
    // Диагностика для разбора в CI: печатает тост, если импорт что-то сказал пользователю.
    await reportStep(page, 'csv: импорт Garmin');

    // Импорт журнала через кнопку «Импорт CSV» в настройках (`#import-file`): это ЛОКАЛЬНЫЙ
    // путь (пишет в IndexedDB контекста), на стенд ничего не уходит. Ни одна спека его не трогала.
    await page.click('#tab-settings');
    await expect(page.locator('#view-settings')).toBeVisible({ timeout: 20000 });
    await page.setInputFiles('#import-file', {
      name: 'journal.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from('started_at,duration_min,partner,place\n2026-01-01T10:00,15,SMOKE,SMOKE\n', 'utf8'),
    });
    // Результат импорта показывается тостом/сообщением; главное — не падаем и остаёмся в настройках.
    await expect(page.locator('#view-settings')).toBeVisible({ timeout: 20000 });
  });
});
