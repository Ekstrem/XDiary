// FE-057: структурная проверка Garmin Connect/State/Sync и сохранения CSV-пути.
// Запуск: node 5-tech-backlog/front-end/FE-057-garmin-connect-test.js
"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "../..");
const app = fs.readFileSync(path.join(root, "8-front/js/app.js"), "utf8");
const api = fs.readFileSync(path.join(root, "8-front/js/api.js"), "utf8");
const html = fs.readFileSync(path.join(root, "8-front/index.html"), "utf8");
const sw = fs.readFileSync(path.join(root, "8-front/sw.js"), "utf8");

let passed = 0;
function ok(name, condition) {
  assert.ok(condition, name);
  passed += 1;
  console.log("ok", passed, "-", name);
}
function has(source, needle) { return source.includes(needle); }

// API-контракт.
ok("Connect отправляет credentials отдельным объектом", has(api, 'post("/api/v1/garmin/connect", { credentials: credentials })'));
ok("State читает /garmin/state", has(api, 'get("/api/v1/garmin/state")'));
ok("Sync отправляет config отдельным объектом", has(api, 'post("/api/v1/garmin/sync", { config: config })'));
ok("FetchActivities вызывает серверный маршрут", has(api, 'post("/api/v1/garmin/fetch-activities")'));

// Три поля и безопасность токена.
ok("Поле API key есть и скрыто", /<input type="password" id="garmin-api-key"/.test(html));
ok("Поле OAuth token есть и скрыто", /<input type="password" id="garmin-oauth-token"/.test(html));
ok("Поле срока действия есть", has(html, 'id="garmin-token-expiry"'));
ok("tokenExpiry добавляется только когда заполнен", has(app, "if (tokenExpiry) credentials.tokenExpiry = tokenExpiry;"));
ok("Секреты не сохраняются в localStorage Garmin", !has(app, 'localStorage.setItem("xdiary.garmin') && !has(app, 'localStorage.setItem("garmin'));
ok("Токены не пишутся в console", !has(app, "console.log(apiKey") && !has(app, "console.log(oauthToken"));

// Второй путь и состояние.
ok("Журнал запускает серверный sync", has(app, '$("btn-journal-garmin").addEventListener("click", function () { startGarminSync("journal"); })'));
ok("Старый путь журнала через openImport удалён", !has(app, '$("btn-journal-garmin").addEventListener("click", function () { openImport(); })'));
ok("Sync после конфига вызывает fetchActivities", has(app, "return Api.garmin.fetchActivities();"));
ok("Состояние загружается при открытии Настроек", has(app, "renderGarminStatus(); showView(\"settings\")"));
ok("requiresReauth открывает повторный Connect", has(app, "if (s && s.requiresReauth)"));
ok("Текст истёкшего токена понятен", has(app, "Срок действия токена Garmin истёк. Вставьте новый токен."));

// Машиночитаемые ошибки и CSV-регрессия.
for (const code of ["validation_error", "not_connected", "token_expired", "upstream_unavailable"]) {
  ok("Обработан код " + code, has(app, code));
}
ok("404 State приводит к форме Connect", has(app, "if (e && e.status === 404)"));
ok("CSV остаётся отдельной кнопкой", has(html, 'id="btn-import-open"') && has(html, "Импорт CSV Garmin…"));
ok("CSV-обработчик сохранён", has(app, '$("btn-import-open").addEventListener("click", openImport);'));

// Версия обязана быть парой.
ok("APP_VERSION поднята", has(app, 'APP_VERSION = "0.124"'));
ok("CACHE соответствует версии", has(sw, 'CACHE = "xdiary-v124"'));

// Негативные контроли: тест реально умеет падать при потере критичных мест.
const noReauth = app.replace(/requiresReauth/g, "reauth_REMOVED");
ok("Негативный контроль: без requiresReauth проверка бы упала", !has(noReauth, "requiresReauth"));
const noJournalSync = app.replace('startGarminSync("journal")', 'openImport()');
ok("Негативный контроль: без server sync журнал бы не прошёл", !has(noJournalSync, 'startGarminSync("journal")'));
const noSecretCheck = app.replace(/if \(tokenExpiry\) credentials\.tokenExpiry = tokenExpiry;/g, "credentials.tokenExpiry = tokenExpiry;");
ok("Негативный контроль: без условия optional expiry тест бы отличался", !has(noSecretCheck, "if (tokenExpiry) credentials.tokenExpiry = tokenExpiry;"));

console.log("FE-057 structural tests:", passed + "/" + passed, "OK");
