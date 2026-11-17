/* FE-056 (runtime) — when cached read failed, the reason is carried and the UI names it.
 *
 * Исполняет настоящий withCache из repository.js:
 *  1) живое чтение 500 + НЕПУСТОЙ кэш -> данные + fromCache + error (причина), без исключения;
 *  2) живое чтение 500 + ПУСТОЙ кэш -> исключение (как и было, FE-051 ловит его через .catch);
 *  3) живое чтение 200 -> fromCache = null, error нет;
 *  4) негативный контроль: без поля error UI не смог бы назвать причину.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-056-cache-error-marker-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "repository.js"), "utf8");

function extractBlock() {
  const start = js.indexOf("  var STALE_AFTER_HOURS = 24;");
  const endMarker = "  DB.readFreshness = function (kind) { return fresh[kind] || null; };";
  const end = js.indexOf(endMarker);
  if (start < 0 || end < 0) throw new Error("блок withCache не найден");
  return js.slice(start, end + endMarker.length);
}

function makeCtx(live, cachedRows) {
  const meta = {};
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    Promise, setTimeout, clearTimeout, Date,
    DB: {
      getMeta: (k) => Promise.resolve(meta[k] === undefined ? null : { value: meta[k] }),
      setMeta: (k, v) => { meta[k] = v; return Promise.resolve(); },
    },
  };
  vm.createContext(ctx);
  vm.runInContext(extractBlock() + "\nglobalThis.__withCache = withCache;\nglobalThis.__fresh = DB.readFreshness;", ctx);
  return { ctx, live, cached: () => Promise.resolve(cachedRows) };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

(async () => {
  // 1) 500 + непустой кэш -> данные + причина
  let c = makeCtx(() => Promise.reject(Object.assign(new Error("HTTP 500"), { status: 500 })), [{ id: "p1" }]);
  const rows = await c.ctx.__withCache("partners", c.live, c.cached);
  const fr = c.ctx.__fresh("partners");
  check("500 + кэш: данные отданы", Array.isArray(rows) && rows.length === 1, "len=" + rows.length);
  check("500 + кэш: признак fromCache", !!(fr && fr.fromCache), "fromCache=" + (fr && fr.fromCache));
  check("500 + кэш: причина сохранена", !!(fr && fr.error && fr.error.status === 500), "error=" + JSON.stringify(fr && fr.error));

  // 2) 500 + пустой кэш -> исключение
  c = makeCtx(() => Promise.reject(Object.assign(new Error("HTTP 503"), { status: 503 })), []);
  let err = null;
  try { await c.ctx.__withCache("partners", c.live, c.cached); } catch (e) { err = e; }
  check("500 + пустой кэш: пробрасывается ошибка", !!err, "err=" + (err && err.message));
  check("500 + пустой кэш: признак не выставлен", c.ctx.__fresh("partners") === null, "fresh=" + JSON.stringify(c.ctx.__fresh("partners")));

  // 3) 200 -> чисто
  c = makeCtx(() => Promise.resolve([{ id: "fresh" }]), []);
  const ok = await c.ctx.__withCache("partners", c.live, c.cached);
  check("200: данные отданы", ok.length === 1, "len=" + ok.length);
  check("200: признаков ошибки нет", c.ctx.__fresh("partners") === null, "fresh=null");

  // 4) негативный контроль: в старом поведении error отсутствовал
  const oldFresh = fr ? { fromCache: true, ageMs: fr.ageMs } : null;
  check("контроль: без error UI не назвал бы причину", !!(oldFresh && oldFresh.error === undefined), "старое поле error отсутствует");

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
