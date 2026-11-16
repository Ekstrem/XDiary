/* FE-053 — runtime: renderSomatic не должен ломаться, когда #somatic-stats нет в DOM.
 * Ошибка возникает АСИНХРОННО (внутри .then), поэтому ловим unhandledRejection, а не try/catch.
 * Запуск: node 5-tech-backlog/front-end/FE-053-somatic-null-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

function extractFn(src, fnName) {
  const start = src.indexOf("  function " + fnName + "(");
  if (start < 0) return null;
  const next = src.indexOf("\n  function ", start + 10);
  return src.slice(start, next);
}

const rejections = [];
process.on("unhandledRejection", (e) => rejections.push(e));

function makeCtx(code, withStats) {
  const stats = { textContent: "" };
  const el = {
    innerHTML: "", children: [],
    appendChild(c) { this.children.push(c); },
    querySelector() { return { innerHTML: "", children: [], appendChild(c) { this.children.push(c); }, addEventListener() {}, classList: { add() {}, remove() {}, toggle() {} } }; },
    classList: { add() {}, remove() {}, toggle() {} },
  };
  // все прочие элементы «есть»: тест проверяет только отсутствие #somatic-stats
  const stubEl = () => ({
    innerHTML: "", textContent: "", classList: { add() {}, remove() {}, toggle() {} },
    appendChild() {}, insertBefore() {}, querySelector() { return null; },
    addEventListener() {}, setAttribute() {}, style: {},
  });
  const cache = {};
  const $ = (id) => {
    if (id === "somatic-stats") return withStats ? stats : null;
    return cache[id] || (cache[id] = stubEl());
  };
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    document: { createElement: () => ({ className: "", textContent: "", addEventListener() {}, appendChild() {} }), getElementById: $ },
    window: {}, Promise, setTimeout, clearTimeout, Date,
    $: $,
    METRIC_META: { freq: { label: "Частота", unit: "/нед" } },
    somaticFailed: false, detailMetric: "freq", detailWindow: 0,
    rangeStartMs: 0, rangeEndMs: 60000, startMs: 0,
    drawChart: () => ({ min: 1, avg: 2, max: 3, count: 0 }),
    fillDatalist: () => {},
    minuteKeyFromDate: () => "0",
    toDateKey: () => "2026-11-16",
    showReadError: () => {},
    renderDetail: () => {},
    renderSomatic: null,
    Api: {
      somatic: { slice: () => Promise.resolve([]) },
      daily: { series: () => Promise.resolve([]) },
    },
  };
  vm.createContext(ctx);
  vm.runInContext("var METRIC_ORDER = ['freq'];\nvar somaticFailed = false;\n" + code + "\nglobalThis.__render = renderSomatic;", ctx);
  return { ctx, el, stats };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

(async () => {
  // 1) текущий код, элемента нет — не должно быть ни исключения, ни unhandledRejection
  rejections.length = 0;
  let syncErr = null;
  try {
    const { ctx, el } = makeCtx(extractFn(js, "renderSomatic"), false);
    ctx.__render(el, { points: [] });
  } catch (e) { syncErr = e; }
  await new Promise((r) => setTimeout(r, 120));
  check("без #somatic-stats: нет синхронного исключения", syncErr === null, syncErr ? String(syncErr).slice(0, 80) : "ok");
  check("без #somatic-stats: нет unhandledRejection", rejections.length === 0,
    rejections.length ? String(rejections[0]).slice(0, 90) : "0 отказов");

  // 2) элемент есть — нормальный путь пишет текст
  rejections.length = 0;
  const okCase = makeCtx(extractFn(js, "renderSomatic"), true);
  okCase.ctx.__render(okCase.el, { points: [] });
  await new Promise((r) => setTimeout(r, 120));
  check("с #somatic-stats: нет отказов", rejections.length === 0, rejections.length ? String(rejections[0]).slice(0, 80) : "0 отказов");
  check("с #somatic-stats: поле обновлено", typeof okCase.stats.textContent === "string", "textContent=" + JSON.stringify(okCase.stats.textContent));

  // 3) негативный контроль: тот же код без guard обязан дать отказ
  rejections.length = 0;
  const noGuard = extractFn(js, "renderSomatic").replace(/\n\s*if \(!s\) return;/, "");
  let oldErr = null;
  try {
    const { ctx, el } = makeCtx(noGuard, false);
    ctx.__render(el, { points: [] });
  } catch (e) { oldErr = e; }
  await new Promise((r) => setTimeout(r, 120));
  check("контроль: без guard — ошибка (sync или отказ)", oldErr !== null || rejections.length > 0,
    "sync=" + (oldErr ? "да" : "нет") + ", отказов=" + rejections.length);

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
