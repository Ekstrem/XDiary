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

// TE-107/104: шаги помечаются аннотациями — при падении видно последний пройденный шаг
// (аннотации в CI доступны без лога; так же читали `TE-053`).
const mark = (label) => {
  try {
    test.info().annotations.push({ type: 'step', description: 'T104: ' + label });
  } catch (e) { /* заглушка dryrun может не иметь test.info */ }
};

test('E2E: FE-046 — ошибка API на старте: сплэш уходит за 2 с, экран или ошибка видны', async ({ page }) => {
  await installBase(page);
  // «Сервис сломан, сеть есть»: все запросы к API отвечают 500. Именно так выглядит падение бэка,
  // и именно этот случай в #775 давал залипший сплэш.
  await page.route('**/api/v1/**', (route) =>
    route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"e2e:API-500"}' })
  );

  await reportStep(page, 'старт с API=500 (FE-046 критерий 3)');
  // TE-107/109: `url: about:blank` в #29/#31. НАСТОЯЩАЯ ПРИЧИНА (трасса `43322907200`, @TL 01:49) —
  // не навигация: `#toast` читался без таймаута и ждал до таймаута теста (90 с), Playwright закрывал
  // контекст, и дальше всё выглядело как «документ не открылся». Гипотезу «относительный goto» СНИМАЮ.
  // Абсолютный адрес и проверка документа остаются страховкой, лечит — `{ timeout: 1500 }` в `api.js`.
  const appUrl = process.env.XDIARY_E2E_APP_URL || 'http://127.0.0.1:4173/';
  // Диагноз `#31`/`43301132233`: в trace **ноль действий**, ошибка — «Test timeout of 90000ms
  // exceeded». То есть прогон висел **на навигации**: `goto` ждал документ 90 с. Ставим явный
  // таймаут навигации и печатаем «до/после» в аннотации, чтобы следующее падение называло шаг.
  page.setDefaultNavigationTimeout(15000);
  console.log('::warning::T104: goto -> ' + appUrl);
  let navResp = null;
  try {
    navResp = await page.goto(appUrl, { waitUntil: 'domcontentloaded', timeout: 15000 });
  } catch (e) {
    console.log('::error::T104: goto упал: ' + appUrl + ' ;; ' + String(e && e.message || e).slice(0, 300));
    throw e;
  }
  const navStatus = navResp ? navResp.status() : -1;
  const navViews = await page.evaluate(() => document.querySelectorAll('[id^="view-"]').length)
    .catch(() => -1);
  if (page.url() === 'about:blank' || navViews === 0) {
    await dumpViews(page, 'FE-046: навигация не состоялась (TE-107)');
    // @TL 2026-11-12 23:20: падение должно быть видно в аннотациях как ОШИБКА, а не только как
    // «assert failed» — иначе следующая красная строка снова читается как дефект app.js.
    console.log('::error::навигация не состоялась: url=' + page.url() + ', status=' + navStatus +
      ', #view-*=' + navViews + ' — проверять нечего');
    throw new Error('после goto(' + appUrl + ') url=' + page.url() + ', status=' + navStatus +
      ', #view-*=' + navViews + ' — документ не открылся, проверять нечего');
  }

  // Разбор #27: `views:{}` = в документе НОЛЬ элементов `#view-*`, при этом ассерты «splash скрыт»
  // и «lock скрыт» на пустой странице проходят сами собой. Проверяем документ явно.
  mark('a0 документ: #view-* присутствуют');
  const viewCount = await page.evaluate(() => document.querySelectorAll('[id^="view-"]').length)
    .catch(() => -1);
  // В заглушке dryrun `evaluate` возвращает не число — тогда проверку не измерить, пропускаем.
  // В браузере `0` значит «документ без разметки экранов» — это падение с ясной причиной.
  if (viewCount === 0) {
    await dumpViews(page, 'FE-046: документ без разметки экранов (TE-107)');
    throw new Error('документ не содержит ни одного #view-* (viewCount=' + viewCount +
      '); смотри url/status/htmlLen в аннотации views');
  }

  mark('a1 старт: splash ≤ 2000 мс');
  try {
    await expect(page.locator('#view-splash'), 'заставка не ушла за 2 с при ошибке API (FE-046)')
      .toBeHidden({ timeout: 2000 });
  } catch (e) {
    await dumpViews(page, 'FE-046: экраны при залипшем splash (API=500)');
    throw e;
  }

  // `check_e2e_reload_lock` требует не «слепой» переход после старта: на свежем устройстве PIN нет,
  // значит замок обязан быть скрыт (FE-036). Это утверждение, а не предположение.
  mark('a2 замок скрыт ≤ 5000 мс');
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

  mark('a3 экран или видимая ошибка ≤ 3000 мс');
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
