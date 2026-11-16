/* FE-052 (runtime) — исполняет РЕАЛЬНЫЙ код afterAuth() из 8-front/js/app.js
 * со стабами окружения: проверяет РЕШЕНИЯ на классах ошибок, а не текст файла.
 *
 * Ожидаемое поведение после фикса:
 *   success(verified+displayName)   -> openJournal
 *   success(verified, без имени)    -> showProfileStep
 *   таймаут (fallback null)         -> forceVisibleScreen
 *   reject status 500 / 502         -> forceVisibleScreen
 *   reject status 0 (нет связи)     -> forceVisibleScreen
 *   reject status 404               -> toast + openOnboarding
 *   reject status 401               -> openOnboarding (без forceVisibleScreen)
 *
 * Запуск: node 5-tech-backlog/front-end/FE-052-runtime-test.js
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

function makeCtx(meImpl, opts) {
  const calls = { journal: 0, profileStep: 0, onboarding: 0, force: [], toast: [] };
  const ctx = {
    console: { log() {}, error() {}, warn() {} },
    Promise, setTimeout, clearTimeout, Date,
    localStorage: { getItem: (k) => (k === "xdiary.clientId" ? "client-1" : null) },
    onbClientId: "",
    storedClientId: () => "client-1",
    showProfileStep: () => { calls.profileStep++; },
    openJournal: () => { calls.journal++; },
    openOnboarding: () => { calls.onboarding++; },
    forceVisibleScreen: (w) => { calls.force.push(w); },
    toast: (t) => { calls.toast.push(t); },
    Api: { config: { token: () => (opts && opts.noToken ? "" : "tok") }, clients: { me: meImpl } },
    withTimeout: (p, ms, fallback) => p.catch((e) => { throw e; }).then((v) => v, (e) => { throw e; }).catch ? p : p,
  };
  // настоящий withTimeout-семантикс: гонка промиса и таймера
  ctx.withTimeout = (p, ms, fallback) => Promise.race([p, new Promise((res) => setTimeout(() => res(fallback), ms))]);
  vm.createContext(ctx);
  vm.runInContext(extract("afterAuth") + "\nglobalThis.__afterAuth = afterAuth;", ctx);
  return { ctx, calls };
}

const results = [];
const check = (n, ok, d) => { results.push(ok); console.log((ok ? "PASS " : "FAIL ") + n + " — " + d); };

async function scenario(name, meImpl, opts, expect, waitMs) {
  const { ctx, calls } = makeCtx(meImpl, opts);
  ctx.__afterAuth();
  await new Promise((r) => setTimeout(r, waitMs || 60)); // дать таймеру/промису сработать
  const actual = {
    journal: calls.journal, profileStep: calls.profileStep,
    onboarding: calls.onboarding, force: calls.force.length, toast: calls.toast.length,
  };
  const ok = Object.keys(expect).every((k) => actual[k] === expect[k]);
  check(name, ok, JSON.stringify(actual) + " ожидали " + JSON.stringify(expect));
}

(async () => {
  await scenario("успех: подтверждён + имя -> журнал",
    () => Promise.resolve({ verified: true, displayName: "A" }), {},
    { journal: 1, onboarding: 0, force: 0 });

  await scenario("успех: подтверждён без имени -> шаг профиля",
    () => Promise.resolve({ verified: true }), {},
    { profileStep: 1, onboarding: 0, force: 0 });

  await scenario("таймаут me() -> видимая ошибка, НЕ онбординг",
    () => new Promise(() => {}), {},
    { onboarding: 0, force: 1 }, 2700);

  await scenario("5xx -> видимая ошибка",
    () => Promise.reject(Object.assign(new Error("HTTP 500"), { status: 500 })), {},
    { onboarding: 0, force: 1 });

  await scenario("нет связи (status 0) -> видимая ошибка",
    () => Promise.reject(Object.assign(new Error("network"), { status: 0 })), {},
    { onboarding: 0, force: 1 });

  await scenario("404 -> онбординг + сообщение",
    () => Promise.reject(Object.assign(new Error("HTTP 404"), { status: 404 })), {},
    { onboarding: 1, force: 0, toast: 1 });

  await scenario("401 -> штатный онбординг без force",
    () => Promise.reject(Object.assign(new Error("HTTP 401"), { status: 401 })), {},
    { onboarding: 1, force: 0 });

  await scenario("нет токена -> онбординг без вызова me()",
    () => { throw new Error("me() не должен вызываться"); }, { noToken: true },
    { onboarding: 1, force: 0 });

  const failed = results.filter((x) => !x).length;
  console.log("\nИТОГ: " + (results.length - failed) + "/" + results.length + " сценариев, падений " + failed);
  process.exit(failed === 0 ? 0 : 1);
})();
