// Трассировка: TE-036 (разбор падающих UI-спеков: `add-partner` флейчил в #941) +
// TE-032 (E2E-фикстуры под новую модель входа). Прежняя ссылка вела на отклонённую карточку — убрана:
// на отклонённые задачи трассировка не ссылается (проверяет check_e2e_traceability).
// E2E (настоящий): живое приложение + живой шлюз, без заглушек.
// Вход по PIN (как пользователь) → POST /api/v1/partners реально уходит →
// партнёр появляется в списке из бэкенда → тест удаляет его за собой.
const { test, expect } = require('@playwright/test');
const { loginWithPin, removeByName } = require('./api');

test('E2E: партнёр — добавить в приложении, увидеть в списке, удалить', async ({ page }) => {
  const name = 'TE-' + Date.now();
  try {
    await loginWithPin(page);
    await expect(page.locator('#view-journal')).toBeVisible();

    await page.click('#tab-partners');
    await expect(page.locator('#view-partners')).toBeVisible();

    await page.click('#btn-partner-add');
    await expect(page.locator('#view-partner-edit')).toBeVisible();
    await page.fill('#pe-name', name);
    await page.click('#btn-partner-save', { timeout: 20000 });

    await expect(page.locator('#view-partners')).toBeVisible();
    // правка (@DO `2026-11-10 16:21`): весь список содержит и чужие сиды, поэтому «мой строки нет»
  // неотличимо от «список показывает только сид». Утверждаем СТРОКУ партнёра, а не текст контейнера.
  // правка (@DO 17:03): печатаем ЧИСЛО карточек — «список пуст» и «список есть, моей строки нет» —
  // разные дефекты, и по одному `toHaveCount(1)` их не различить.
  // правка: в `#partners-list` карточки рендерятся как `button.partner-row` (`8-front/js/app.js:1305`),
  // а `.partner-card` — это сетка ВИЗАРДА (`app.js:835`) и CSS для неё (`styles.css:181`).
  const cards = await page.locator('#partners-list .partner-row').count().catch(() => -1);
  console.log('::notice::add-partner: карточек в списке после создания: ' + cards +
    ' | ищем: ' + name);
  await expect(page.locator('#partners-list .partner-row', { hasText: name })).toHaveCount(1);
  } finally {
    const r = await removeByName(page, 'partner', name);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
