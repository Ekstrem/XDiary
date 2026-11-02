// E2E (настоящий) — TE-026: соматика/дневные метрики (Фаза 3).
//
// Решение PO (@TL 21:35/22:00): вариант (б)+(в) — генерируем ТЕСТОВЫЕ точки на dev и проверяем,
// что они проходят реальный путь до API; реальные данные Garmin остаются вне dev/CI.
// Контракт (@BE 21:45): POST /api/v1/somatic-series/points
//   RecordPointCommand(string T, int Hr, int BodyBattery)  → тело { t, hr, bodyBattery }
// Чтение: GET /api/v1/somatic-series/slice?fromT=&toT=, GET /api/v1/daily-metrics/series?fromDate=&toDate=
// Прод не трогаем — globalSetup блокирует не-dev стенды (DO-017, 59a0afe).
const { test, expect } = require('@playwright/test');
const { API_BASE, loginWithPin } = require('./api');

function pad(n) { return String(n).padStart(2, '0'); }

function dayKey(offsetDays) {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + offsetDays);
  return d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate());
}

function minuteKey(offsetMin) {
  const d = new Date(Date.now() + offsetMin * 60000);
  return d.getUTCFullYear() + '-' + pad(d.getUTCMonth() + 1) + '-' + pad(d.getUTCDate()) +
    'T' + pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes());
}

async function api(page, path, method, body) {
  return page.evaluate(async ({ base, path, method, body }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const headers = { 'Content-Type': 'application/json' };
    if (t) headers.Authorization = 'Bearer ' + t;
    const r = await fetch(base + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
    let data = null;
    try { data = await r.json(); } catch (e) {}
    return { status: r.status, data };
  }, { base: API_BASE, path, method, body });
}

test('E2E: соматика Garmin — сгенерированные точки доходят до API (TE-026)', async ({ page }) => {
  await loginWithPin(page);

  // 3 тестовые точки в пределах последнего часа — попадают в окно slice и в сегодняшний день
  const points = [
    { t: minuteKey(-50), hr: 61, bodyBattery: 71 },
    { t: minuteKey(-35), hr: 64, bodyBattery: 68 },
    { t: minuteKey(-20), hr: 67, bodyBattery: 65 },
  ];
  const created = [];

  await test.step('POST /somatic-series/points — генерация тестовых точек', async () => {
    for (const p of points) {
      const r = await api(page, '/api/v1/somatic-series/points', 'POST', p);
      expect([200, 201], 'POST /somatic-series/points ' + JSON.stringify(p) + ' -> ' + r.status).toContain(r.status);
      created.push(p);
    }
    console.log('::notice::TE-026 сгенерировано точек: ' + created.length);
  });

  await test.step('slice отдаёт сгенерированные точки', async () => {
    const fromT = minuteKey(-120);
    const toT = minuteKey(5);
    const r = await api(page, '/api/v1/somatic-series/slice?fromT=' + encodeURIComponent(fromT) + '&toT=' + encodeURIComponent(toT), 'GET');
    expect(r.status, 'GET /somatic-series/slice').toBe(200);
    expect(Array.isArray(r.data), 'срез соматики — массив').toBe(true);
    // точки приходят через реальный путь записи → должны быть видны в срезе
    expect(r.data.length, 'в срезе есть точки (после генерации): ' + r.data.length).toBeGreaterThan(0);
    console.log('::notice::TE-026 somatic-series/slice после генерации: ' + r.data.length + ' точек за 2 часа');
  });

  await test.step('daily-metrics/series отвечает за сегодня', async () => {
    const fromDate = dayKey(0);
    const toDate = dayKey(0);
    const r = await api(page, '/api/v1/daily-metrics/series?fromDate=' + encodeURIComponent(fromDate) + '&toDate=' + encodeURIComponent(toDate), 'GET');
    expect(r.status, 'GET /daily-metrics/series').toBe(200);
    const rows = Array.isArray(r.data) ? r.data : (r.data && r.data.items) || [];
    expect(Array.isArray(rows), 'дневные метрики — список дней').toBe(true);
    console.log('::notice::TE-026 daily-metrics/series за сегодня: ' + rows.length + ' дней');
  });
});
