// Smoke-тест CSV-ядра (round-trip с запятыми, кавычками, переносами, кириллицей).
// Запуск: node tools/smoke_csv.js
const fs = require("fs");
const path = require("path");

global.window = {};
eval(fs.readFileSync(path.join(__dirname, "..", "js", "csv.js"), "utf8"));
const CSV = global.window.CSV;

const data = [
  ["a", "b"],
  ["hello, world", 'x"y'],
  ["line1\nline2", "z;z"],
  ["\u0440\u0443\u0441\u0441\u043a\u0438\u0439", "\u0442\u0435\u0433,\u0441 \u0437\u0430\u043f\u044f\u0442\u043e\u0439"]
];

const s = CSV.stringify(data);
const back = CSV.parse(s);
const ok = JSON.stringify(back) === JSON.stringify(data);
console.log("roundtrip ok:", ok);
console.log("serialized:", JSON.stringify(s));
process.exit(ok ? 0 : 1);
