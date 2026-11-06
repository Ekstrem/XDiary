// ============================================================================
// LEGACY (FE-025/026 + ADR-021): спека проверяет УБРАННЫЙ контракт.
// Было: `POST /api/v1/privacy/token {pin:""}` выдавал device-токен, UI ждал `#tabbar`.
// Сейчас: PIN НЕ выдаёт токен — выдача по OTP/client-JWT, `#tabbar` больше нет.
// Поэтому спека исключена из гейта через `describe.skip`, но НЕ удалена:
// переписать под модель «чистое устройство → онбординг → журнал» — отдельная задача.
// ============================================================================
// E2E: сценарий «без PIN» (FE-006/BE-019) — только для стенда БЕЗ PIN.
// Если на стенде PIN задан, приложение без локального PIN получает 401 на login("") —
// это отдельный продуктовый дефект (клиент не предлагает ввести PIN с другого устройства).
const { test, expect } = require('@playwright/test');
const { API_BASE, installBase, waitToken } = require('./api');

test.describe.skip('legacy: PIN больше не выдаёт токен (FE-025/026) — переписать', () => {
  test('E2E: без PIN приложение получает device-токен и открывает журнал', async ({ page }) => {
  // ADR-021 этап 2 (@AR 2026-11-16): в `9-tests` не должно быть `fetch`/`request` в `/privacy/*`,
  // даже в skipped-блоке — только описания и ссылки на историю задач.
  // История: проба требовала `POST /api/v1/privacy/token {pin:""}` = 200 (стенд без PIN);
  // контракт отменён FE-025/026, а регресс-защита живёт в этой записи, не в исполняемом коде.

  await installBase(page);
  await page.goto('/');
  await expect(page.locator('#tabbar')).toBeVisible();
  await waitToken(page);
  expect(await page.evaluate(() => localStorage.getItem('xdiary.accessToken'))).toBeTruthy();
});
});
