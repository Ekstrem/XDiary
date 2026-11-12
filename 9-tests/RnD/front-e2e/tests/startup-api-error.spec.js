// TE-104 (критерий 3 карточки FE-046): «подменить API на 500 → `#view-onboarding` / `#view-journal` показываются
// (или видимая ошибка), сплэш не висит».
//
// Почему это отдельная спека, а не строка в `onboarding.spec.js`: там проверяется, что сплэш уходит
// на **здоровом** стенде (лимит 20 с), а карточка требует другого — поведение при **ошибке сервиса**:
// сеть есть, а API отвечает 500. Это разные дефекты: «залип на загрузке из-за медленного ответа» и
// «залип, потому что ошибку не обработали». Симптом из карточки (8 из 19 падений E2E, `cb57caa4`) —
// как раз второй.
//
// Три утверждения (все — из критерия карточки, не из вкуса):
//   1. заставка `#view-splash` уходит не позже **2 с** — иначе пользователь видит «вечную загрузку»;
//   2. после этого виден **экран** (`#view-onboarding`/`#view-journal`) **или** видимая ошибка
//      (текст вида «не удалось/ошибка/нет связи/повторить») — «пусто и тихо» критерий не проходит;
//   3. при провале печатается состояние **всех** экранов (`dumpViews`), иначе разбор начнётся с нуля.
//
// Прогон идёт по `XDIARY_E2E_BASE` (как остальные спеки); API подменяется на уровне сети через `page.route`,
// поэтому стенд может быть здоровым — проверяется обработка ошибки, а не состояние сервера.
const { test, expect } = require('@playwright/test');
const { installBase, reportStep, dumpViews } = require('./api');

test('E2E: FE-046 — ошибка API на старте: сплэш уходит за 2 с, экран или ошибка видны', async ({ page }) => {
  await installBase(page);
  // «Сервис сломан, сеть есть»: все запросы к API отвечают 500. Именно так выглядит падение бэка,
  // и именно этот случай в #775 давал залипший сплэш.
  await page.route('**/api/v1/**', (route) =>
    route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"e2e:API-500"}' })
  );

  await reportStep(page, 'старт с API=500 (FE-046 критерий 3)');
  await page.goto('/');

  try {
    await expect(page.locator('#view-splash'), 'заставка не ушла за 2 с при ошибке API (FE-046)')
      .toBeHidden({ timeout: 2000 });
  } catch (e) {
    await dumpViews(page, 'FE-046: экраны при залипшем splash (API=500)');
    throw e;
  }

  // `check_e2e_reload_lock` требует не «слепой» переход после старта: на свежем устройстве PIN нет,
  // значит замок обязан быть скрыт (FE-036). Это утверждение, а не предположение.
  await expect(page.locator('#view-lock'), 'на свежем устройстве показан замок (FE-036)')
    .toBeHidden({ timeout: 5000 });

  const onboarding = page.locator('#view-onboarding');
  const journal = page.locator('#view-journal');

  // «Видимая ошибка»: берём ВСЕ совпадения по тексту и проверяем, что хотя бы одно видимо.
  // `.first()` здесь врал бы: `#pin-error` и `#toast` живут с классом `hidden`, и первый по DOM узел
  // может быть скрыт, пока настоящий текст ошибки виден в другом месте.
  const errorMatches = page.getByText(/не удалось|ошибк|нет связи|повторить/i);

  // Два числа критерия (решение @TL `2026-11-13 11:00`, вариант «б»): заставка ≤ 2 000 мс,
  // а экран/ошибка — ≤ 3 000 мс. Ожидание, а не «мгновенный снимок»: иначе спека требует появления
  // ошибки в тот же тик и падает на легальной задержке.
  const screenOrError = async () => {
    if (await onboarding.isVisible().catch(() => false)) return true;
    if (await journal.isVisible().catch(() => false)) return true;
    const n = await errorMatches.count().catch(() => 0);
    for (let i = 0; i < n; i += 1) {
      if (await errorMatches.nth(i).isVisible().catch(() => false)) return true;
    }
    return false;
  };

  try {
    await expect.poll(screenOrError, {
      timeout: 3000,
      message: 'за 3 с после скрытия заставки не показан ни экран, ни видимая ошибка (FE-046)',
    }).toBe(true);
  } catch (e) {
    await dumpViews(page, 'FE-046: после ошибки API не видно ни экрана, ни ошибки за 3 с');
    throw e;
  }
});
