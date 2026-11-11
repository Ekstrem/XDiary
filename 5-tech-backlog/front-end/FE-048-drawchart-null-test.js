/* FE-048 — проверка, что drawChart не бросает на null-canvas.
 * Читает РЕАЛЬНЫЙ drawChart из 8-front/js/app.js (без копии логики) и гоняет его
 * с null / без parentElement / с canvas без getContext.
 * Запуск: node 5-tech-backlog/front-end/FE-048-drawchart-null-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const src = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

const start = src.indexOf("function drawChart(canvas");
if (start < 0) { console.error("FAIL: drawChart не найден"); process.exit(1); }
// берём тело функции до следующей функции верхнего уровня внутри IIFE
const rest = src.slice(start);
const endMarker = rest.indexOf("\n  function ", 10);
const fnSrc = endMarker > 0 ? rest.slice(0, endMarker) : rest;

const ctx = {
  console,
  window: { devicePixelRatio: 2 },
  document: { getElementById: () => null },
};
vm.createContext(ctx);
// METRIC_META живёт в модуле рядом с drawChart — даём заглушку, чтобы проверять только guard.
vm.runInContext("var METRIC_META = new Proxy({}, { get: function(){ return { label: '', color: '#000' }; } });", ctx);
vm.runInContext("var drawChart = " + fnSrc.replace(/^function drawChart/, "function"), ctx);

const results = [];
const check = (name, ok, detail) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + name + " — " + detail); };

// 1) canvas === null
let r = null, threw = null;
try { r = ctx.drawChart(null, [{ t: 1, v: 1 }], "v", 0, 10, 0); } catch (e) { threw = e; }
check("canvas=null → не бросает", threw === null, threw ? threw.message : "ok");
check("canvas=null → count=0", r && r.count === 0, JSON.stringify(r));

// 2) объект без parentElement
threw = null;
try { r = ctx.drawChart({ getContext: () => ({}) }, [], "v", 0, 10, 0); } catch (e) { threw = e; }
check("нет parentElement → не бросает", threw === null, threw ? threw.message : "ok");
check("нет parentElement → count=0", r && r.count === 0, JSON.stringify(r));

// 3) parentElement есть, но getContext вернул null
threw = null;
try {
  r = ctx.drawChart({ parentElement: { clientWidth: 300 }, getContext: () => null, style: {} }, [], "v", 0, 10, 0);
} catch (e) { threw = e; }
check("getContext()=null → не бросает", threw === null, threw ? threw.message : "ok");

// 4) пустые точки при валидном canvas — прежнее поведение (count=0), не падение
const fakeCtx2d = new Proxy({}, { get: () => () => {} });
threw = null;
try {
  r = ctx.drawChart({ parentElement: { clientWidth: 300 }, getContext: () => fakeCtx2d, style: {} }, [], "v", 0, 10, 0);
} catch (e) { threw = e; }
check("валидный canvas, 0 точек → не бросает и count=0", threw === null && r && r.count === 0,
  threw ? threw.message : JSON.stringify({ count: r && r.count }));

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
