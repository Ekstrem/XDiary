/* FE-051 (runtime, сильнее структурного) — реальный renderPartners из app.js при отказе загрузки.
 *
 * Исполняет НАСТОЯЩУЮ renderPartners (вместе с showLoadError/clearLoadError) на DOM-заглушке:
 *  1) при отказе DB.getAllPartners() появляется .load-error[role=alert] с текстом «Не удалось загрузить…»;
 *  2) блок вставлен ПЕРЕД списком внутри .content (а не над appbar);
 *  3) «Партнёров пока нет» (partners-empty) скрыт — экран не противоречит себе;
 *  4) кнопка «Повторить» реально перезапускает загрузку (второй вызов DB);
 *  5) при успешной повторной загрузке блок ошибки исчезает и список заполняется.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-051-partners-error-runtime-test.js
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

class El {
  constructor(tag) {
    this.tagName = (tag || "div").toUpperCase();
    this.children = [];
    this.parentNode = null;
    this.className = "";
    this.textContent = "";
    this.attrs = {};
    this.style = {};
    this.listeners = {};
    this.innerHTML = "";
    const classes = new Set();
    this.classList = {
      add: (k) => classes.add(k),
      remove: (k) => classes.delete(k),
      toggle: (k, on) => (on ? classes.add(k) : classes.delete(k)),
      contains: (k) => classes.has(k),
    };
  }
  appendChild(c) { c.parentNode = this; this.children.push(c); return c; }
  insertBefore(c, ref) {
    const i = this.children.indexOf(ref);
    if (i < 0) return this.appendChild(c);
    c.parentNode = this;
    this.children.splice(i, 0, c);
    return c;
  }
  removeChild(c) { const i = this.children.indexOf(c); if (i >= 0) { this.children.splice(i, 1); c.parentNode = null; } return c; }
  setAttribute(k, v) { this.attrs[k] = v; }
  addEventListener(t, f) { (this.listeners[t] = this.listeners[t] || []).push(f); }
  click() { (this.listeners.click || []).forEach((f) => f()); }
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
  get firstChild() { return this.children[0] || null; }
}

function makeCtx(behavior) {
  // behavior(i) -> Promise: i-я попытка загрузки
  const view = new El("section");
  const header = new El("header");
  view.appendChild(header);
  const content = new El("main");
  const list = new El("div");
  const empty = new El("div");
  empty.className = "empty-state hidden";
  content.appendChild(list);
  content.appendChild(empty);
  view.appendChild(content);

  const byId = { "view-partners": view, "partners-list": list, "partners-empty": empty };
  let attempt = 0;
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    document: { getElementById: (id) => byId[id] || null, createElement: (t) => new El(t) },
    window: {}, Promise, setTimeout, clearTimeout, Date,
    $: (id) => byId[id] || null,
    DB: { getAllPartners: () => { attempt++; return behavior(attempt); } },
    showStaleNote: () => {},
    avatarHtml: () => "<span>T</span>",
    esc: (s) => String(s),
    openPartnerEdit: () => {},
    partnerEditReturn: "partners",
  };
  vm.createContext(ctx);
  vm.runInContext(
    extract("clearLoadError") + "\n" + extract("showLoadError") + "\n" + extract("renderPartners") +
      "\nglobalThis.__render = renderPartners;",
    ctx
  );
  return { ctx, view, list, empty, attempts: () => attempt };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };
const flush = () => new Promise((r) => setTimeout(r, 60));

(async () => {
  // сценарий: первая загрузка падает (500, кэш пуст), вторая (после «Повторить») успешна
  const c = makeCtx((i) => (i === 1 ? Promise.reject(Object.assign(new Error("HTTP 500"), { status: 500 })) : Promise.resolve([{ id: "p1", name: "TE-P-1" }])));
  c.ctx.__render();
  await flush();

  const box = c.view.querySelector(".load-error");
  check("при отказе появился блок ошибки", !!box, box ? "есть" : "нет");
  check("role=alert", !!box && box.attrs.role === "alert", "role=" + (box && box.attrs.role));
  check("текст «Не удалось загрузить…»", !!box && /Не удалось загрузить/.test(box.children[0].textContent), "text=" + (box && box.children[0].textContent.slice(0, 50)));
  check("блок внутри .content перед списком", !!box && box.parentNode === c.list.parentNode && box.parentNode.children.indexOf(box) < box.parentNode.children.indexOf(c.list), "позиция ок");
  check("«Партнёров пока нет» скрыт", c.empty.classList.contains("hidden"), "hidden=" + c.empty.classList.contains("hidden"));

  // 4) клик «Повторить» -> вторая попытка загрузки
  const btn = box.children.find((x) => x.tagName === "BUTTON");
  check("есть кнопка «Повторить»", !!btn && btn.textContent === "Повторить", "текст=" + (btn && btn.textContent));
  btn.click();
  await flush();
  check("«Повторить» перезапустил загрузку", c.attempts() === 2, "попыток=" + c.attempts());

  // 5) при успехе блок исчез, список заполнен
  const boxAfter = c.view.querySelector(".load-error");
  check("после успеха блок ошибки исчез", boxAfter === null, boxAfter ? "остался" : "исчез");
  check("список заполнен карточками", c.list.children.length === 1, "карточек=" + c.list.children.length);

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
