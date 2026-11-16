/* FE-051 (runtime) — исполняет РЕАЛЬНЫЙ код showLoadError/clearLoadError из 8-front/js/app.js
 * на минимальной DOM-заглушке (без jsdom): проверяет поведение, а не текст файла.
 *
 * Проверки:
 *  1) блок вставляется ПЕРЕД списком внутри его родителя (а не над appbar);
 *  2) у блока role="alert" и класс load-error;
 *  3) текст сообщения тот, что передали;
 *  4) кнопка «Повторить» реально вызывает retry;
 *  5) clearLoadError убирает блок;
 *  6) негативный контроль: если вставить блок «как раньше» (в начало секции) — проверка 1 падает.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-051-runtime-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

function extract(fnName) {
  const start = js.indexOf("  function " + fnName + "(");
  if (start < 0) throw new Error("не найдена функция " + fnName);
  const next = js.indexOf("\n  function ", start + 10);
  return js.slice(start, next);
}

// --- минимальный DOM ---
class El {
  constructor(tag) {
    this.tagName = (tag || "div").toUpperCase();
    this.children = [];
    this.parentNode = null;
    this.className = "";
    this.attrs = {};
    this.textContent = "";
    this.listeners = {};
  }
  appendChild(c) { c.parentNode = this; this.children.push(c); return c; }
  insertBefore(c, ref) {
    const i = this.children.indexOf(ref);
    if (i < 0) return this.appendChild(c);
    c.parentNode = this;
    this.children.splice(i, 0, c);
    return c;
  }
  removeChild(c) {
    const i = this.children.indexOf(c);
    if (i >= 0) { this.children.splice(i, 1); c.parentNode = null; }
    return c;
  }
  setAttribute(k, v) { this.attrs[k] = v; }
  addEventListener(t, f) { (this.listeners[t] = this.listeners[t] || []).push(f); }
  click() { (this.listeners.click || []).forEach((f) => f()); }
  get firstChild() { return this.children[0] || null; }
  // поиск по классу в поддереве
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

function makeDom() {
  const section = new El("section"); // #view-journal
  const header = new El("header");
  section.appendChild(header);
  const content = new El("main");
  const list = new El("div"); // #journal-list
  const empty = new El("div");
  content.appendChild(list);
  content.appendChild(empty);
  section.appendChild(content);

  const byId = { "view-journal": section, "journal-list": list, "journal-empty": empty };
  const document = {
    getElementById: (id) => byId[id] || null,
    createElement: (t) => new El(t),
  };
  return { document, section, header, content, list, byId };
}

function load(dom, code) {
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    document: dom.document,
    window: {},
    setTimeout, clearTimeout, Promise, Date,
    $: (id) => dom.document.getElementById(id),
  };
  vm.createContext(ctx);
  const src = [
    "var loadErrorRetries = 0;",
    extract("clearLoadError"),
    extract("showLoadError"),
    "globalThis.__api = { showLoadError: showLoadError, clearLoadError: clearLoadError };",
  ].join("\n");
  vm.runInContext(src, ctx);
  return ctx.__api;
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

// ---- сценарий 1: реальная функция ----
let retries = 0;
let dom = makeDom();
let api = load(dom, js);
api.showLoadError("view-journal", "journal-list", "Не удалось загрузить журнал. Проверьте связь.", () => { retries++; });

const boxes = dom.section.children.filter((c) => (" " + c.className + " ").indexOf(" load-error ") >= 0)
  .concat(dom.content.children.filter((c) => (" " + c.className + " ").indexOf(" load-error ") >= 0));
check("блок ошибки создан", boxes.length === 1, "найдено: " + boxes.length);
const box = boxes[0];
check("блок внутри .content (рядом со списком), а не в начале секции",
  !!box && box.parentNode === dom.content, "parent=" + (box && box.parentNode && box.parentNode.tagName));
check("блок стоит ПЕРЕД списком", !!box && dom.content.children.indexOf(box) < dom.content.children.indexOf(dom.list),
  "индексы: box=" + (box ? dom.content.children.indexOf(box) : -1) + ", list=" + dom.content.children.indexOf(dom.list));
check("role=alert", !!box && box.attrs.role === "alert", "role=" + (box && box.attrs.role));
check("текст сообщения сохранён", !!box && box.children[0].textContent.indexOf("Не удалось загрузить журнал") === 0,
  "текст=" + (box && box.children[0].textContent.slice(0, 40)));
const btn = box.children.find((c) => c.tagName === "BUTTON");
check("есть кнопка «Повторить»", !!btn && btn.textContent === "Повторить", "текст кнопки=" + (btn && btn.textContent));
btn.click();
check("кнопка вызывает retry", retries === 1, "retry=" + retries);

api.clearLoadError("view-journal");
const left = dom.section.children.concat(dom.content.children)
  .filter((c) => (" " + c.className + " ").indexOf(" load-error ") >= 0).length;
check("clearLoadError убирает блок", left === 0, "осталось: " + left);

// ---- сценарий 2: негативный контроль (старая вставка в начало секции) ----
dom = makeDom();
retries = 0;
api = load(dom, js);
// имитируем старое поведение: вставляем блок в начало секции, минуя функцию
const oldBox = new dom.document.constructor === undefined ? null : null; // (не используется)
const fake = dom.document.createElement("div");
fake.className = "load-error";
dom.section.insertBefore(fake, dom.section.firstChild);
const badPlacement = fake.parentNode === dom.content && dom.content.children.indexOf(fake) < dom.content.children.indexOf(dom.list);
check("контроль: старая вставка не проходит проверку места", badPlacement === false, "старое место: " + (fake.parentNode && fake.parentNode.tagName));

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
