// E2E — TE-053: что происходит с замком, когда PIN есть **на стенде**, а локального PIN нет.
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
// ЧТО ПРОВЕРЯЕМ СЕЙЧАС (два утверждения, оба про инвариант, а не про старый UX):
//   1. НЕТ локального PIN (при PIN на стенде) → замка НЕТ, приложение не пускает в журнал
//      «через замок»: либо онбординг, либо журнал — но `#view-lock` обязан быть скрыт;
//   2. ПОСЛЕ установки локального PIN (`window.Pin.setPin`) reload → замок ЕСТЬ, неверный PIN
//      даёт `#pin-error`, верный — открывает журнал.
// Второе — это и есть «fallback»-путь в текущей модели, и оно же негативный контроль:
// если замок перестанет появляться при локальном PIN, тест **упадёт** (а не пропустится).
//
// Применимость по-прежнему определяется **внешним фактом** (ответ стенда на пустой PIN), но
// теперь это факт только про стенд: 401 = на стенде PIN задан (сценарий информативен),
// 200 = на стенде PIN нет (сценарий тоже валиден — см. п.1), иное → падаем.
// ВАЖНО (TE-055): на этапе 3 `FE-042` маршрут `/api/v1/privacy/token` удаляется вместе с
// device-токеном — тогда эту пробу нужно заменить внешним признаком (`XDIARY_STAND_HAS_PIN=1`).
const { test, expect } = require('@playwright/test');
const { readToken, API_BASE, PIN, loginWithPin, reportStep, expectScreen, dumpViews } = require('./api');

/** Внешний факт: задан ли PIN на стенде. `null` — состояние неизвестно (падаем, не пропускаем). */
async function standHasPin(request) {
  const r = await request.post(API_BASE + '/api/v1/privacy/token', { data: { pin: '' } });
  if (r.status() === 401) return { has: true, status: 401 };
  if (r.status() === 200) return { has: false, status: 200 };
  return { has: null, status: r.status() };
}

/** Убирает локальный PIN из IndexedDB (`xdiary`/`meta`.`pin`), не трогая токен и clientId. */
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

test('E2E: без локального PIN замка нет; с локальным PIN замок появляется и проверяется', async ({ page, request }) => {
  const stand = await standHasPin(request);
  expect(
    stand.has,
    'неизвестное состояние стенда: POST /privacy/token {pin:""} → ' + stand.status + ' (не «пропускаем»)'
  ).not.toBeNull();
  console.log('::notice::стенд: PIN ' + (stand.has ? 'задан' : 'не задан') + ' (ответ ' + stand.status + ')');

  // Сессия получается штатно (адаптивная фикстура: PIN не нужен, потому что на 0.90 он не первичен).
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
  await expect(onb.or(jr).first()).toBeVisible({ timeout: 20000 });
  console.log('::notice::без локального PIN: ' + ((await onb.isVisible()) ? 'онбординг' : 'журнал') + ', замка нет');

  // --- утверждение 2 (негативный контроль): с локальным PIN замок появляется и работает ---
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
  expect(token, 'client-JWT на устройстве после разблокировки').toBeTruthy();
  const me = await page.evaluate(async (base) => {
    const t = localStorage.getItem('xdiary.accessToken');
    const r = await fetch(base + '/api/v1/clients/me', { headers: { Authorization: 'Bearer ' + t } });
    return { status: r.status, body: await r.json().catch(() => null) };
  }, API_BASE);
  expect(me.status, 'GET /clients/me после разблокировки вернул ' + me.status).toBe(200);
  expect(me.body && me.body.id, '/clients/me вернул id клиента').toBeTruthy();
});
