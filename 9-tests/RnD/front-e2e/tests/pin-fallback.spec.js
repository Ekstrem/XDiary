// E2E — TE-053: путь «на стенде PIN есть, у устройства локального PIN НЕТ» (FE-007).
//
// Постановка от @FE (`chat/fe.md` `[2026-11-03 08:01]`): «на стенде PIN есть, у устройства НЕТ
// локального PIN → `login("")` → 401 → экран PIN → `login(pin)` → журнал». До этой спеки путь
// **не проверялся как утверждение**: `no-pin.spec.js` целиком в `test.describe.skip` (legacy
// FE-025/026 + ADR-021), а `pin-first-login.spec.js` на шаге после reload **допускал три исхода**
// (`lock.or(onb).or(jr)`), то есть «замок не появился» проходило как успех.
//
// Здесь допустимый исход ровно один. Применимость сценария определяется **внешним фактом** —
// ответом стенда на пустой PIN, а не поведением UI: 401 = PIN на стенде задан (сценарий применим),
// 200 = стенда без PIN (тест честно пропускается), иное — падаем (неизвестное состояние не «пропускаем»).
//
// Негативный контроль (критерий приёмки TE-053 п.3): если экран PIN перестанет появляться,
// тест обязан **упасть**, а не пропуститься. Проверяется отдельно — временным снятием `#view-lock`.
const { test, expect } = require('@playwright/test');
const { API_BASE, PIN, loginWithPin, reportStep, expectScreen, dumpViews } = require('./api');

/** Внешний факт: задан ли PIN на стенде. `true` — задан (пустой PIN даёт 401). */
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

test('E2E: стенд с PIN, на устройстве локального PIN нет → экран PIN → верный PIN → журнал', async ({ page, request }) => {
  const stand = await standHasPin(request);
  test.skip(
    stand.has === false,
    'стенд без PIN (пустой PIN → 200): сценарий «есть PIN на стенде, нет локально» неприменим'
  );
  expect(stand.has, 'неизвестное состояние стенда: POST /privacy/token {pin:""} → ' + stand.status).toBe(true);

  // Сессия и локальный PIN появляются штатно, затем PIN с устройства убираем — это и есть
  // «то же устройство, но локального PIN нет» (а не первый вход).
  await loginWithPin(page);
  await reportStep(page, 'шаг 1: сессия получена штатно (PIN был локально)');
  await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });

  await dropLocalPin(page);
  await reportStep(page, 'шаг 2: локальный PIN удалён, токен и clientId сохранены');
  await page.reload();
  await dumpViews(page, 'TE-053: reload без локального PIN при PIN на стенде');

  // ЕДИНСТВЕННЫЙ допустимый исход — экран PIN. Ни `onboarding`, ни `journal` здесь не «ок»:
  // при `journal` клиент попал в данные без локального подтверждения, при `onboarding` —
  // потерял сессию. Оба случая = дефект, и тест обязан это назвать.
  await expectScreen(page, '#view-lock', 'без локального PIN при PIN на стенде → экран PIN', 20000);
  await expect(page.locator('#pin-input')).toBeVisible({ timeout: 20000 });

  await reportStep(page, 'шаг 3: неверный PIN → остаёмся на экране, видна ошибка');
  await page.fill('#pin-input', '0000');
  // В разметке 0.87 отдельной кнопки «разблокировать» нет: Enter в поле
  // повешен на keydown в app.js (`pin-input` + `unlock()`). Селектор не выдумываем.
  await page.press('#pin-input', 'Enter');
  await expect(page.locator('#view-lock')).toBeVisible({ timeout: 20000 });
  await expect(page.locator('#view-journal')).toBeHidden();
  await expect(page.locator('#pin-error')).toBeVisible({ timeout: 20000 });

  await reportStep(page, 'шаг 4: верный PIN → журнал');
  await page.fill('#pin-input', PIN);
  await page.press('#pin-input', 'Enter');
  await expectScreen(page, '#view-journal', 'верный PIN → журнал', 20000);
  await expect(page.locator('#view-lock')).toBeHidden();
});
