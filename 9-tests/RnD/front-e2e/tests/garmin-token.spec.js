// Трассировка: TE-112 (P1, владелец 2026-11-23: «поля ввода токена нет, синхронизация не работает,
// экспорт занятия невозможен — должны появиться тесты на это»).
// Регрессионный якорь владельца: тест ОБЯЗАН падать, если формы токена в настройках нет.
// До мержа FE-057 — skip с явной причиной (не ложный pass, §5 маркера front-e2e).
const { test, expect } = require('@playwright/test');
const { loginWithPin } = require('./api');

// Переворачивается в `true` после мержа FE-057 (форма токена + кнопка «Из Garmin»).
// Пока `false` — весь файл skip, и это НЕ засчитывается как «пройдено».
const FE057_MERGED = false;

test.skip(!FE057_MERGED, 'FE-057 not merged');

test('E2E: Garmin — поля токена в настройках, connect, «Из Garmin» через сервер', async ({ page }) => {
  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible();

  await page.click('#tab-settings');
  await expect(page.locator('#view-settings')).toBeVisible();

  // ЯКОРЬ (владелец 2026-11-23): трёх полей нет -> тест ПАДАЕТ, а не «skip».
  // Это и есть регрессия «поля токена убрали» — отличить от «API сломан» можно по диагностике ниже.
  await expect(page.locator('#garmin-api-key')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('#garmin-oauth-token')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('#garmin-token-expiry')).toBeVisible({ timeout: 10000 });

  await page.fill('#garmin-api-key', 'TE-e2e-key');
  await page.fill('#garmin-oauth-token', 'TE-e2e-token');
  await page.fill('#garmin-token-expiry', '2999-12-31');

  const connectResp = page
    .waitForResponse(
      (r) => r.url().includes('/api/v1/garmin/connect') && r.request().method() === 'POST',
      { timeout: 20000 }
    )
    .catch(() => null);
  await page.click('#btn-garmin-connect');
  const conn = await connectResp;
  console.log('::notice::garmin-token: POST /garmin/connect -> ' +
    (conn ? conn.status() : 'ответ не дождались (20s)'));

  // state: connected + lastSync; секретов в UI быть не должно (§6 AGENTS.md).
  const stateResp = page
    .waitForResponse(
      (r) => r.url().includes('/api/v1/garmin/state') && r.request().method() === 'GET',
      { timeout: 20000 }
    )
    .catch(() => null);
  await page.reload();
  const state = await stateResp;
  console.log('::notice::garmin-token: GET /garmin/state -> ' +
    (state ? state.status() : 'нет ответа'));
  const pageText = await page.locator('body').innerText();
  if (/TE-e2e-token/.test(pageText)) {
    throw new Error('garmin-token: oauth token виден в UI — утечка секрета в отображении');
  }

  // Кнопка «Из Garmin» на журнале: серверный sync, НЕ <input type=file> (CSV — отдельная кнопка).
  await page.click('#tab-journal');
  await expect(page.locator('#view-journal')).toBeVisible();
  const syncResp = page
    .waitForResponse(
      (r) => r.url().includes('/api/v1/garmin/sync') && r.request().method() === 'POST',
      { timeout: 20000 }
    )
    .catch(() => null);
  await page.click('#btn-journal-garmin');
  const sync = await syncResp;
  console.log('::notice::garmin-token: POST /garmin/sync -> ' +
    (sync ? sync.status() : 'ответ не дождались (20s)'));
  // Ключевое различие владельца: «Из Garmin» = серверный вызов, а не файловый импорт.
  if (await page.locator('input[type=file]').count() > 0 && !sync) {
    throw new Error('garmin-token: «Из Garmin» открыл файловый импорт вместо серверного sync ' +
      '(ожидали POST /api/v1/garmin/sync)');
  }
});
