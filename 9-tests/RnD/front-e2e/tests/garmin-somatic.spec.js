// E2E (настоящий) — TE-026: соматика/дневные метрики из Garmin (Фаза 3).
// Проверяем, что контракты Garmin → SomaticSeries / DailyMetrics отдают данные на dev:
//   GET /api/v1/somatic-series/slice?fromT=&toT=
//   GET /api/v1/daily-metrics/series?fromDate=&toDate=
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

async function getJson(page, path) {
  return page.evaluate(async ({ base, path }) => {
    const t = localStorage.getItem('xdiary.accessToken') || '';
    const r = await fetch(base + path, { headers: t ? { Authorization: 'Bearer ' + t } : {} });
    let data = null;
    try { data = await r.json(); } catch (e) {}
    return { status: r.status, data };
  }, { base: API_BASE, path });
}

test('E2E: соматика и дневные метрики Garmin отдаются на dev (TE-026)', async ({ page }) => {
  await test.step('login по PIN', async () => {
    await loginWithPin(page);
  });

  await test.step('somatic-series/slice — 200 и массив точек', async () => {
    const fromT = minuteKey(-90 * 24 * 60);
    const toT = minuteKey(0);
    const r = await getJson(page, '/api/v1/somatic-series/slice?fromT=' + encodeURIComponent(fromT) + '&toT=' + encodeURIComponent(toT));
    expect(r.status, 'GET /somatic-series/slice (' + fromT + '..' + toT + ')').toBe(200);
    expect(Array.isArray(r.data), 'срез соматики — массив').toBe(true);
    console.log('::notice::TE-026 somatic-series/slice: ' + r.data.length + ' точек за 90 дней');
  });

  await test.step('daily-metrics/series — 200 и данные за период', async () => {
    const fromDate = dayKey(-30);
    const toDate = dayKey(0);
    const r = await getJson(page, '/api/v1/daily-metrics/series?fromDate=' + encodeURIComponent(fromDate) + '&toDate=' + encodeURIComponent(toDate));
    expect(r.status, 'GET /daily-metrics/series (' + fromDate + '..' + toDate + ')').toBe(200);
    const rows = Array.isArray(r.data) ? r.data : (r.data && r.data.items) || [];
    expect(Array.isArray(rows), 'дневные метрики — список дней').toBe(true);
    console.log('::notice::TE-026 daily-metrics/series: ' + rows.length + ' дней за 30 дней');
  });
});
