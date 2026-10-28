/* XDiary — обёртка над IndexedDB (локальное хранилище, без сети) */
(function () {
  "use strict";

  var DB_NAME = "xdiary";
  var DB_VERSION = 2;
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

  window.DB = {
    // записи
    getAllRecords: function () { return getAll("records"); },
    getRecord: function (id) { return getOne("records", id); },
    putRecord: function (rec) { return putOne("records", rec); },
    deleteRecord: function (id) { return deleteOne("records", id); },
    clearRecords: function () {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readwrite");
        t.objectStore("records").clear();
        return txDone(t);
      });
    },
    // мета (PIN и пр.)
    getMeta: function (key) { return getOne("meta", key); },
    setMeta: function (key, value) { return putOne("meta", { key: key, value: value }); },
    deleteMeta: function (key) { return deleteOne("meta", key); },
    // партнёры
    getAllPartners: function () { return getAll("partners"); },
    getPartner: function (id) { return getOne("partners", id); },
    putPartner: function (p) { return putOne("partners", p); },
    deletePartner: function (id) { return deleteOne("partners", id); }
  };
})();
