// Пробы двух уровней таймаута (@TL 2026-11-13 02:25). Обе — МИМО `.catch`:
// иначе сигнал гаснет (канон @AR 02:12, 5-й случай «гейт не удерживает состояние»).
const { test, expect } = require('@playwright/test');

test('пробы: таймаут класса и экземпляра', async ({ page }) => {
  await page.goto('/');

  let cls = '';
  let t0 = Date.now();
  try { await page.locator('#definitely-absent-element').click(); } catch (e) { cls = String((e && e.message) || e); }
  const clsSecs = Math.round((Date.now() - t0) / 100) / 10;

  let inst = '';
  t0 = Date.now();
  try {
    await page.locator('#definitely-absent-element').innerText({ timeout: 1500 });
  } catch (e) { inst = String((e && e.message) || e); }
  const instSecs = Math.round((Date.now() - t0) / 100) / 10;

  console.log('::notice::probe-class: ' + cls.replace(/\n/g, ' ').slice(0, 130) + ' ;; dur=' + clsSecs + 's');
  console.log('::notice::probe-instance: ' + inst.replace(/\n/g, ' ').slice(0, 130) + ' ;; dur=' + instSecs + 's');

  expect(cls).toContain('15000ms');
  expect(inst).toContain('1500ms');
});
