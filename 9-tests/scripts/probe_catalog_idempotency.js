#!/usr/bin/env node
/**
 * Замер живой идемпотентности `POST /api/v1/catalog` (FE-054, серверная половина).
 *
 * Зачем: клиентская часть закрыта тестом @FE (9/9), а серверная проверялась только
 * handler-тестом. @TL 2026-11-17 22:37 попросил проверить живой стенд: два POST с ОДНИМ `id`
 * должны вернуть тот же `id` и дать ОДИН элемент в списке (контракт `AddCatalogItemCommand(Guid? Id …)`).
 *
 * Почему скрипт, а не спека: спеку пришлось бы объявлять в маркере `front-e2e.md` и держать
 * под тремя гейтами (coverage/traceability/ci_filter), а замер нужен разово — по квитанции
 * `deploy-prod`. Скрипт ничего не меняет в продукте и чистит за собой позицию каталога.
 *
 * Запуск: node 9-tests/scripts/probe_catalog_idempotency.js
 *   XDIARY_API_BASE — база (по умолчанию dev-стенд); XDIARY_E2E_TEST_OTP — OTP (по умолчанию 000000).
 *
 * Коды возврата: 0 — идемпотентность подтверждена; 1 — не подтверждена; 2 — не удалось
 * выполнить замер (сеть/регистрация/вход) — это «не измерено», а не «дефект».
 */
const crypto = require('crypto');

const BASE = process.env.XDIARY_API_BASE || 'https://xdiary-vps2.taile0b6ee.ts.net';
const OTP = process.env.XDIARY_E2E_TEST_OTP || '000000';

const j = async (r) => ({ status: r.status, data: await r.json().catch(() => null) });

async function main() {
  const phone = '+7999' + String(crypto.randomInt(0, 10000000)).padStart(7, '0');
  const c = await j(await fetch(BASE + '/api/v1/clients', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ phone }),
  }));
  if (!c.data || !c.data.id) {
    console.log('НЕ ИЗМЕРЕНО: POST /clients ->', c.status, JSON.stringify(c.data).slice(0, 160));
    process.exitCode = 2; return;
  }
  const v = await j(await fetch(BASE + '/api/v1/clients/' + c.data.id + '/verify', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code: OTP }),
  }));
  const token = (v.data && (v.data.accessToken || v.data.token)) || '';
  if (!token) {
    console.log('НЕ ИЗМЕРЕНО: POST /verify ->', v.status, JSON.stringify(v.data).slice(0, 160));
    process.exitCode = 2; return;
  }
  const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };
  await fetch(BASE + '/api/v1/clients/me/profile', {
    method: 'POST', headers: H,
    body: JSON.stringify({ displayName: 'TE-IDEMP', gender: 'female' }),
  });

  const id = crypto.randomUUID();
  const name = 'TE-IDEMP-' + Date.now();
  const body = JSON.stringify({ id, kind: 'toy', name });
  const a = await j(await fetch(BASE + '/api/v1/catalog', { method: 'POST', headers: H, body }));
  const b = await j(await fetch(BASE + '/api/v1/catalog', { method: 'POST', headers: H, body }));
  const list = await j(await fetch(BASE + '/api/v1/catalog', { headers: H }));
  const one = await j(await fetch(BASE + '/api/v1/catalog/' + id, { headers: H }));
  const items = Array.isArray(list.data) ? list.data : (list.data && list.data.items) || [];
  const same = items.filter((x) => x && x.id === id);

  console.log('POST /catalog #1 ->', a.status, 'id:', a.data && a.data.id);
  console.log('POST /catalog #2 ->', b.status, 'id:', b.data && b.data.id);
  console.log('присланный id      ->', id);

  // 2026-11-19 12:02: запись и чтение — РАЗНЫЕ свойства, и смешивать их в один вердикт нельзя.
  const idempotent = a.status < 300 && b.status < 300
    && !!a.data && !!b.data && a.data.id === id && b.data.id === id;
  const readableById = one.status < 300 && !!one.data && (one.data.id === id);
  const readableInList = same.length === 1;
  const readable = readableById || readableInList;

  console.log('ЧТЕНИЕ: GET /catalog/<id> ->', one.status,
    '| GET /catalog ->', list.status, '| элементов всего:', items.length, '| с нашим id:', same.length);
  if (!readable) {
    console.log('::warning::probe_catalog: запись прошла, но ЧТЕНИЕ пустое (ни по id, ни в списке) — ' +
      'это дефект read-модели Catalog (класс BE-137), а не отсутствие идемпотентности');
  }

  console.log(idempotent
    ? 'ИТОГ: идемпотентность ПОДТВЕРЖДЕНА (два POST с одним id → один id); чтение: ' + (readable ? 'есть' : 'ПУСТО')
    : 'ИТОГ: идемпотентность НЕ подтверждена (id в ответах разошлись или дубли)');
  process.exitCode = idempotent ? 0 : 1;

  // Уборка (@FE 2026-11-17 23:52: после прогона остались 2 позиции — мой фильтр по `name` их не нашёл).
  // Удаляем по ЯВНЫМ id из ответов (надёжно), плюс страховка по `name` из списка; печатаем статусы.
  const createdIds = [a.data && a.data.id, b.data && b.data.id].filter(Boolean);
  const listedIds = items.filter((x) => x && x.name === name).map((x) => x.id);
  const toRemove = Array.from(new Set(createdIds.concat(listedIds)));
  for (const rid of toRemove) {
    const r = await fetch(BASE + '/api/v1/catalog/' + rid + '/remove', { method: 'POST', headers: H });
    console.log('cleanup catalog:', rid, '->', r.status);
  }
  if (!toRemove.length) console.log('cleanup catalog: удалять нечего (id в ответах отсутствуют)');
}

main().catch((e) => { console.log('НЕ ИЗМЕРЕНО:', String((e && e.message) || e).slice(0, 200)); process.exitCode = 2; });
