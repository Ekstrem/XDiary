/* FE-044 (часть 2, runtime) — withCache из 8-front/js/repository.js.
 *
 * Дополняет FE-055 (showStaleNote 6/6): проверяет СЛОЙ ВЫШЕ — когда данные помечаются fromCache.
 * Ожидаемо по коду repository.js:153-168:
 *  1) живое чтение успешно -> данные отданы, признак fromCache НЕ выставлен;
 *  2) живое чтение упало, в кэше есть строки -> отданы кэш-строки + fromCache=true + ageMs по syncAt;
 *  3) живое чтение упало, кэш пуст -> ошибка пробрасывается (fromCache остаётся null) —
 *     именно поэтому бейдж stale-note не появляется, если кэш пуст (наш вывод в прогоне 43863805161);
 *  4) негативный контроль: при успехе живого чтения fromCache НЕ должен выставляться.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-044-withcache-runtime-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "repository.js"), "utf8");

function extractBlock(fromMarker, toMarker) {
  const start = js.indexOf(fromMarker);
  if (start < 0) throw new Error("не найден блок: " + fromMarker);
  const end = js.indexOf(toMarker, start + fromMarker.length);
  if (end < 0) throw new Error("не найден конец блока: " + toMarker);
  return js.slice(start, end + toMarker.length);
}

// вырезаем ровно блок кэша: переменные STALE_AFTER_HOURS..fresh, syncKey/markSync/withCache/DB.readFreshness
const block = extractBlock("  var STALE_AFTER_HOURS = 24;", "  DB.readFreshness = function (kind) { return fresh[kind] || null; };");

function makeCtx(opts) {
  const meta = opts.meta || {};          // что лежит в DB.getMeta
  const cachedRows = opts.cached || [];  // что лежит в кэше
  const live = opts.live;                // функция живого чтения
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    Promise, setTimeout, clearTimeout, Date,
    DB: {
      getMeta: (k) => Promise.resolve(meta[k] === undefined ? null : { value: meta[k] }),
      setMeta: (k, v) => { meta[k] = v; return Promise.resolve(); },
    },
  };
  vm.createContext(ctx);
  vm.runInContext(block + "\nglobalThis.__withCache = withCache;\nglobalThis.__readFreshness = DB.readFreshness;", ctx);
  return { ctx, live, cached: () => Promise.resolve(cachedRows), meta };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

(async () => {
  // 1) живое чтение успешно -> fromCache не выставлен
  let c = makeCtx({ live: () => Promise.resolve([{ id: "a" }]), cached: [] });
  let v = await c.ctx.__withCache("partners", c.live, c.cached);
  check("успех: данные отданы", Array.isArray(v) && v.length === 1, "len=" + v.length);
  check("успех: fromCache не выставлен", c.ctx.__readFreshness("partners") === null, "freshness=" + JSON.stringify(c.ctx.__readFreshness("partners")));

  // 2) живое чтение упало, кэш есть -> fromCache + ageMs
  const old = Date.now() - 48 * 3600000;
  let c2 = makeCtx({ live: () => Promise.reject(Object.assign(new Error("HTTP 500"), { status: 500 })), cached: [{ id: "b" }], meta: { "syncAt.partners": old } });
  let v2 = await c2.ctx.__withCache("partners", c2.live, c2.cached);
  const fr = c2.ctx.__readFreshness("partners");
  check("кэш есть: отданы кэш-строки", Array.isArray(v2) && v2.length === 1, "len=" + v2.length);
  check("кэш есть: fromCache=true", !!fr && fr.fromCache === true, "freshness=" + JSON.stringify(fr));
  check("кэш есть: ageMs ≈ 48 ч", !!fr && Math.abs(fr.ageMs - 48 * 3600000) < 5 * 60000, "ageMs=" + (fr && Math.round(fr.ageMs / 3600000) + " ч"));

  // 3) живое чтение упало, кэш пуст -> ошибка пробрасывается, fromCache не выставлен
  let c3 = makeCtx({ live: () => Promise.reject(Object.assign(new Error("HTTP 500"), { status: 500 })), cached: [] });
  let err = null;
  try { await c3.ctx.__withCache("partner", c3.live, c3.cached); } catch (e) { err = e; }
  check("пустой кэш: ошибка пробрасывается", !!err && /HTTP 500/.test(String(err.message)), "err=" + (err && err.message));
  check("пустой кэш: fromCache не выставлен", c3.ctx.__readFreshness("partner") === null, "freshness=" + JSON.stringify(c3.ctx.__readFreshness("partner")));

  // 4) негативный контроль: успех не должен ставить fromCache даже после прошлого кэш-прохода
  let c4 = makeCtx({ live: () => Promise.resolve([{ id: "c" }]), cached: [{ id: "old" }], meta: { "syncAt.catalog": old } });
  await c4.ctx.__withCache("catalog", () => Promise.reject(new Error("fail")), () => Promise.resolve([{ id: "old" }]));
  const marked = c4.ctx.__readFreshness("catalog");
  await c4.ctx.__withCache("catalog", () => Promise.resolve([{ id: "fresh" }]), () => Promise.resolve([]));
  const after = c4.ctx.__readFreshness("catalog");
  check("контроль: после успеха признак снимается", !!marked && after === null, "было=" + JSON.stringify(marked) + ", стало=" + JSON.stringify(after));

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
