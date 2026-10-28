/* XDiary — обёртка над IndexedDB (локальное хранилище, без сети) */
(function () {
  "use strict";

  var DB_NAME = "xdiary";
  var DB_VERSION = 1;
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

  window.DB = {
    getAllRecords: function () {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readonly");
        return request(t.objectStore("records").getAll()).then(function (all) { return all || []; });
      });
    },
    getRecord: function (id) {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readonly");
        return request(t.objectStore("records").get(id));
      });
    },
    putRecord: function (rec) {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readwrite");
        t.objectStore("records").put(rec);
        return txDone(t);
      });
    },
    deleteRecord: function (id) {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readwrite");
        t.objectStore("records").delete(id);
        return txDone(t);
      });
    },
    clearRecords: function () {
      return openDB().then(function (db) {
        var t = db.transaction("records", "readwrite");
        t.objectStore("records").clear();
        return txDone(t);
      });
    },
    getMeta: function (key) {
      return openDB().then(function (db) {
        var t = db.transaction("meta", "readonly");
        return request(t.objectStore("meta").get(key));
      });
    },
    setMeta: function (key, value) {
      return openDB().then(function (db) {
        var t = db.transaction("meta", "readwrite");
        t.objectStore("meta").put({ key: key, value: value });
        return txDone(t);
      });
    },
    deleteMeta: function (key) {
      return openDB().then(function (db) {
        var t = db.transaction("meta", "readwrite");
        t.objectStore("meta").delete(key);
        return txDone(t);
      });
    }
  };
})();
