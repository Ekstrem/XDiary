/* FE-055 (runtime) — черновик акта: сохранение до отправки, очистка только после 2xx,
 * сохранение id созданной записи (повтор без дубля).
 *
 * Исполняет РЕАЛЬНЫЕ хелперы из app.js (readRecordDraft/writeRecordDraft/patchRecordDraftId/clearRecordDraft)
 * поверх localStorage-заглушки:
 *  1) черновик пишется и читается обратно;
 *  2) id, полученный от сервера, фиксируется в черновике (повтор пойдёт в ту же запись);
 *  3) очистка удаляет черновик;
 *  4) негативный контроль: без записи id повтор не смог бы отличить «создать» от «продолжить».
 *
 * Запуск: node 5-tech-backlog/front-end/FE-055-draft-runtime-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

function extract(fnName) {
  const start = js.indexOf("  function " + fnName + "(");
  if (start < 0) throw new Error("не найдена " + fnName);
  const next = js.indexOf("\n  function ", start + 10);
  return js.slice(start, next);
}

function makeCtx() {
  const store = {};
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    Promise, setTimeout, clearTimeout, Date, JSON,
    localStorage: {
      getItem: (k) => (k in store ? store[k] : null),
      setItem: (k, v) => { store[k] = String(v); },
      removeItem: (k) => { delete store[k]; },
    },
  };
  vm.createContext(ctx);
  const code = "var RECORD_DRAFT_KEY = \"xdiary.recordDraft\";\n" +
    extract("readRecordDraft") + "\n" + extract("writeRecordDraft") + "\n" +
    extract("patchRecordDraftId") + "\n" + extract("clearRecordDraft") + "\n" +
    "globalThis.__api = { read: readRecordDraft, write: writeRecordDraft, patchId: patchRecordDraftId, clear: clearRecordDraft };";
  vm.runInContext(code, ctx);
  return { api: ctx.__api, store };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

// 1) запись и чтение
let { api, store } = makeCtx();
api.write({ startedAt: "2026-11-17T20:00", durationMin: 12, place: "TE-PLACE", partner: "TE-P" }, null);
const d1 = api.read();
check("черновик записан и читается", !!d1 && d1.rec && d1.rec.durationMin === 12, "rec.durationMin=" + (d1 && d1.rec && d1.rec.durationMin));
check("id пока null", d1 && d1.id === null, "id=" + (d1 && d1.id));

// 2) сервер выдал id -> фиксируем в черновике
api.patchId("srv-record-1");
const d2 = api.read();
check("id созданной записи зафиксирован", d2 && d2.id === "srv-record-1", "id=" + (d2 && d2.id));
check("id попал и в rec (повтор пойдёт по нему)", d2 && d2.rec.id === "srv-record-1", "rec.id=" + (d2 && d2.rec.id));
check("текст черновика не потерян", d2 && d2.rec.place === "TE-PLACE", "place=" + (d2 && d2.rec.place));

// 3) очистка после успеха
api.clear();
check("после успеха черновик очищен", api.read() === null, "read=" + JSON.stringify(api.read()));

// 4) негативный контроль: без patchId повтор не знал бы id -> создал бы вторую запись
({ api, store } = makeCtx());
api.write({ durationMin: 5 }, null);
const noId = api.read();
check("контроль: без patchId id остаётся null (повтор создал бы дубль)", noId && noId.id === null, "id=" + (noId && noId.id));

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
