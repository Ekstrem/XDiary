/* XDiary — обёртка над IndexedDB (локальное хранилище, без сети) */
(function () {
  "use strict";

  var DB_NAME = "xdiary";
  var DB_VERSION = 5;
  var _db = null;

  function openDB() {
    if (_db) return Promise.resolve(_db);
    return new Promise(function (resolve, reject) {
      var req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onupgradeneeded = function (e) {
        var db = e.target.result;
        if (!db.objectStoreNames.contains("records")) {
          var st = db.createObjectStore("records", { keyPath: "id" });
          st.createIndex("startedAt", "startedAt");
        }
        if (!db.objectStoreNames.contains("meta")) {
          db.createObjectStore("meta", { keyPath: "key" });
        }
        if (!db.objectStoreNames.contains("partners")) {
          db.createObjectStore("partners", { keyPath: "id" });
        }
        if (!db.objectStoreNames.contains("catalog")) {
          var cs = db.createObjectStore("catalog", { keyPath: "id" });
          cs.createIndex("kind", "kind");
        }
        if (!db.objectStoreNames.contains("somatic")) {
          db.createObjectStore("somatic", { keyPath: "t" });
        }
        if (!db.objectStoreNames.contains("sleep")) {
          db.createObjectStore("sleep", { keyPath: "date" });
        }
        if (!db.objectStoreNames.contains("weekly")) {
          db.createObjectStore("weekly", { keyPath: "date" });
        }
        if (!db.objectStoreNames.contains("sleep_weekly")) {
          db.createObjectStore("sleep_weekly", { keyPath: "end" });
        }
      };
      req.onsuccess = function () { _db = req.result; resolve(_db); };
      req.onerror = function () { reject(req.error); };
    });
  }

  function request(req) {
    return new Promise(function (resolve, reject) {
      req.onsuccess = function () { resolve(req.result); };
      req.onerror = function () { reject(req.error); };
    });
  }

  function txDone(t) {
    return new Promise(function (resolve, reject) {
      t.oncomplete = function () { resolve(); };
      t.onerror = function () { reject(t.error); };
      t.onabort = function () { reject(t.error); };
    });
  }

  function getAll(storeName) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readonly");
      return request(t.objectStore(storeName).getAll()).then(function (all) { return all || []; });
    });
  }
  function getOne(storeName, id) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readonly");
      return request(t.objectStore(storeName).get(id));
    });
  }
  function putOne(storeName, obj) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readwrite");
      t.objectStore(storeName).put(obj);
      return txDone(t);
    });
  }
  function deleteOne(storeName, id) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readwrite");
      t.objectStore(storeName).delete(id);
      return txDone(t);
    });
  }
  function putBatch(storeName, list) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readwrite");
      var s = t.objectStore(storeName);
      list.forEach(function (obj) { s.put(obj); });
      return txDone(t);
    });
  }
  function getRange(storeName, start, end) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readonly");
      var req = t.objectStore(storeName).getAll(IDBKeyRange.bound(start, end));
      return request(req).then(function (all) { return all || []; });
    });
  }
  function clearStore(storeName) {
    return openDB().then(function (db) {
      var t = db.transaction(storeName, "readwrite");
      t.objectStore(storeName).clear();
      return txDone(t);
    });
  }

  window.DB = {
    // записи
    getAllRecords: function () { return getAll("records"); },
    getRecord: function (id) { return getOne("records", id); },
    putRecord: function (rec) { return putOne("records", rec); },
    deleteRecord: function (id) { return deleteOne("records", id); },
    clearRecords: function () { return clearStore("records"); },
    // мета (PIN и пр.)
    getMeta: function (key) { return getOne("meta", key); },
    setMeta: function (key, value) { return putOne("meta", { key: key, value: value }); },
    deleteMeta: function (key) { return deleteOne("meta", key); },
    // партнёры
    getAllPartners: function () { return getAll("partners"); },
    getPartner: function (id) { return getOne("partners", id); },
    putPartner: function (p) { return putOne("partners", p); },
    deletePartner: function (id) { return deleteOne("partners", id); },
    // каталог (костюмы / игрушки / фантазии)
    getAllCatalog: function () { return getAll("catalog"); },
    getCatalogItem: function (id) { return getOne("catalog", id); },
    putCatalogItem: function (item) { return putOne("catalog", item); },
    deleteCatalogItem: function (id) { return deleteOne("catalog", id); },
    // сырые данные IndexedDB для миграции (repository.js их не переопределяет)
    rawRecords: function () { return getAll("records"); },
    rawPartners: function () { return getAll("partners"); },
    rawCatalog: function () { return getAll("catalog"); },
    // соматика (time-series, ключ — минута "YYYY-MM-DDTHH:mm")
    putSomaticBatch: function (list) { return putBatch("somatic", list); },
    getSomaticRange: function (start, end) { return getRange("somatic", start, end); },
    clearSomatic: function () { return clearStore("somatic"); },
    // сон (ключ — дата "YYYY-MM-DD")
    putSleepBatch: function (list) { return putBatch("sleep", list); },
    getSleep: function (date) { return getOne("sleep", date); },
    getAllSleep: function () { return getAll("sleep"); },
    clearSleep: function () { return clearStore("sleep"); },
    // недельные тренды (стресс / пульс покоя, ключ — "YYYY-MM-DD")
    putWeeklyBatch: function (list) { return putBatch("weekly", list); },
    getAllWeekly: function () { return getAll("weekly"); },
    getWeekly: function (date) { return getOne("weekly", date); },
    clearWeekly: function () { return clearStore("weekly"); },
    // недельный сон (ключ — дата конца недели "YYYY-MM-DD")
    putSleepWeeklyBatch: function (list) { return putBatch("sleep_weekly", list); },
    getAllSleepWeekly: function () { return getAll("sleep_weekly"); },
    clearSleepWeekly: function () { return clearStore("sleep_weekly"); }
  };
})();
