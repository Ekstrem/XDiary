/* FE-037 (вариант B) — структурный тест: кнопка «Из Garmin» на «Журнале» и её обработчик.
 *
 * Читает РЕАЛЬНЫЕ 8-front/index.html и 8-front/js/app.js (не копию), проверяет:
 *  1) в секции #view-journal есть <button id="btn-journal-garmin" class="fab fab-garmin">;
 *  2) существующая кнопка «+» (#btn-new) осталась и не сломана;
 *  3) в app.js есть обработчик, вызывающий openImport() именно для этого id;
 *  4) негативный контроль: без строки-обработчика тест обязан падать.
 *
 * Запуск: node 5-tech-backlog/front-end/FE-037-journal-garmin-test.js
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..", "..");
const html = fs.readFileSync(path.join(root, "8-front", "index.html"), "utf8");
const js = fs.readFileSync(path.join(root, "8-front", "js", "app.js"), "utf8");

const results = [];
const check = (name, ok, detail) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + name + " — " + detail); };

// секция журнала: от <section id="view-journal" до следующего <section
const start = html.indexOf('<section id="view-journal"');
const nextSection = html.indexOf("<section", start + 10);
const journal = start >= 0 ? html.slice(start, nextSection > 0 ? nextSection : undefined) : "";

check("секция #view-journal найдена", journal.length > 0, "длина=" + journal.length);
check("кнопка «Из Garmin» внутри журнала", journal.includes('id="btn-journal-garmin"'), "id есть в секции");
check("кнопка использует класс fab fab-garmin", /id="btn-journal-garmin"[^>]*class="fab fab-garmin"/.test(journal), "class ok");
check("кнопка «+» не потеряна", journal.includes('id="btn-new"'), "#btn-new на месте");
check("кнопка доступна для скринридера", /id="btn-journal-garmin"[^>]*aria-label="[^"]+"/.test(journal), "aria-label ok");

const handler = /\((["'])btn-journal-garmin\1\)\.addEventListener\((["'])click\2,\s*function\s*\(\)\s*\{\s*openImport\(\);\s*\}\)/.test(js);
check("обработчик вызывает openImport()", handler, handler ? "строка есть" : "строка не найдена");

// негативный контроль: тот же поиск без строки-обработчика должен не находить
const jsWithout = js.replace(/\$\("btn-journal-garmin"\)\.addEventListener\([^\n]*\n/, "");
const handlerOld = /\((["'])btn-journal-garmin\1\)\.addEventListener\((["'])click\2,\s*function\s*\(\)\s*\{\s*openImport\(\);\s*\}\)/.test(jsWithout);
check("контроль: без обработчика тест падает", handlerOld === false, "старое поведение: " + (handlerOld ? "прошло (плохо)" : "упало (ожидаемо)"));

const failed = results.filter((x) => !x).length;
console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
process.exit(failed === 0 ? 0 : 1);
