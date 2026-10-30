/* XDiary — API-клиент к шлюзу микросервисов (8-front).
 * Бэкенд: .NET 8 (ApiGateway, YARP) + 10 BC. Формат JSON — camelCase.
 * JWT (FE-002): access в localStorage, заголовок Authorization: Bearer.
 */
(function () {
  "use strict";

  var ACCESS_KEY = "xdiary.accessToken";
  var REFRESH_KEY = "xdiary.refreshToken";
  var BASE_KEY = "xdiary.apiBase";

  function storeGet(k) { try { return localStorage.getItem(k) || ""; } catch (e) { return ""; } }
  function storeSet(k, v) { try { if (v) localStorage.setItem(k, v); else localStorage.removeItem(k); } catch (e) {} }

  function token() { return storeGet(ACCESS_KEY); }

  function request(method, path, body) {
    var base = storeGet(BASE_KEY);
    if (!base) return Promise.reject(new Error("Не задан адрес API (шлюза)"));
    var headers = { "Accept": "application/json" };
    if (token()) headers["Authorization"] = "Bearer " + token();
    var opts = { method: method, headers: headers };
    if (body !== undefined && body !== null) {
      headers["Content-Type"] = "application/json";
      opts.body = JSON.stringify(body);
    }
    return fetch(base + path, opts).then(function (r) {
      if (r.status === 204) return null;
      var ct = r.headers.get("content-type") || "";
      var p = (ct.indexOf("json") !== -1) ? r.json() : r.text();
      return p.then(function (data) {
        if (!r.ok) {
          var err = new Error("HTTP " + r.status);
          err.status = r.status;
          err.data = data;
          throw err;
        }
        return data;
      });
    });
  }

  function get(p) { return request("GET", p); }
  function post(p, b) { return request("POST", p, b); }
  function put(p, b) { return request("PUT", p, b); }
  function del(p) { return request("DELETE", p); }

  window.Api = {
    config: {
      base: function () { return storeGet(BASE_KEY); },
      setBase: function (u) { storeSet(BASE_KEY, u); },
      token: token,
      setToken: function (t) { storeSet(ACCESS_KEY, t); },
      refresh: function () { return storeGet(REFRESH_KEY); },
      setRefresh: function (t) { storeSet(REFRESH_KEY, t); },
      clear: function () { storeSet(ACCESS_KEY, ""); storeSet(REFRESH_KEY, ""); }
    },

    // Журнал: цепочка команд (StartManualEntry → Specify* → Complete)
    journal: {
      list: function () { return get("/api/v1/journal/records"); },
      get: function (id) { return get("/api/v1/journal/records/" + id); },
      start: function () { return post("/api/v1/journal/records"); },
      startFromGarmin: function (d) { return post("/api/v1/journal/records/from-garmin", d); },
      when: function (id, startedAt) { return post("/api/v1/journal/records/" + id + "/when", { startedAt: startedAt }); },
      addPartner: function (id, partnerId) { return post("/api/v1/journal/records/" + id + "/partners", { partnerId: partnerId }); },
      removePartner: function (id, partnerId) { return del("/api/v1/journal/records/" + id + "/partners/" + partnerId); },
      place: function (id, place) { return post("/api/v1/journal/records/" + id + "/place", { place: place }); },
      positions: function (id, positions) { return post("/api/v1/journal/records/" + id + "/positions", { positions: positions }); },
      accessories: function (id, d) { return post("/api/v1/journal/records/" + id + "/accessories", d); },
      contraception: function (id, d) { return post("/api/v1/journal/records/" + id + "/contraception", d); },
      duration: function (id, durationMin) { return post("/api/v1/journal/records/" + id + "/duration", { durationMin: durationMin }); },
      orgasms: function (id, orgasms) { return post("/api/v1/journal/records/" + id + "/orgasms", { orgasms: orgasms }); },
      complete: function (id) { return post("/api/v1/journal/records/" + id + "/complete"); },
      remove: function (id) { return del("/api/v1/journal/records/" + id); }
    },

    partners: {
      list: function () { return get("/api/v1/partners"); },
      get: function (id) { return get("/api/v1/partners/" + id); },
      register: function (name) { return post("/api/v1/partners", { name: name }); },
      rename: function (id, name) { return put("/api/v1/partners/" + id + "/name", { name: name }); },
      profile: function (id, d) { return put("/api/v1/partners/" + id + "/profile", d); },
      remove: function (id) { return del("/api/v1/partners/" + id); }
    },

    catalog: {
      list: function () { return get("/api/v1/catalog"); },
      get: function (id) { return get("/api/v1/catalog/" + id); },
      add: function (kind, name) { return post("/api/v1/catalog", { kind: kind, name: name }); },
      rename: function (id, name) { return put("/api/v1/catalog/" + id + "/name", { name: name }); },
      details: function (id, d) { return put("/api/v1/catalog/" + id + "/details", d); },
      archive: function (id) { return post("/api/v1/catalog/" + id + "/archive"); },
      restore: function (id) { return post("/api/v1/catalog/" + id + "/restore"); },
      remove: function (id) { return del("/api/v1/catalog/" + id); }
    }
  };
})();
