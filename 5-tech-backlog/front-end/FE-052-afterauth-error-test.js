/* FE-052 — структурный тест: при ошибке me() на старте фронт не уходит молча в онбординг.
 * Читает РЕАЛЬНЫЙ 8-front/js/app.js.
 * Запуск: node 5-tech-backlog/front-end/FE-052-afterauth-error-test.js
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

function bodyOf(src, fnName) {
  const start = src.indexOf("  function " + fnName + "(");
  if (start < 0) return "";
  const next = src.indexOf("\n  function ", start + 10);
  return src.slice(start, next > 0 ? next : start + 4000);
}

const after = bodyOf(js, "afterAuth");
const fvs = bodyOf(js, "forceVisibleScreen");

const timeoutBranch = /if \(c === null\)[\s\S]{0,220}forceVisibleScreen\(/;
const serverErrBranch = /st >= 500[\s\S]{0,220}forceVisibleScreen\(/;
const notFoundBranch = /st === 404[\s\S]{0,220}toast\(/;

check("afterAuth найден", after.length > 0, "длина=" + after.length);
check("таймаут (null) даёт видимую ошибку", timeoutBranch.test(after), "forceVisibleScreen при таймауте");
check("5xx/сеть дают видимую ошибку", serverErrBranch.test(after), "forceVisibleScreen при 5xx");
check("404 не молчит (сообщение пользователю)", notFoundBranch.test(after), "toast при 404");
check("онбординг остаётся штатным путём", /openOnboarding\(\);/.test(after), "openOnboarding есть");
check("forceVisibleScreen не обещает несуществующую кнопку", !/нажмите «Повторить»/.test(fvs), "текст правдивый");
check("forceVisibleScreen объясняет, что делать", /обновите страницу|Проверьте сеть/.test(fvs), "инструкция есть");

// Негативный контроль: те же проверки на «старом» тексте, где ветки таймаута нет.
// Доказывает, что проверка зависит от кода, а не проходит всегда.
const oldAfter = after.replace("if (c === null)", "if (c === undefined)");
const oldTimeout = timeoutBranch.test(oldAfter);
const oldServer = serverErrBranch.test(oldAfter.replace("st >= 500", "st >= 599"));
check("контроль: без ветки таймаута проверка падает", oldTimeout === false, "старое: " + oldTimeout);
check("контроль: без ветки 5xx проверка падает", oldServer === false, "старое: " + oldServer);

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
