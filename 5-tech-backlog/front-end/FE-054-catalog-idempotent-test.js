/* FE-054 (runtime) — идемпотентное создание каталога: клиентский id переиспользуется при retry.
 *
 * По сообщению @BE 2026-11-16 13:35: `POST /api/v1/catalog` принимает опциональный `id`.
 * Исполняет настоящий DB.putCatalogItem из repository.js в vm со стабами Api:
 *  1) `Api.catalog.add` получает сгенерированный клиентом id (не пустой, один и тот же при повторе);
 *  2) при сетевой ошибке — ровно один retry, и id во втором вызове ТОТ ЖЕ (идемпотентность);
 *  3) при 400 retry НЕ делается (не прячем настоящую валидацию);
 *  4) успешный ответ отдаёт элемент с id (серверный или клиентский).
 *
 * Запуск: node 5-tech-backlog/front-end/FE-054-catalog-idempotent-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "repository.js"), "utf8");

function extractPutCatalog() {
  const start = js.indexOf("  DB.putCatalogItem = function (it) {");
  if (start < 0) throw new Error("putCatalogItem не найден");
  const end = js.indexOf("\n  };", start);
  return js.slice(start, end + 4);
}

function makeCtx(addImpl) {
  const calls = [];
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    Promise, setTimeout, clearTimeout, Date,
    crypto: { randomUUID: () => "uuid-fixed-0001" },
    window: {},
    DB: {},
    Api: {
      catalog: {
        add: (kind, name, id) => { calls.push({ kind, name, id }); return addImpl(calls.length, { kind, name, id }); },
        details: () => Promise.resolve({}),
        archive: () => Promise.resolve(),
        restore: () => Promise.resolve(),
        rename: () => Promise.resolve(),
        remove: () => Promise.resolve(),
        get: () => Promise.resolve(null),
      },
    },
  };
  vm.createContext(ctx);
  // clientUuid живёт рядом с retryOnce — вырезаем её вместе с putCatalogItem
  const uuidFn = js.slice(js.indexOf("  function clientUuid()"), js.indexOf("  // FE-045: один retry на 400/404"));
  vm.runInContext(uuidFn + "\n" + extractPutCatalog() + "\nglobalThis.__put = DB.putCatalogItem;", ctx);
  return { ctx, calls };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

(async () => {
  // 1) успех с первой попытки: id непустой и равен клиентскому UUID
  let c = makeCtx(() => Promise.resolve({ id: "srv-1" }));
  let out = await c.ctx.__put({ kind: "toy", name: "TE-TOY-1" });
  check("успех: add вызван один раз", c.calls.length === 1, "вызовов=" + c.calls.length);
  check("успех: передан клиентский id", c.calls[0] && c.calls[0].id === "uuid-fixed-0001", "id=" + (c.calls[0] && c.calls[0].id));
  check("успех: элемент получил серверный id", out && out.id === "srv-1", "id=" + (out && out.id));

  // 2) сетевой сбой -> один retry с ТЕМ ЖЕ id
  c = makeCtx((i) => (i === 1 ? Promise.reject(Object.assign(new Error("network"), { status: 0 })) : Promise.resolve({ id: "srv-2" })));
  out = await c.ctx.__put({ kind: "toy", name: "TE-TOY-2" });
  check("сеть: ровно 2 вызова add", c.calls.length === 2, "вызовов=" + c.calls.length);
  check("сеть: id одинаковый в обоих вызовах", c.calls[0].id === c.calls[1].id && !!c.calls[0].id, "id1=" + c.calls[0].id + " id2=" + c.calls[1].id);
  check("сеть: результат получен", out && out.id === "srv-2", "id=" + (out && out.id));

  // 3) 400 -> retry НЕ делается
  c = makeCtx(() => Promise.reject(Object.assign(new Error("HTTP 400"), { status: 400 })));
  let err = null;
  try { await c.ctx.__put({ kind: "toy", name: "" }); } catch (e) { err = e; }
  check("400: ошибка проброшена", !!err && err.status === 400, "err=" + (err && err.status));
  check("400: retry не делался", c.calls.length === 1, "вызовов=" + c.calls.length);

  // 4) негативный контроль: без id в теле повтор создал бы дубль — проверяем, что id действительно уходит в add
  check("контроль: id уходит в add (иначе дубль при retry)", c.calls[0] && typeof c.calls[0].id === "string" && c.calls[0].id.length > 0, "id=" + (c.calls[0] && c.calls[0].id));

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
