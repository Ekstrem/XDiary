/* FE-040 — воспроизводимый тест таймеров индикатора загрузки.
 *
 * Зачем: в карточке `FE-040` я ссылался на «прогон на заглушке DOM», но самого артефакта в
 * репозитории не было — то есть приёмка опиралась на моё слово. @TL: «нужен артефакт теста
 * таймеров, воспроизводимый, а не „проверено вручную“». Этот файл и есть артефакт.
 *
 * Что делает: читает РЕАЛЬНЫЙ код `busyStart`/`busyStop`/`busyRun` из `8-front/js/app.js`,
 * подставляет минимальную заглушку DOM и проверяет три поведения карточки:
 *   1) быстрая операция (150 мс) — баннер НЕ появляется вовсе (порог 300 мс, «не мигает»);
 *   2) средняя (1000 мс) — появился на ~400 мс и снят к ~1300 мс;
 *   3) долгая (3200 мс) — текст «дольше обычного» + кнопка «Повторить», клик вызывает повтор
 *      и снимает баннер.
 *
 * Запуск (из корня репозитория): node 5-tech-backlog/front-end/FE-040-timers-test.js
 * Код возврата: 0 — все проверки прошли, 1 — есть падение.
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const appPath = path.join(root, "8-front", "js", "app.js");
const src = fs.readFileSync(appPath, "utf8");

const start = src.indexOf("var busyState = null;");
const end = src.indexOf("function renderTrends()");
if (start < 0 || end < 0 || end <= start) {
  console.error("FAIL: не найден блок FE-040 (busyStart..renderTrends) в " + appPath);
  process.exit(1);
}
const block = src.slice(start, end);

function mkEl() {
  const el = {
    style: { cssText: "" },
    className: "",
    parentNode: null,
    children: [],
    _handlers: {},
    setAttribute() {},
    addEventListener(t, f) { this._handlers[t] = f; },
    appendChild(c) { c.parentNode = el; el.children.push(c); return c; },
    removeChild(c) { el.children = el.children.filter((x) => x !== c); c.parentNode = null; return c; },
  };
  return el;
}

const doc = { body: mkEl(), createElement: mkEl };
const ctx = { document: doc, setTimeout, clearTimeout, Promise, console };
vm.createContext(ctx);
vm.runInContext(block, ctx);

const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const last = () => doc.body.children[doc.body.children.length - 1];

const results = [];
function check(name, ok, detail) {
  results.push({ name, ok, detail });
  console.log((ok ? "PASS " : "FAIL ") + name + " — " + detail);
}

(async () => {
  // 1) быстрая операция: баннера не должно быть вообще
  ctx.busyRun("Быстро", () => wait(150));
  await wait(500);
  check("150 мс → баннера нет", doc.body.children.length === 0,
    "children=" + doc.body.children.length + " (ожидание 0)");

  // 2) средняя: появился после 300 мс, снят к ответу
  ctx.busyRun("Средний", () => wait(1000));
  await wait(400);
  const shownAt400 = last() && last().style.display === "block";
  check("1000 мс @400 мс → баннер виден", !!shownAt400,
    "display=" + (last() ? last().style.display : "нет элемента"));
  await wait(900);
  check("1000 мс @1300 мс → баннер снят", doc.body.children.length === 0,
    "children=" + doc.body.children.length + " (ожидание 0)");

  // 3) долгая: «дольше обычного» + кнопка, клик → повтор и снятие
  let retried = false;
  ctx.busyRun("Долгий", () => wait(3500), () => { retried = true; });
  await wait(3200);
  const el = last();
  const slowText = el && el.children[0] && el.children[0].textContent;
  const btnVisible = el && el.children[1] && el.children[1].style.display === "inline-block";
  check("3200 мс → «дольше обычного»", /дольше обычного/.test(String(slowText)), "text=\"" + slowText + "\"");
  check("3200 мс → кнопка «Повторить» видна", !!btnVisible,
    "btn=" + (el && el.children[1] ? el.children[1].style.display : "—"));
  el.children[1]._handlers.click();
  check("клик «Повторить» → повтор вызван", retried === true, "retried=" + retried);
  check("клик «Повторить» → баннер снят", doc.body.children.length === 0,
    "children=" + doc.body.children.length + " (ожидание 0)");

  await wait(500);
  const failed = results.filter((r) => !r.ok).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
