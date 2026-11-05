// Трассировка: TE-050 (read-путь и паритет `partners_read`, критерий @BE-071).
// E2E — `BE-071` / `read-paths.md`: **read-модель не должна быть пустой при живых данных**.
//
// Зачем этот сценарий существует (2026-11-05, @TE по разбору @TL `08:22`):
// `partners_read` / `journal_read` заполняются **только** проекцией событий, бэкфилла в репозитории
// нет. Значит после выкатки `GET /partners` может вернуть **`200` с пустым списком** — и критерий
// «эндпоинт отвечает 200» пройдёт **на пустоте**, то есть «зелено» и «данных нет» снова сольются.
// Поэтому проверяем не код ответа, а **содержимое**: создали партнёра → он должен появиться в
// read-модели ПОСЛЕ успешного создания (контракт @BE `08:46`: проекция синхронная, `200` ⇒ строка уже есть;
// соответствовать тому, что видит приложение.
//
// Включается признаком `XDIARY_READPARITY=1` (как `wizard-offline` включается `XDIARY_READPATH_OFFLINE`),
// чтобы не менять поведение обычных прогонов: сценарий требует живого бэкенда с фиксом `partners_read`
// (BE-068/`e7a9bd30`) и осмыслен только после выкатки.
const { test, expect } = require('@playwright/test');
const { API_BASE, ACCESS_KEY, loginWithPin, readToken, removeByName, reportStep, waitToken } = require('./api');

const ENABLED = process.env.XDIARY_READPARITY === '1';
const POLL_MS = Number(process.env.XDIARY_READPARITY_POLL_MS || 1000);
const TIMEOUT_MS = Number(process.env.XDIARY_READPARITY_TIMEOUT_MS || 30000);

// Страховочное окно ожидания: контракт синхронный, поэтому первая проверка идёт сразу, а ретрай
// нужен только чтобы инфраструктурная задержка не давала ложный красный (о ней сообщаем отдельно).
function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

test('E2E: read-модель партнёров не пуста при живых данных (BE-071)', async ({ page }) => {
  test.skip(
    !ENABLED,
    'pending:BE-071 — нужен конфиг с фиксом partners_read; включается XDIARY_READPARITY=1'
  );

  const name = 'TE-PARITY-' + Date.now();
  let token = '';
  try {
    await loginWithPin(page);
    await expect(page.locator('#view-journal')).toBeVisible({ timeout: 20000 });
    await reportStep(page, 'шаг 1: сессия получена');

    // Ключ токена не дублируем в спеке: `waitToken` ждёт его появления (и даёт диагностику, если
    // JWT не пришёл), а `readToken` читает тот же ключ из одной константы `api.js:ACCESS_KEY`.
    // История: 2026-11-05 спека читала несуществующий `xdiary.token` и падала ДО проверки паритета
    // (@DO, REF=7bf8c3e9) — поэтому теперь ключ живёт в одном месте тестовой обвязки.
    await waitToken(page);
    await reportStep(page, 'шаг 1.1: JWT в localStorage появился');
    token = await readToken(page);
    expect(token, 'нет токена сессии (`' + ACCESS_KEY + '`) — дальше проверять нечего').not.toBe('');

    // 1. Создаём партнёра через тот же API, что и приложение (не через UI — нам нужен контроль времени).
    const created = await page.evaluate(
      async ({ base, tok, nm }) => {
        const r = await fetch(base + '/api/v1/partners', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok },
          body: JSON.stringify({ name: nm, gender: 'female', photo: '', notes: '' }),
        });
        return { status: r.status, body: await r.text() };
      },
      { base: API_BASE, tok: token, nm: name }
    );
    await reportStep(page, 'шаг 2: POST /partners → ' + created.status);
    expect([200, 201], 'создание партнёра не удалось: ' + created.status + ' ' + created.body.slice(0, 200))
      .toContain(created.status);

    // 2. Ждём появления в read-модели. По контракту @BE (`08:46`) проекция **синхронная**:
    // `POST /partners` → `SaveAsync` → `PublishAsync` → `PartnerEventHandler.Handle` → `UpsertAsync`
    // (`GetAwaiter().GetResult`) внутри запроса, то есть `200/201` уже означает «строка в `partners_read`».
    // Поэтому первая проверка идёт **без задержки**, а ретрай оставлен только как страховка от
    // инфраструктурной задержки — и если он понадобился, это видно в отчёте шага (контракт стал async?).
    const startedAt = Date.now();
    const deadline = startedAt + TIMEOUT_MS;
    let last = { matched: false, count: 0, sample: '', status: 0 };
    let attempts = 0;
    while (Date.now() < deadline) {
      attempts += 1;
      const got = await page.evaluate(
        async ({ base, tok }) => {
          const r = await fetch(base + '/api/v1/partners', { headers: { Authorization: 'Bearer ' + tok } });
          const txt = await r.text();
          let count = -1;
          try {
            const j = JSON.parse(txt);
            count = Array.isArray(j) ? j.length : (j && typeof j === 'object' ? 1 : -1);
          } catch (e) {
            count = -1;
          }
          return { status: r.status, count, text: txt.slice(0, 300) };
        },
        { base: API_BASE, tok: token }
      );
      const matched = got.status === 200 && got.count > 0 && got.text.includes(name);
      last = { matched, count: got.count, sample: got.text, status: got.status };
      if (matched) break;
      await sleep(POLL_MS);
    }
    const waitedMs = Date.now() - startedAt;

    await reportStep(
      page,
      'шаг 3: read-модель — status=' + last.status + ', count=' + last.count +
        ', имя в ответе=' + last.matched + ', попыток=' + attempts + ', ждали=' + waitedMs + ' мс'
    );
    if (last.matched && waitedMs > 2000) {
      // Не красный: страховка отработала. Но контракт «200 ⇒ строка уже есть» перестал быть синхронным.
      console.warn(
        '::warning::проекция partners_read ответила не сразу: ждали ' + waitedMs + ' мс при контракте ' +
        '"POST 200 ⇒ строка уже в partners_read" (@BE 08:46) — проверить, не стала ли проекция асинхронной'
      );
    }

    // 3. Главное утверждение: не «200», а «200 и данные на месте».
    expect(
      last.matched,
      'read-модель не содержит созданного партнёра за ' + TIMEOUT_MS + ' мс (' +
        'status=' + last.status + ', count=' + last.count + '): ' +
        'это либо проекция не догнала, либо бэкфилл/маппинг не работает (BE-071). Ответ: ' +
        last.sample
    ).toBeTruthy();
  } finally {
    const r = await removeByName(page, 'partner', name);
    console.log('cleanup partner:', JSON.stringify(r));
  }
});
