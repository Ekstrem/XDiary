#!/usr/bin/env node
/**
 * Замер: обновляется ли read-модель `Journal` после `complete` — то есть доехал ли
 * фикс `BE-137` до СТЕНДА (не только до `main` и не только до `lock`).
 *
 * Зачем: @TL 2026-11-19 09:11 поддержал идею, @DO 2026-11-19 08:53 назвал класс
 * «фикс есть в `main`, но не в образе» вторым по частоте источником ложных выводов
 * «фикс не работает» (4 раза за сутки: `BE-137` жил в `main` с 08:39, а на стенде
 * оставался `303303f` до 09:05).
 *
 * Что проверяет (одна сессия, ~30 с, без БД):
 *   1) `POST /journal/records` → `when` → `partners` → `place` → `complete`;
 *   2) `GET /journal/records/<id>` — `startedAt` не `0001-01-01` (иначе read-модель не обновилась);
 *   3) `GET /journal/recent` — `lastPartner` и `lastPlace` непустые (иначе визард не предзаполнит:
 *      `app.js:881-888` `draft.partner = recent.lastPartner?.name || ""`).
 *
 * Запуск: node 9-tests/scripts/probe_journal_recent.js [--base URL] [--json]
 *   XDIARY_API_BASE — база (по умолчанию dev-стенд); XDIARY_E2E_TEST_OTP — OTP (по умолчанию 000000).
 *
 * Коды возврата: 0 — read-модель обновляется (фикс на стенде);
 *                1 — запись идёт, но read-модель пуста (стенд до фикса / дефект проекции);
 *                2 — не измерено (сеть/регистрация/вход) — это НЕ дефект.
 */
const crypto = require('crypto');

const args = process.argv.slice(2);
const argv = {};
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--base') argv.base = args[++i];
  else if (args[i] === '--json') argv.json = true;
}
const BASE = argv.base || process.env.XDIARY_API_BASE || 'https://xdiary-vps2.taile0b6ee.ts.net';
const OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';
const STARTED_AT = '2026-11-02T05:16';
const PLACE = 'TE-probe-place';

const j = async (r) => ({ status: r.status, data: await r.json().catch(() => null) });
const out = (o) => { if (argv.json) console.log(JSON.stringify(o)); else console.log(o.text); };

async function main() {
  const receipt = { base: BASE, steps: {}, startedAt: null, status: null, recent: null };
  const phone = '+7999' + String(crypto.randomInt(0, 10000000)).padStart(7, '0');

  const c = await j(await fetch(BASE + '/api/v1/clients', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ phone }),
  }));
  if (!c.data || !c.data.id) {
    out({ text: 'НЕ ИЗМЕРЕНО: POST /clients -> ' + c.status + ' ' + JSON.stringify(c.data).slice(0, 160), receipt });
    process.exitCode = 2; return;
  }
  const v = await j(await fetch(BASE + '/api/v1/clients/' + c.data.id + '/verify', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code: OTP }),
  }));
  const token = (v.data && (v.data.accessToken || v.data.token)) || '';
  if (!token) {
    out({ text: 'НЕ ИЗМЕРЕНО: POST /clients/{id}/verify -> ' + v.status + ' (стенд без тестового OTP?)', receipt });
    process.exitCode = 2; return;
  }
  const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

  const partnerName = 'TE-probe-' + Date.now();
  const p = await j(await fetch(BASE + '/api/v1/partners', { method: 'POST', headers: H, body: JSON.stringify({ name: partnerName }) }));
  receipt.steps.partner = p.status;
  if (!p.data || !p.data.id) {
    out({ text: 'НЕ ИЗМЕРЕНО: POST /partners -> ' + p.status, receipt });
    process.exitCode = 2; return;
  }
  const rec = await j(await fetch(BASE + '/api/v1/journal/records', { method: 'POST', headers: H }));
  receipt.steps.record = rec.status;
  const rid = rec.data && rec.data.id;
  if (!rid) {
    out({ text: 'НЕ ИЗМЕРЕНО: POST /journal/records -> ' + rec.status, receipt });
    process.exitCode = 2; return;
  }

  receipt.steps.when = (await fetch(BASE + '/api/v1/journal/records/' + rid + '/when', {
    method: 'POST', headers: H, body: JSON.stringify({ startedAt: STARTED_AT }),
  })).status;
  receipt.steps.partners = (await fetch(BASE + '/api/v1/journal/records/' + rid + '/partners', {
    method: 'POST', headers: H, body: JSON.stringify({ partnerId: p.data.id }),
  })).status;
  receipt.steps.place = (await fetch(BASE + '/api/v1/journal/records/' + rid + '/place', {
    method: 'POST', headers: H, body: JSON.stringify({ place: PLACE }),
  })).status;
  receipt.steps.complete = (await fetch(BASE + '/api/v1/journal/records/' + rid + '/complete', { method: 'POST', headers: H })).status;

  const one = await j(await fetch(BASE + '/api/v1/journal/records/' + rid, { headers: H }));
  receipt.startedAt = one.data && one.data.startedAt;
  receipt.status = one.data && one.data.status;
  // @TL 2026-11-19 09:37: печатаем СОСТАВ записи до вывода про проекцию — иначе «recent пуст»
  // можно спутать с «партнёр не привязался к записи».
  receipt.recordPartners = (one.data && Array.isArray(one.data.partners))
    ? one.data.partners.map((x) => (x && (x.id || x.partnerId)) || String(x))
    : null;
  receipt.recordPlace = one.data ? (one.data.place || null) : null;
  const recent = await j(await fetch(BASE + '/api/v1/journal/recent', { headers: H }));
  receipt.recent = recent.data ? {
    lastPartner: recent.data.lastPartner || null,
    lastPlace: recent.data.lastPlace || null,
  } : null;

  const linkedNone = Array.isArray(receipt.recordPartners) && receipt.recordPartners.length === 0;
  if (linkedNone) {
    console.log('ВНИМАНИЕ: в записи нет партнёров (partners=[]) ⇒ `recent.lastPartner` пуст по этой ' +
      'причине, а не из-за проекции; проверь тело POST …/partners {partnerId}');
  }
  const zeroStart = !receipt.startedAt || String(receipt.startedAt).indexOf('0001-01-01') === 0;
  const hasPartner = !!(receipt.recent && receipt.recent.lastPartner);
  const hasPlace = !!(receipt.recent && recent.data && recent.data.lastPlace);
  const ok = !zeroStart && hasPartner && hasPlace;

  out({
    text: [
      'квитанция: base ' + BASE,
      '  шаги: ' + JSON.stringify(receipt.steps),
      '  GET /journal/records/<id>: startedAt=' + receipt.startedAt + ' status=' + receipt.status,
      '    состав записи: partners=' + JSON.stringify(receipt.recordPartners) +
        ' place=' + JSON.stringify(receipt.recordPlace),
      '  GET /journal/recent: lastPartner=' + JSON.stringify(receipt.recent && receipt.recent.lastPartner) +
        ' lastPlace=' + JSON.stringify(receipt.recent && receipt.recent.lastPlace),
      ok
        ? 'ИТОГ: read-модель Journal обновляется (фикс BE-137 на стенде)'
        : 'ИТОГ: read-модель Journal НЕ обновляется (стенд до фикса или дефект проекции)',
    ].join('\n'),
    receipt,
  });
  process.exitCode = ok ? 0 : 1;

  // Уборка: доводить до `complete` уже довели, удаляем запись и партнёра; печатаем статусы.
  const del = await fetch(BASE + '/api/v1/journal/records/' + rid + '/delete', { method: 'POST', headers: H });
  console.log('cleanup record:', rid, '->', del.status);
  const rem = await fetch(BASE + '/api/v1/partners/' + p.data.id + '/remove', { method: 'POST', headers: H });
  console.log('cleanup partner:', p.data.id, '->', rem.status);
}

main().catch((e) => {
  console.log('НЕ ИЗМЕРЕНО:', String((e && e.message) || e).slice(0, 200));
  process.exitCode = 2;
});
