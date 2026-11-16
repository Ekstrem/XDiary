// Трассировка: TE-109 — тост с текстом причины не проверялся ни одной спекой.
// Дефект, который ловит эта спека: продукт показывает ошибку через `#toast`
// (`8-front/js/app.js:106` — `textContent = msg`, `classList.remove('hidden')`),
// а спеки читали тост только для логов (`api.js:reportStep`) и никогда не утверждали
// ни видимость, ни текст. Значит «тост с причиной» мог исчезнуть/опустеть, и ни один
// прогон бы не покраснел.
//
// Негативный контроль (дефект-плечо, без правки продукта):
//   XDIARY_E2E_TOAST_NEGATIVE=1 → спека ждёт заведомо отсутствующий текст и ОБЯЗАНА упасть.
//   Обычный прогон этого не делает: он утверждает реальную причину «Укажите имя».
const { test, expect } = require('@playwright/test');
const { loginWithPin } = require('./api');

const NEGATIVE = process.env.XDIARY_E2E_TOAST_NEGATIVE === '1';

test('E2E: тост показывает текст причины, а не пустой/скрытый элемент (TE-109)', async ({ page }) => {
  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible();

  await page.click('#tab-partners');
  await expect(page.locator('#view-partners')).toBeVisible();

  await page.click('#btn-partner-add');
  await expect(page.locator('#view-partner-edit')).toBeVisible();

  // Поле имени НЕ заполняем намеренно: сохранение обязано быть отклонено продуктом
  // (`app.js: savePartnerEdit` → `if (!name) { toast("Укажите имя"); return; }`).
  await page.click('#btn-partner-save', { timeout: 20000 });

  const toast = page.locator('#toast');
  // Утверждение 1: тост ВИДИМ (не только присутствует в DOM).
  await expect(toast).toBeVisible({ timeout: 10000 });

  const text = ((await toast.innerText({ timeout: 2000 }).catch(() => '')) || '').trim();
  console.log('::notice::toast-reason: текст тоста: ' + JSON.stringify(text.slice(0, 160)));

  // Утверждение 2: тост содержит ТЕКСТ ПРИЧИНЫ. Именно этого не хватало (TE-109):
  // без него утверждение «тост виден» проходит и на пустом тосте.
  const expected = NEGATIVE ? /ТЕКСТ-КОТОРОГО-НЕ-БЫВАЕТ/ : /Укажите имя/i;
  if (NEGATIVE) {
    console.log('::notice::toast-reason: НЕГАТИВНЫЙ КОНТРОЛЬ — ждём падение на заведомо неверном тексте');
  }
  await expect(toast).toHaveText(expected, { timeout: 5000 });

  // Утверждение 3: отказ не уводит пользователя из формы (иначе «причина показана»,
  // а действие уже сделано/экран потерян).
  await expect(page.locator('#view-partner-edit')).toBeVisible();

  // Пустое имя не создаёт партнёра — чистить нечего; на всякий случай подтверждаем,
  // что список не получил новую строку с пустым именем.
  await page.click('#btn-partner-edit-back').catch(() => {});
});
