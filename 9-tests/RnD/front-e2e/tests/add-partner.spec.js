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
    // В логе прогона `43402985971` в списке была 1 ЧУЖАЯ карточка: значит надо различать
    // «POST не ушёл с нашим именем» и «ушёл, но список не обновился».
    const filled = await page.inputValue('#pe-name').catch(() => '');
    if (filled !== null && filled !== undefined && filled !== name) {
      throw new Error('add-partner: поле имени не приняло значение (в поле: ' +
        JSON.stringify(filled) + ', ждали ' + JSON.stringify(name) + ')');
    }
    // TE-110 (@TL 2026-11-13 11:41): ждём ОТВЕТ `POST /partners`, а не «сколько-то миллисекунд».
    // Без этого список мог ещё не обновиться, и `toHaveCount` падал на дефолтных 10 с (флейк:
    // первый прогон красный, повтор того же SHA — зелёный).
    const saveResp = page
      .waitForResponse(
        (r) => r.url().includes('/api/v1/partners') && r.request().method() === 'POST',
        { timeout: 20000 }
      )
      .catch(() => null);
    await page.click('#btn-partner-save', { timeout: 20000 });
    const saved = await saveResp;
    console.log('::notice::add-partner: POST /partners -> ' +
      (saved ? saved.status() : 'ответ не дождались (20s)'));

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
  // Явный таймаут + сообщение, которое отличает «строка не пришла» от «пришла под другим именем».
  try {
    await expect(page.locator('#partners-list .partner-row', { hasText: name }))
      .toHaveCount(1, { timeout: 20000 });
  } catch (e) {
    const names = await page
      .locator('#partners-list .partner-row')
      .evaluateAll((rows) => rows.map((r) => {
        const n = r.querySelector('.partner-row-name, span');
        const txt = ((n && n.textContent) || r.innerText || '').trim();
        return txt.slice(0, 40);
      }).slice(0, 10))
      .catch(() => []);
    const fromApi = await page
      .evaluate(async () => {
        const base = localStorage.getItem('xdiary.apiBase') || '';
        const t = localStorage.getItem('xdiary.accessToken') || '';
        const r = await fetch(base + '/api/v1/partners', { headers: t ? { Authorization: 'Bearer ' + t } : {} });
        const list = await r.json().catch(() => null);
        const names = Array.isArray(list) ? list.map((x) => (x && (x.name || x.title)) || '?') : [];
        return { status: r.status, count: names.length, names: names.slice(-5) };
      })
      .catch((e) => ({ error: String((e && e.message) || e).slice(0, 120) }));
    throw new Error('add-partner: строки с именем ' + name + ' нет в списке за 20 с; ' +
      'имена из API: ' + JSON.stringify(fromApi) + '; ' +
      'карточек: ' + (names.length || 0) + '; первые имена: ' + JSON.stringify(names) +
      ' | POST /partners: ' + (saved ? saved.status() : 'нет ответа') + ' | ' + e.message);
  }
  } finally {
    const r = await removeByName(page, 'partner', name);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
