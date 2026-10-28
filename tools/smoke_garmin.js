// Smoke-тест парсинга отчётов Garmin Connect (копия логики из js/app.js).
// Запуск: node tools/smoke_garmin.js "путь/к/файлу.csv" ...
const fs = require("fs");
const path = require("path");

global.window = {};
eval(fs.readFileSync(path.join(__dirname, "..", "js", "csv.js"), "utf8"));
const CSV = global.window.CSV;

function pad(n) { return String(n).padStart(2, "0"); }
const RU_MONTHS = { "янв": 1, "фев": 2, "мар": 3, "апр": 4, "май": 5, "июн": 6, "июл": 7, "авг": 8, "сен": 9, "окт": 10, "ноя": 11, "дек": 12 };

function parseUSDate(s) {
  var parts = String(s || "").trim().split("/");
  if (parts.length !== 3) return null;
  var a = parseInt(parts[0], 10), b = parseInt(parts[1], 10), y = parseInt(parts[2], 10);
  var m, d;
  if (a > 12 && b <= 12) { m = b; d = a; } else { m = a; d = b; }
  if (!m || !d || isNaN(y)) return null;
  return y + "-" + pad(m) + "-" + pad(d);
}
function monthOf(label) {
  var s = String(label).toLowerCase().trim();
  for (var key in RU_MONTHS) { if (s.indexOf(key) === 0) return RU_MONTHS[key]; }
  return null;
}
function parsePart(part, defaultMonth) {
  var s = String(part).trim().toLowerCase();
  var m = null;
  for (var key in RU_MONTHS) { if (s.indexOf(key) === 0) { m = RU_MONTHS[key]; s = s.slice(key.length); break; } }
  var day = parseInt(s, 10);
  return { month: (m || defaultMonth), day: day };
}
function parseDurationRu(s) {
  var str = String(s || "").toLowerCase();
  var hm = /(\d+)\s*ч/.exec(str);
  var mm = /(\d+)\s*мин/.exec(str);
  var h = hm ? parseInt(hm[1], 10) : 0;
  var m = mm ? parseInt(mm[1], 10) : 0;
  return h * 60 + m;
}
function parseGarminTwoCol(text, field) {
  text = String(text || "").replace(/^\uFEFF/, "");
  var rows = CSV.parse(text);
  var out = [];
  for (var r = 1; r < rows.length; r++) {
    var row = rows[r];
    if (!row || row.length < 2) continue;
    var date = parseUSDate(row[0]);
    var val = parseInt(row[1], 10);
    if (!date || isNaN(val)) continue;
    var obj = { date: date };
    obj[field] = val;
    out.push(obj);
  }
  return out;
}
function parseSleepWeekly(text) {
  text = String(text || "").replace(/^\uFEFF/, "");
  var rows = CSV.parse(text);
  var out = [];
  var currentYear = new Date().getFullYear();
  for (var r = 1; r < rows.length; r++) {
    var row = rows[r];
    if (!row || row.length < 4) continue;
    var label = String(row[0]).trim();
    var norm = label.replace(/\s+-\s+/g, "|").replace(/-/g, "|");
    var parts = norm.split("|");
    if (parts.length < 2) continue;
    var first = parsePart(parts[0], null);
    var second = parsePart(parts[1], first.month);
    if (!first.month || isNaN(first.day) || isNaN(second.day)) continue;
    var hasYear = /^\s*\d{4}\s*$/.test(String(row[1]));
    var year = hasYear ? parseInt(row[1], 10) : currentYear;
    var score = hasYear ? row[2] : row[1];
    var quality = hasYear ? row[3] : row[2];
    var durationStr = hasYear ? row[4] : row[3];
    currentYear = year;
    out.push({
      end: year + "-" + pad(second.month) + "-" + pad(second.day),
      start: year + "-" + pad(first.month) + "-" + pad(first.day),
      label: label,
      score: parseInt(score, 10) || null,
      quality: String(quality || ""),
      durationMin: parseDurationRu(durationStr)
    });
  }
  return out;
}
function detectImportType(rows) {
  if (!rows || !rows.length) return null;
  var header = rows[0].map(function (h) { return String(h).trim().toLowerCase(); });
  var joined = header.join(" ").toLowerCase();
  if (header.indexOf("timestamp") !== -1 || header.indexOf("time") !== -1 || header.indexOf("t") !== -1 || header.indexOf("datetime") !== -1) return "somatic_minute";
  if (header.indexOf("date") !== -1 && (joined.indexOf("sleep") !== -1 || header.indexOf("sleep_score") !== -1 || header.indexOf("duration_min") !== -1 || header.indexOf("deep_min") !== -1)) return "sleep_daily";
  if (header[0] === "дата" && (joined.indexOf("сред") !== -1 || joined.indexOf("качеств") !== -1 || joined.indexOf("рез") !== -1)) return "sleep_weekly";
  if (joined.indexOf("стресс") !== -1) return "stress_weekly";
  if (joined.indexOf("покой") !== -1 || joined.indexOf("пульса") !== -1) return "resting_weekly";
  return null;
}

for (const file of process.argv.slice(2)) {
  const text = fs.readFileSync(file, "utf8");
  const rows = CSV.parse(text.replace(/^\uFEFF/, ""));
  const type = detectImportType(rows);
  console.log("\n=== " + path.basename(file) + " => " + type + " ===");
  if (type === "stress_weekly") { const it = parseGarminTwoCol(text, "stress"); console.log("count", it.length, "first", JSON.stringify(it[0]), "last", JSON.stringify(it[it.length - 1])); }
  else if (type === "resting_weekly") { const it = parseGarminTwoCol(text, "resting_hr"); console.log("count", it.length, "first", JSON.stringify(it[0]), "last", JSON.stringify(it[it.length - 1])); }
  else if (type === "sleep_weekly") { const it = parseSleepWeekly(text); console.log("count", it.length, "first", JSON.stringify(it[0]), "last", JSON.stringify(it[it.length - 1])); }
  else console.log("unhandled:", type);
}
