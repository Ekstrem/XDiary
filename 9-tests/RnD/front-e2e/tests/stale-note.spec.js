// E2E — `TE-058`: `stale-note` (`04-architecture/read-paths.md` §5 п.2) — «кэш назван, свежее без бейджа».
//
// Почему эта спека существует: аудит `9-tests/reports/spec-coverage-gaps.md` показал **0 спек** на
// контракт `FE-044`. То есть «зелёный E2E» не означал «правило п.2 подтверждено» — это тот же класс,
// что «зелено на пустом».
//
// Что проверяем (критерии из карточки, дословно):
//   1. при **живом** ответе API бейджа `#stale-note` **нет** (иначе шум на свежих данных, второй критерий @FE);
//   2. при чтении **из кэша** (живой запрос заблокирован, копия в IndexedDB старше 24 ч) бейдж **есть**;
//   3. текст соответствует `/(данные могли устареть|синхронизированы .* ч назад)/`.
//
// Механика фронта (`8-front/js/app.js`): `showStaleNote(viewId, kinds)` выходит сразу, если
// `DB.readFreshness(k)` не помечен `fromCache`; признак ставится в `repository.js` только при отказе
// живого чтения; порог — `STALE_AFTER_HOURS_UI = 24`. Поэтому «свежий» сценарий проверяется без
// подмены сети, а «устаревший» — блокировкой эндпоинтов ПОСЛЕ первой успешной загрузки (кэш уже есть).
//
// Включается `XDIARY_STALENOTE=1`; без признака — `skip`, чтобы не менять поведение обычных прогонов.
const { test, expect } = require('@playwright/test');
const { loginWithPin, reportStep, unlockIfLocked } = require('./api');

// SW блокируем по той же причине, что и в busy-indicator: `page.route` не видит запросы
// service worker'а, а спека опирается на подмену сети и на перезагрузку.
test.use({ serviceWorkers: 'block' });

const ENABLED = process.env.XDIARY_STALENOTE === '1';

// Сдвигаем «возраст» кэша в прошлое на 48 ч, чтобы пройти порог 24 ч без ожидания суток.
// ВАЖНО: `syncAt.<kind>` живёт в **IndexedDB** (`8-front/js/db.js` store `meta`, `DB.getMeta/setMeta`),
// а не в localStorage — `repository.js:150` пишет через `DB.setMeta`, а свежесть читается
// `DB.readFreshness` (in-memory признак `fromCache`). Поэтому первый заход «через localStorage»
// здесь был бы вечно-зелёным: он ничего не сдвигал. Ключ пишем через ту же `DB`, что и приложение.
const KINDS = ['partners', 'catalog'];

async function ageCache(page, hours) {
  return page.evaluate(
    async ({ kinds, h }) => {
      const moved = [];
      try {
        const shift = h * 3600000;
        for (const kind of kinds) {
          const key = 'syncAt.' + kind;
          const v = await window.DB.getMeta(key);
          if (v === null || v === undefined) continue;
          await window.DB.setMeta(key, Number(v) - shift);
          moved.push(key + ': ' + Number(v) + ' -> ' + (Number(v) - shift));
        }
      } catch (e) {
        return { moved, error: String(e) };
      }
      return { moved };
    },
    { kinds: KINDS, h: hours }
  );
}

test('E2E: stale-note — нет на свежих данных, есть на устаревшем кэше (read-paths §5 п.2)', async ({ page }) => {
  test.skip(
    !ENABLED,
    'pending:TE-058 — включается XDIARY_STALENOTE=1 (проверяет контракт FE-044 в браузере)'
  );

  // 1. Живой ответ: сначала нормальная загрузка визарда, чтобы кэш и `syncAt.*` появились.
  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  await page.click('#btn-new');
  await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 20000 });
  await reportStep(page, 'шаг 1: визард открыт на живом API');

  // Критерий 1: свежие данные — бейджа быть не должно.
  await expect(
    page.locator('#view-wizard #stale-note'),
    'бейдж «данные могли устареть» показан на свежих данных — это шум (второй критерий FE-044)'
  ).toHaveCount(0);

  // 2. Имитируем «живой ответ недоступен», но копия в кэше есть. `syncAt.*` сдвигаем на 48 ч.
  const aged = await ageCache(page, 48);
  await reportStep(page, 'шаг 2: сдвинуты метки кэша: ' + JSON.stringify(aged.moved));
  await page.route('**/api/v1/partners**', (route) => route.abort('failed'));
  await page.route('**/api/v1/catalog**', (route) => route.abort('failed'));
  try {
    await page.reload();
    // После reload приложение может показать замок (PIN) — снимаем его, а не ждём журнал вслепую.
    // Именно на этом упал первый прогон (@TL 09:14): «#view-journal hidden после reload».
    await unlockIfLocked(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 25000 });
    await page.click('#btn-new');
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 25000 });

    // Критерий 2: данные пришли из кэша — бейдж обязан быть.
    const note = page.locator('#view-wizard #stale-note');
    await expect(
      note,
      'данные взяты из устаревшего кэша, но бейдж `#stale-note` не показан — правило §5 п.2 не выполнено'
    ).toHaveCount(1, { timeout: 15000 });

    // Критерий 3: текст про устаревание (а не произвольный).
    await expect(note).toContainText(/данные могли устареть|синхронизированы .* ч назад/i, {
      timeout: 10000,
    });
    await expect(note).toHaveAttribute('role', 'status');

    await reportStep(page, 'шаг 3: бейдж на месте, текст и role="status" соответствуют');
  } finally {
    await page.unroute('**/api/v1/partners**').catch(() => {});
    await page.unroute('**/api/v1/catalog**').catch(() => {});
  }
});
