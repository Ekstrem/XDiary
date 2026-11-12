// Временная спека-проба (@TL 2026-11-13 02:16, по замечанию @AR 02:12):
// фикс `actionTimeout`/`innerText({timeout})` проверялся на «зелёном» пути, где
// `.catch(() => '')` гасит сигнал. Здесь измеряем САМ таймаут действием, которое
// не имеет собственного timeout: значение в тексте ошибки и есть `actionTimeout`.
const { test, expect } = require('@playwright/test');

test('проба: actionTimeout применён к действию без явного таймаута', async ({ page }) => {
  await page.goto('/');
  const t0 = Date.now();
  let msg = '';
  try {
    await page.locator('#definitely-absent-element').click();
  } catch (e) {
    msg = String((e && e.message) || e);
  }
  const secs = Math.round((Date.now() - t0) / 100) / 10;
  console.log('::notice::probe: msg=' + msg.replace(/\n/g, ' ').slice(0, 200) + ' ;; dur=' + secs + 's');
  expect(msg).toContain('Timeout');
  expect(msg + ' ' + secs).toContain('15000');
});
