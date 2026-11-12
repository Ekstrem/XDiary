/* XDiary — service worker: офлайн и установка */
var CACHE = "xdiary-v113"; // синхронизировать с APP_VERSION в app.js
var ASSETS = [
  "./",
  "./index.html",
  "./manifest.webmanifest",
  "./css/styles.css",
  "./js/db.js",
  "./js/pin.js",
  "./js/csv.js",
  "./js/api.js",
  "./js/repository.js",
  "./js/app.js",
  "./icons/icon-192.png",
  "./icons/icon-512.png",
  "./icons/icon-maskable-512.png"
];

self.addEventListener("install", function (e) {
  e.waitUntil(
    caches.open(CACHE).then(function (c) { return c.addAll(ASSETS); }).then(function () { return self.skipWaiting(); })
  );
});

self.addEventListener("activate", function (e) {
  e.waitUntil(
    caches.keys().then(function (keys) {
      // FE-035: удаляем только СВОИ старые кэши (xdiary-v*) — чужие кэши на origin не трогаем
      return Promise.all(keys.filter(function (k) { return k.indexOf("xdiary-") === 0 && k !== CACHE; })
        .map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); })
  );
});

// FE-046 (#25/#27): раньше здесь мог вернуться `undefined` из `caches.match("./index.html")`,
// и `respondWith(undefined)` даёт битый ответ: документа нет вообще, `#view-*` — ноль.
// Теперь последний рубеж — видимая страница ошибки вместо белого экрана.
function offlineShell() {
  return caches.match("./index.html").then(function (cached) {
    if (cached) return cached;
    return new Response(
      '<!doctype html><html lang="ru"><head><meta charset="utf-8">' +
      '<meta name="viewport" content="width=device-width, initial-scale=1">' +
      '<title>Нет связи</title></head><body style="font:16px/1.4 system-ui;padding:24px">' +
      '<h1>Нет связи с сервером</h1>' +
      '<p>Приложение не загрузилось: не удалось получить страницу. Проверьте сеть и нажмите «Повторить».</p>' +
      '<p><a href="./" style="display:inline-block;padding:12px 20px;background:#2563eb;color:#fff;' +
      'border-radius:8px;text-decoration:none">Повторить</a></p>' +
      '</body></html>',
      { status: 503, statusText: "offline", headers: { "Content-Type": "text/html; charset=utf-8" } }
    );
  });
}

self.addEventListener("fetch", function (e) {
  var req = e.request;
  if (req.method !== "GET") return;
  var url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // FE-035: код (навигация/js/css/html/manifest) — сначала СЕТЬ, иначе обновление страницы
  // не доезжает: старый app.js отдавался из кэша до ручной очистки
  var isCode = req.mode === "navigate" || /\.(?:js|css|html|webmanifest)$/.test(url.pathname);
  if (isCode) {
    e.respondWith(
      fetch(req).then(function (res) {
        if (res && res.status === 200) {
          var copy = res.clone();
          caches.open(CACHE).then(function (c) { c.put(req, copy); });
        }
        return res;
      }).catch(function () {
        return caches.match(req).then(function (cached) {
          if (cached) return cached;
          if (req.mode === "navigate") return offlineShell();
          return new Response("", { status: 503, statusText: "offline" });
        });
      })
    );
    return;
  }

  e.respondWith(
    caches.match(req).then(function (cached) {
      if (cached) return cached;
      return fetch(req).then(function (res) {
        if (res && res.status === 200) {
          var copy = res.clone();
          caches.open(CACHE).then(function (c) { c.put(req, copy); });
        }
        return res;
      }).catch(function () {
        if (req.mode === "navigate") return offlineShell();
        return new Response("", { status: 503, statusText: "offline" });
      });
    })
  );
});
