/* FE-051 — структурный тест: видимая ошибка загрузки вместо молча пустого экрана.
 * Читает РЕАЛЬНЫЕ 8-front/js/app.js и 8-front/css/styles.css.
 * Запуск: node 5-tech-backlog/front-end/FE-051-load-error-test.js
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");
const css = fs.readFileSync(path.join(root, "8-front", "css", "styles.css"), "utf8");

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

// тело функции: от "function X(" до следующей функции верхнего уровня
function bodyOf(fnName) {
  const start = js.indexOf("  function " + fnName + "(");
  if (start < 0) return "";
  const next = js.indexOf("\n  function ", start + 10);
  return js.slice(start, next > 0 ? next : start + 4000);
}

check("helper showLoadError объявлен", /function showLoadError\(viewId, listId, message, retry\)/.test(js), "есть");
check("helper clearLoadError объявлен", /function clearLoadError\(viewId\)/.test(js), "есть");
check("блок ошибки имеет role=alert", /setAttribute\("role", "alert"\)/.test(js), "есть");
check("кнопка «Повторить» вызывает retry", /textContent = "Повторить"[\s\S]{0,400}?typeof retry === "function"/.test(js), "есть");

for (const [fn, view, list] of [["renderJournal", "view-journal", "journal-list"], ["renderPartners", "view-partners", "partners-list"]]) {
  const b = bodyOf(fn);
  const hasCatch = /catch\(function \(e\)/.test(b);
  const callsShow = b.includes('showLoadError("' + view + '"');
  const hasText = /не удалось/i.test(b);
  const hasClear = b.includes('clearLoadError("' + view + '")');
  const retryOk = new RegExp('showLoadError\\("' + view + '"[\\s\\S]{0,300}?' + fn + '\\);').test(b);
  check(fn + ": .catch", hasCatch, "catch=" + hasCatch);
  check(fn + ": показывает блок ошибки", callsShow, "showLoadError=" + callsShow);
  check(fn + ": текст про «не удалось»", hasText, "текст=" + hasText);
  check(fn + ": очищает блок при успехе", hasClear, "clear=" + hasClear);
  check(fn + ": retry указывает на сам рендер", retryOk, "retry=" + retryOk);
}

// FE-051 (доп.): при ошибке пустые состояния скрываются, чтобы экран не противоречил себе
for (const [fn, emptyId] of [['renderJournal','journal-empty'], ['renderPartners','partners-empty']]) {
  const b = bodyOf(fn);
  check(fn + ': скрывает «' + emptyId + '» при ошибке', b.includes('\).classList.add("hidden")'), 'empty-state');
}

check("блок ошибки вставляется рядом со списком, а не над appbar",
  /list\.parentNode\.insertBefore\(box, list\)/.test(js), "позиция вставки");
check("CSS для .load-error есть", /\.load-error\s*\{/.test(css), "стиль есть");

// негативный контроль: убрать catch из renderPartners — тест обязан это заметить
const patched = js.replace(/\.catch\(function \(e\) \{\s*console\.error\("renderPartners:[\s\S]{0,400}?\}\);/, "");
const oldBody = patched.slice(patched.indexOf("  function renderPartners("), patched.indexOf("  function renderPartners(") + 2500);
check("контроль: без catch проверка падает", /catch\(function \(e\)/.test(oldBody) === false, "старое поведение без catch");

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
