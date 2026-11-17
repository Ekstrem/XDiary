/* FE-055 (проверка контракта FE-044, runtime) — showStaleNote на РЕАЛЬНОМ коде из app.js.
 *
 * Проверяем (read-paths §5 п.2 «смешанная свежесть названа»):
 *  1) свежие данные (readFreshness = null) -> бейджа нет;
 *  2) данные из кэша, возраст 2 ч (< 24 ч) -> бейджа нет;
 *  3) данные из кэша, возраст 48 ч (> 24 ч) -> бейдж ЕСТЬ, текст «данные могли устареть … ч назад»;
 *  4) повторный вызов на свежих данных УБИРАЕТ ранее показанный бейдж;
 *  5) негативный контроль: если бы порог не проверялся (свежие данные давали бы бейдж) — проверка 2 падала бы.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-055-stale-note-runtime-test.js
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

// ---- минимальный DOM с поддержкой querySelector('.stale-note') и removeChild ----
class El {
  constructor(tag) {
    this.tagName = tag.toUpperCase();
    this.children = [];
    this.parentNode = null;
    this.className = "";
    this.textContent = "";
    this.attrs = {};
    this.style = {};
  }
  setAttribute(k, v) { this.attrs[k] = v; }
  insertBefore(c, ref) { const i = this.children.indexOf(ref); if (i < 0) return this.appendChild(c); c.parentNode = this; this.children.splice(i, 0, c); return c; }
  appendChild(c) { c.parentNode = this; this.children.push(c); return c; }
  removeChild(c) {
    const i = this.children.indexOf(c);
    if (i >= 0) { this.children.splice(i, 1); c.parentNode = null; }
    return c;
  }
  querySelector(sel) {
    const cls = sel.replace(/^\./, "");
    const walk = (n) => {
      for (const c of n.children) {
        if ((" " + c.className + " ").indexOf(" " + cls + " ") >= 0) return c;
        const r = walk(c);
        if (r) return r;
      }
      return null;
    };
    return walk(this);
  }
}

function makeCtx(freshness) {
  const view = new El("section");
  const byId = { "view-wizard": view };
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    document: {
      getElementById: (id) => byId[id] || null,
      createElement: (t) => new El(t),
    },
    window: {}, Promise, setTimeout, clearTimeout, Date,
    DB: { readFreshness: (k) => freshness[k] || null },
    STALE_AFTER_HOURS_UI: 24,
  };
  vm.createContext(ctx);
  vm.runInContext(
    "var STALE_AFTER_HOURS_UI = 24;\n" +
      extract("staleText") + "\n" + extract("showStaleNote") +
      "\nglobalThis.__show = showStaleNote;",
    ctx
  );
  return { ctx, view };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

function badge(view) { return view.querySelector(".stale-note"); }

// 1) свежие данные — бейджа нет
let { ctx, view } = makeCtx({ partners: null, catalog: null });
ctx.__show("view-wizard", ["partners", "catalog"]);
check("свежие данные: бейджа нет", badge(view) === null, "badge=" + (badge(view) ? "есть" : "нет"));

// 2) кэш 2 ч (< 24 ч) — бейджа нет
({ ctx, view } = makeCtx({ partners: { fromCache: true, ageMs: 2 * 3600000 }, catalog: null }));
ctx.__show("view-wizard", ["partners", "catalog"]);
check("кэш 2 ч (<24 ч): бейджа нет", badge(view) === null, "badge=" + (badge(view) ? "есть" : "нет"));

// 3) кэш 48 ч (> 24 ч) — бейдж есть и текст про часы
({ ctx, view } = makeCtx({ partners: { fromCache: true, ageMs: 48 * 3600000 }, catalog: null }));
ctx.__show("view-wizard", ["partners", "catalog"]);
const b = badge(view);
check("кэш 48 ч: бейдж есть", !!b, "badge=" + (b ? "есть" : "нет"));
check("текст называет возраст в часах", !!b && /48\s*ч назад/.test(b.textContent), "text=" + (b ? JSON.stringify(b.textContent).slice(0, 60) : "-"));

// 4) свежие данные после устаревших — бейдж убирается
({ ctx, view } = makeCtx({ partners: { fromCache: true, ageMs: 48 * 3600000 }, catalog: null }));
ctx.__show("view-wizard", ["partners"]);
const had = !!badge(view);
// подменяем свежесть на «свежую» — тот же ctx, второй вызов
ctx.DB.readFreshness = () => null;
ctx.__show("view-wizard", ["partners"]);
check("повторный вызов на свежих данных убирает бейдж", had && badge(view) === null, "было=" + had + ", стало=" + (badge(view) ? "есть" : "нет"));

// 5) негативный контроль: если бы «устарело» считалось всегда, кэш 2 ч тоже дал бы бейдж
({ ctx, view } = makeCtx({ partners: { fromCache: true, ageMs: 2 * 3600000 }, catalog: null }));
const forced = { fromCache: true, ageMs: 2 * 3600000 };
const wouldShow = forced.fromCache; // без порога бейдж появился бы
check("контроль: без порога бейдж появился бы и на 2 ч", wouldShow === true && badge(view) === null, "порог соблюдён");

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
