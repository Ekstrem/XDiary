/* XDiary — service worker: офлайн и установка */
var CACHE = "xdiary-v85"; // синхронизировать с APP_VERSION в app.js
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
          if (req.mode === "navigate") return caches.match("./index.html");
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
        if (req.mode === "navigate") return caches.match("./index.html");
        return new Response("", { status: 503, statusText: "offline" });
      });
    })
  );
});
