// E2E — TE-053: что происходит с замком, когда PIN есть **на стенде**, а локального PIN нет.
// Трассировка: TE-007 → FE-007 (путь «PIN на стенде / нет локального»), TE-053 (эта спека), ADR-021 этап 1.
//
// ИСТОРИЯ (важно, чтобы спека не врала): в `0.87`–`0.89` `app.js` звал серверный
// `Api.privacy.*`, и путь был таким: устройство без локального PIN → `login("")` → 401 →
// **экран PIN** → `login(pin)` → журнал (постановка @FE `[2026-11-03 08:01]`, FE-007).
//
// НА 0.90 ЭТОГО ПУТИ НЕТ ПО ДИЗАЙНУ. `FE-042` этап 1 убрал `Api.privacy.*` из `app.js`
// (`git grep -n "Api\.privacy\." -- 8-front/js/app.js` → пусто), и по ADR-021 / AR-009 `B`
// PIN — **локальный** замок: `ensureView()` показывает `#view-lock` только при
// `Pin.hasPin() = true`, а при `false` уходит в онбординг/`afterAuth()` (`app.js:182-188`).
// Поэтому исход «без локального PIN показываем PIN-экран» стал бы **дефектом**, а не нормой:
// он означал бы, что клиент требуёт подтверждение, которого у него нет.
//
// ЧТО ПРОВЕРЯЕМ СЕЙЧАС (два СЦЕНАРИЯ, два отдельных теста — предложение @AR 2026-11-16 02:12):
//   (а) «чистый профиль»: локального PIN нет → замка НЕТ, приложение не пускает в журнал через
//       замок: либо онбординг, либо журнал, но `#view-lock` обязан быть скрыт;
//   (б) «PIN на устройстве»: `window.Pin.setPin` → reload → замок ЕСТЬ, неверный PIN даёт
//       `#pin-error`, верный открывает журнал (это же негативный контроль: если замок перестанет
//       появляться при локальном PIN, тест упадёт, а не пропустится).
//
// Применимость НЕ зависит от состояния стенда: после этапа 3 PIN живёт на устройстве, поэтому
// «стенд имеет PIN» не является фактом. Состояние создаёт спека (`window.Pin.setPin` /
// `dropLocalPin`), внешний маркер не нужен (@AR 2026-11-16 02:12). Проба `/privacy/token`
// удалена: по ADR-021 этап 2 в `9-tests` не должно быть `fetch`/`request` в `/privacy/*`.
//
// MVP-истории: US-11
//   US-11 вход по PIN (устройство с PIN, локального нет)
const { test, expect } = require('@playwright/test');
const { readToken, API_BASE, PIN, loginWithPin, reportStep, expectScreen, dumpViews, waitAnyScreen } = require('./api');

async function dropLocalPin(page) {
  await page.evaluate(async () => {
    await new Promise((resolve) => {
      const req = indexedDB.open('xdiary');
      req.onsuccess = () => {
        const db = req.result;
        try {
          const tx = db.transaction('meta', 'readwrite');
          tx.objectStore('meta').delete('pin');
          tx.oncomplete = () => resolve(true);
          tx.onerror = () => resolve(false);
        } catch (e) {
          resolve(false);
        }
      };
      req.onerror = () => resolve(false);
    });
  });
}

/** Ставит локальный PIN через публичный API фронта (без UI: это подготовка состояния). */
async function setLocalPin(page, pin) {
  await page.evaluate((v) => window.Pin.setPin(v), pin);
}

test('E2E: чистый профиль без локального PIN   журнал открыт, замка нет (ADR-021)', async ({ page }) => {
  test.setTimeout(100_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  // Сценарий (а) из решения @AR 2026-11-16 02:12: PIN   свойство устройства, поэтому «стенд имеет PIN»
  // не факт; состояние создаёт сама спека (dropLocalPin). Проба /privacy/token не нужна.
  await loginWithPin(page);
  await reportStep(page, 'шаг 1: сессия получена штатно');
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  // --- утверждение 1: без локального PIN замка НЕТ (ADR-021: PIN локален) ---
  await dropLocalPin(page);
  await reportStep(page, 'шаг 2: локальный PIN удалён, токен и clientId сохранены');
  await page.reload();
  await dumpViews(page, 'TE-053: reload БЕЗ локального PIN');
  await expect(
    page.locator('#view-lock'),
    'без локального PIN замок обязан быть скрыт (ADR-021; на 0.90 app.js:182-188 уходит в онбординг/afterAuth)'
  ).toBeHidden({ timeout: 20000 });
  // В журнал или онбординг — оба допустимы; важно, что не «через замок».
  const onb = page.locator('#view-onboarding');
  const jr = page.locator('#view-journal');
  // TE-072: `.or().first()` упорядочивает по DOM, а не по видимости (см. api.js:waitAnyScreen).
  const shown = await waitAnyScreen(page, ['view-onboarding', 'view-journal'], 20000);
  await reportStep(page, 'без локального PIN показан экран: ' + shown);
  console.log('::notice::без локального PIN: ' + ((await onb.isVisible()) ? 'онбординг' : 'журнал') + ', замка нет');
});

test('E2E: локальный PIN есть   замок, неверный PIN даёт ошибку, верный открывает журнал', async ({ page }) => {
  test.setTimeout(100_000);
  // TE-062: бюджет = сумма объявленных ожиданий (общий лимит теста 90 с не менялся).
  // Лимит поднят ТОЛЬКО для этой спеки; общий 90 с в конфиге не менялся.
  // Сценарий (б): вход штатный, затем PIN ставится на устройстве   reload   замок   проверка PIN.
  await loginWithPin(page);
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
  await setLocalPin(page, PIN);
  await reportStep(page, 'шаг 3: локальный PIN установлен → reload → ожидаем замок');
  await page.reload();
  await dumpViews(page, 'TE-053: reload С локальным PIN');
  await expectScreen(page, '#view-lock', 'с локальным PIN → экран PIN', 20000);
  await expect(page.locator('#pin-input')).toBeVisible({ timeout: 20000 });
  await reportStep(page, 'шаг 4: неверный PIN → остаёмся на экране, видна ошибка');
  await page.fill('#pin-input', '0000');
  // В разметке 0.90 отдельной кнопки «разблокировать» нет: Enter в поле повешен на keydown
  // в `app.js` (`pin-input` + `unlock()`). Селектор не выдумываем.
  await page.press('#pin-input', 'Enter');
  await expect(page.locator('#view-lock')).toBeVisible({ timeout: 20000 });
  await expect(page.locator('#view-journal')).toBeHidden();
  await expect(page.locator('#pin-error')).toBeVisible({ timeout: 20000 });
  await reportStep(page, 'шаг 5: верный PIN → журнал');
  await page.fill('#pin-input', PIN);
  // Явная кнопка «Войти» (`#pin-submit`) не проверялась: раньше вход шёл только по Enter.
  await page.click('#pin-submit');
  await expectScreen(page, '#view-journal', 'верный PIN → журнал', 20000);
  await expect(page.locator('#view-lock')).toBeHidden();
  // Поведенческое утверждение: после разблокировки сессия ЖИВА, а не просто «экран сменился».
  const token = await readToken(page);
  expect(token, 'client-JWT после разблокировки имеет вид header.payload.signature')
    .toMatch(/^[\w-]+\.[\w-]+\.[\w-]+$/);
  const me = await page.evaluate(async (base) => {
    const t = localStorage.getItem('xdiary.accessToken');
    const r = await fetch(base + '/api/v1/clients/me', { headers: { Authorization: 'Bearer ' + t } });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, API_BASE);
  expect(me.status, 'GET /clients/me после разблокировки вернул ' + me.status).toBe(200);
  expect(me.body && me.body.id, '/clients/me вернул id клиента').toBeTruthy();
  await test.step('Забыли PIN? уводит в онбординг (FE-032, локальный PIN сбрасывается)', async () => {
    // TE-066: `#btn-pin-forgot` живёт ТОЛЬКО внутри `#view-lock` (index.html:34). После успешной
    // разблокировки приложение показывает журнал, кнопки нет — `page.click` ждёт её появления до
    // общего лимита теста (в прогоне #770 это дало `Test timeout of 100000ms exceeded`, а снимок
    // `error-context.md` показывал открытый журнал). Поэтому сначала возвращаем замок перезагрузкой.
    await page.reload();
    await expectScreen(page, '#view-lock', 'перед «Забыли PIN?» — замок', 20000);
    await page.click('#btn-pin-forgot');
    await expect(page.locator('#view-onboarding'), 'после «Забыли PIN?» открыт онбординг')
      .toBeVisible({ timeout: 20000 });
  });
});
