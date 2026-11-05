/* FE-045 — проверка логики, вынесенной из реального repository.js:
 * needProfile() (когда profile нужен) и retryOnce() (один retry на 400/404).
 * Запуск: node 5-tech-backlog/front-end/FE-045-partner-save-test.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..", "..");
const src = fs.readFileSync(path.join(root, "8-front", "js", "repository.js"), "utf8");
const start = src.indexOf("function needProfile(p) {");
const end = src.indexOf("DB.putPartner = function (p) {");
if (start < 0 || end < 0) { console.error("FAIL: блок needProfile/retryOnce не найден"); process.exit(1); }

const ctx = { console, Promise, setTimeout };
vm.createContext(ctx);
vm.runInContext(src.slice(start, end), ctx);

const results = [];
const check = (name, ok, detail) => {
  results.push({ name, ok });
  console.log((ok ? "PASS " : "FAIL ") + name + " — " + detail);
};

// needProfile: дефолты формы не требуют profile
check("photo='', gender=female, notes='' → profile НЕ нужен",
  ctx.needProfile({ photo: "", gender: "female", notes: "" }) === false, "false");
check("gender=male → profile нужен",
  ctx.needProfile({ photo: "", gender: "male", notes: "" }) === true, "true");
check("photo непустой → profile нужен",
  ctx.needProfile({ photo: "data:image/png;base64,x", gender: "female", notes: "" }) === true, "true");
check("notes непустые → profile нужен",
  ctx.needProfile({ photo: "", gender: "female", notes: "заметка" }) === true, "true");
check("gender не задан вовсе → profile НЕ нужен (дефолт female)",
  ctx.needProfile({ photo: "", notes: "" }) === false, "false");

(async () => {
  // retryOnce: 404 → один повтор и успех
  let calls = 0;
  const flaky = () => { calls++; return calls === 1 ? Promise.reject({ status: 404 }) : Promise.resolve("ok"); };
  const r1 = await ctx.retryOnce(flaky);
  check("404 → retryOnce повторяет и возвращает результат", r1 === "ok" && calls === 2, "calls=" + calls);

  // 500 — не ретраим
  let calls2 = 0;
  let threw = false;
  try { await ctx.retryOnce(() => { calls2++; return Promise.reject({ status: 500 }); }); }
  catch (e) { threw = true; }
  check("500 → НЕ ретраится, ошибка пробрасывается", threw && calls2 === 1, "calls=" + calls2);

  const failed = results.filter((r) => !r.ok).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " проверок, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
