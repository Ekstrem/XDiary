/* FE-050 — воспроизводимый тест гонки мемо списка партнёров (v2).
 * Читает РЕАЛЬНЫЙ 8-front/js/repository.js через vm; негативный контроль получаем
 * детерминированной заменой «сброс на успехе» на пустышку (settle = identity).
 * Запуск: node 5-tech-backlog/front-end/FE-050-partners-memo-race-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const src = fs.readFileSync(path.join(root, "8-front", "js", "repository.js"), "utf8");

// Негативный контроль: ровно то же, но settle не сбрасывает мемо.
const srcOld = src.replace(
  /var settle = function \(v\) \{ dropPartnersMemo\(\); return v; \};.*/,
  "var settle = function (v) { return v; };"
);
if (srcOld === src) { console.error("FAIL: не удалось построить негативный контроль"); process.exit(1); }

function makeCtx() {
  const calls = { list: 0, register: 0 };
  let partners = [{ id: "p-seed", name: "T" }];
  let pending = null;

  const Api = {
    config: { token: () => "tok" },
    partners: {
      list: () => { calls.list++; return Promise.resolve(partners.map((p) => ({ id: p.id, name: p.name }))); },
      register: (name) => {
        calls.register++;
        return new Promise((resolve) => {
          pending = () => { partners = partners.concat([{ id: "p-new", name }]); resolve({ id: "p-new" }); };
        });
      },
      rename: (id, name) => { partners = partners.map((p) => (p.id === id ? { id, name } : p)); return Promise.resolve(); },
      remove: (id) => { partners = partners.filter((p) => p.id !== id); return Promise.resolve(); },
      profile: () => Promise.resolve(),
      linkClient: () => Promise.resolve(),
    },
  };

  const ctx = {
    console: { log: () => {}, error: () => {}, warn: () => {} },
    Promise, setTimeout, clearTimeout, Date,
    window: {}, localStorage: { getItem: () => null, setItem: () => {}, removeItem: () => {} },
    Api,
  };
  ctx.window.DB = { rawPartners: () => Promise.resolve([]), rawJournal: () => Promise.resolve([]), rawCatalog: () => Promise.resolve([]) };
  vm.createContext(ctx);
  return { ctx, calls, flush: () => pending && pending() };
}

async function scenario(code) {
  const { ctx, calls, flush } = makeCtx();
  vm.runInContext(code, ctx);
  const DB = ctx.window.DB;

  await DB.getAllPartners();                 // мемо = [seed]
  const putP = DB.putPartner({ name: "TE-NEW" });
  const during = await DB.getAllPartners();  // чужой рендер: мемо освежается СТАРЫМ списком
  flush();
  await putP;
  const after = await DB.getAllPartners();   // после успеха: должно быть 2, если мемо сброшено
  return { during: during.length, after: after.length, list: calls.list };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

(async () => {
  const fresh = await scenario(src);
  check("НОВОЕ: во время POST список ещё старый", fresh.during === 1, "during=" + fresh.during);
  check("НОВОЕ: после putPartner виден созданный", fresh.after === 2, "after=" + fresh.after + ", list=" + fresh.list);

  const old = await scenario(srcOld);
  check("контроль: старое поведение теряет созданного", old.after === 1, "after=" + old.after + ", list=" + old.list);

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
