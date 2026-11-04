// E2E — TE-054: «визард без сети» — ожидаемое поведение по `04-architecture/read-paths.md` §1 п.1.
//
// Постановка @AR (`chat/ar.md` `[2026-11-15 04:55]`): «@TE: нужен сценарий „визард без сети“ —
// ожидаемое поведение по §1, а не „пусто и тихо“». §1 п.1 спеки: **молчаливый `catch` запрещён**;
// ошибка чтения обязана быть **отличима от «данных нет»**. Разрешено: явное состояние «не удалось»
// (неблокирующий бейдж/тост) либо отказ от предзаполнения **с сохранением формы** — но не тихий
// пустой список и не «визард вообще не открылся».
//
// Что проверяем (три утверждения, все — из спеки, а не из моего вкуса):
//   1. **Форма сохраняется:** при недоступном API визард ОТКРЫВАЕТСЯ. Сейчас это не так:
//      у `DB.getAllPartners()` нет `.catch` → `Promise.all` отвергается → `openWizard`/`reopenWizard`
//      без обработчика → визард не открывается и ошибка не показана (`read-paths.md` §2, «хуже тихого»).
//   2. **Ошибка видна:** в визарде или тосте есть явный признак отказа (не удалось / ошибка / нет связи /
//      офлайн). Тихий `null`/`[]` этот пункт не проходит — он неотличим от «истории ещё нет».
//   3. **Нет ложной подстановки:** при отказе предзаполнение не выдаётся за данные — выбранной карточки
//      партнёра по умолчанию нет (иначе пользователь видит «данные», которых не читали).
//
// Пока @FE не сделал §3 п.1–2, сценарий **не должен краснить гейт**: он включается внешним признаком
// `XDIARY_READPATH_OFFLINE=1` (env), а не наблюдением за поведением. После фикса флаг ставится в CI
// и тест становится обязательным — иначе он навсегда останется «зелёным на пустоте».
const { test, expect } = require('@playwright/test');
const { loginWithPin, reportStep, dumpViews } = require('./api');

const ENABLED = process.env.XDIARY_READPATH_OFFLINE === '1';

test('E2E: визард без сети — ошибка видна, форма сохранена (read-paths §1)', async ({ page }) => {
  test.skip(
    !ENABLED,
    'pending:FE-READPATH — ждём двух состояний на шаг (read-paths §3 п.1); включается XDIARY_READPATH_OFFLINE=1'
  );

  // Вход по PIN требует сети — поэтому сеть отключаем ПОСЛЕ логина, точечно: только MV журнала,
  // партнёры и каталог приходят из IndexedDB и не должны блокироваться (иначе проверим не то).
  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  await reportStep(page, 'шаг 1: сессия получена, дальше — имитация «нет сети» для MV журнала');

  await page.route('**/api/v1/journal/**', (route) => route.abort('failed'));
  await page.route('**/api/v1/partners**', (route) => route.abort('failed'));
  try {
    await page.click('#btn-new');
    await dumpViews(page, 'wizard-offline: после нажатия «+» без сети');

    // 1. Форма сохраняется: визард обязан открыться, а не «молча ничего».
    await expect(page.locator('#view-wizard')).toBeVisible({ timeout: 20000 });

    // 2. Ошибка обязана быть видна и отличима от «данных нет».
    const errorSignal = page.locator('#toast, #view-wizard').filter({
      hasText: /(не удалось|ошибк|нет связи|офлайн|offline)/i,
    });
    await expect(errorSignal.first(), 'нет видимого признака отказа чтения (read-paths §1 п.1)')
      .toBeVisible({ timeout: 20000 });

    // 3. Предзаполнение не выдаётся за прочитанные данные.
    await expect(page.locator('#w-partner-grid .partner-card.selected'))
      .toHaveCount(0, { timeout: 10000 });
  } finally {
    await page.unroute('**/api/v1/journal/**').catch(() => {});
    await page.unroute('**/api/v1/partners**').catch(() => {});
  }
});
