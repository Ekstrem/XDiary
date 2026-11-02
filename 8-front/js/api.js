/* XDiary — API-клиент к шлюзу микросервисов (8-front).
 * Бэкенд: .NET 8 (ApiGateway, YARP) + 10 BC. Формат JSON — camelCase.
 * JWT (FE-002): access в localStorage, заголовок Authorization: Bearer.
 */
(function () {
  "use strict";

  var ACCESS_KEY = "xdiary.accessToken";
  var REFRESH_KEY = "xdiary.refreshToken";
  var BASE_KEY = "xdiary.apiBase";
  var DEFAULT_BASE = "https://xdiary-vps1.taile0b6ee.ts.net";

  function storeGet(k) { try { return localStorage.getItem(k) || ""; } catch (e) { return ""; } }
  function storeSet(k, v) { try { if (v) localStorage.setItem(k, v); else localStorage.removeItem(k); } catch (e) {} }

  function token() { return storeGet(ACCESS_KEY); }
  // FE-024: произвольный сервер разрешён ТОЛЬКО для E2E; иначе «застрявший» ключ вычищаем и идём на прод
  function baseUrl() {
    if (window.__XDIARY_E2E__ === true) {
      var custom = storeGet(BASE_KEY);
      if (custom) return custom;
    } else {
      storeSet(BASE_KEY, "");
    }
    return DEFAULT_BASE;
  }
  function refreshToken() { return storeGet(REFRESH_KEY); }

  // FE-028: обновление пары по refresh-токену; одна попытка на всех (single-flight), без шторма
  var refreshInflight = null;
  function authRefresh() {
    if (refreshInflight) return refreshInflight;
    var rt = refreshToken();
    if (!rt) return Promise.reject(Object.assign(new Error("Нет refresh-токена"), { status: 401 }));
    refreshInflight = fetch(baseUrl() + "/api/v1/clients/refresh", {
      method: "POST",
      headers: { "Accept": "application/json", "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: rt })
    }).then(function (r) {
      if (!r.ok) { var e = new Error("HTTP " + r.status); e.status = r.status; throw e; }
      return r.json();
    }).then(function (data) {
      var a = data && (data.accessToken || data.token);
      if (!a) throw new Error("refresh: нет accessToken в ответе");
      storeSet(ACCESS_KEY, a);
      if (data.refreshToken) storeSet(REFRESH_KEY, data.refreshToken);
      return a;
    }).then(function (a) { refreshInflight = null; return a; }, function () {
      refreshInflight = null;
      // 401/400/сеть — одинаково: сессия истекла, уходим в онбординг
      throw Object.assign(new Error("Сессия истекла — войдите заново"), { status: 401 });
    });
    return refreshInflight;
  }

  function request(method, path, body, isRetry) {
    var base = baseUrl();
    var headers = { "Accept": "application/json" };
    if (token()) headers["Authorization"] = "Bearer " + token();
    var opts = { method: method, headers: headers };
    if (body !== undefined && body !== null) {
      headers["Content-Type"] = "application/json";
      opts.body = JSON.stringify(body);
    }
    return fetch(base + path, opts).catch(function () {
      throw new Error("Недоступен API (шлюз) — проверьте сеть/адрес");
    }).then(function (r) {
      if (r.status === 401 && !isRetry && refreshToken() && path.indexOf("/privacy/token") !== 0 && path.indexOf("/clients/refresh") !== 0) {
        return authRefresh().then(function () { return request(method, path, body, true); })
          .catch(function () { var e = new Error("Сессия истекла (401)"); e.status = 401; throw e; });
      }
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

  window.Api = {
    config: {
      base: function () { return baseUrl(); },
      token: token,
      setToken: function (t) { storeSet(ACCESS_KEY, t); },
      refresh: function () { return storeGet(REFRESH_KEY); },
      setRefresh: function (t) { storeSet(REFRESH_KEY, t); },
      clear: function () { storeSet(ACCESS_KEY, ""); storeSet(REFRESH_KEY, ""); }
    },
    auth: {
      login: function (pin) {
        return post("/api/v1/privacy/token", { pin: pin }).then(function (d) {
          storeSet(ACCESS_KEY, d.accessToken);
          storeSet(REFRESH_KEY, d.refreshToken);
          return d;
        });
      },
      refresh: function () { return authRefresh(); },
      logout: function () { storeSet(ACCESS_KEY, ""); storeSet(REFRESH_KEY, ""); }
    },
    privacy: {
      hasPin: function () { return get("/api/v1/privacy/pin/has"); },
      setPin: function (pin, currentPin) { return post("/api/v1/privacy/pin/set", { pin: pin, currentPin: currentPin }); },
      verifyPin: function (pin) { return post("/api/v1/privacy/pin/verify", { pin: pin }); },
      removePin: function (currentPin) { return post("/api/v1/privacy/pin/remove", { currentPin: currentPin }); },
      token: function (pin) { return post("/api/v1/privacy/token", { pin: pin }); }
    },
    somatic: {
      slice: function (fromT, toT) { return get("/api/v1/somatic-series/slice?fromT=" + encodeURIComponent(fromT) + "&toT=" + encodeURIComponent(toT)); }
    },
    daily: {
      series: function (fromDate, toDate) { return get("/api/v1/daily-metrics/series?fromDate=" + encodeURIComponent(fromDate) + "&toDate=" + encodeURIComponent(toDate)); }
    },

    // Журнал: цепочка команд (StartManualEntry → Specify* → Complete)
    journal: {
      list: function () { return get("/api/v1/journal/records"); },
      recent: function () { return get("/api/v1/journal/recent"); },
      places: function () { return get("/api/v1/journal/places"); },
      fantasies: function () { return get("/api/v1/journal/fantasies"); },
      get: function (id) { return get("/api/v1/journal/records/" + id); },
      start: function () { return post("/api/v1/journal/records"); },
      startFromGarmin: function (d) { return post("/api/v1/journal/records/from-garmin", d); },
      when: function (id, startedAt) { return post("/api/v1/journal/records/" + id + "/when", { startedAt: startedAt }); },
      addPartner: function (id, partnerId) { return post("/api/v1/journal/records/" + id + "/partners", { partnerId: partnerId }); },
      removePartner: function (id, partnerId) { return post("/api/v1/journal/records/" + id + "/partners/" + partnerId + "/remove"); },
      place: function (id, place) { return post("/api/v1/journal/records/" + id + "/place", { place: place }); },
      positions: function (id, positions) { return post("/api/v1/journal/records/" + id + "/positions", { positions: positions }); },
      accessories: function (id, d) { return post("/api/v1/journal/records/" + id + "/accessories", d); },
      contraception: function (id, d) { return post("/api/v1/journal/records/" + id + "/contraception", d); },
      duration: function (id, durationMin) { return post("/api/v1/journal/records/" + id + "/duration", { durationMin: durationMin }); },
      orgasms: function (id, orgasms) { return post("/api/v1/journal/records/" + id + "/orgasms", { orgasms: orgasms }); },
      complete: function (id) { return post("/api/v1/journal/records/" + id + "/complete"); },
      remove: function (id) { return post("/api/v1/journal/records/" + id + "/delete"); }
    },

    partners: {
      list: function () { return get("/api/v1/partners"); },
      get: function (id) { return get("/api/v1/partners/" + id); },
      register: function (name) { return post("/api/v1/partners", { name: name }); },
      rename: function (id, name) { return post("/api/v1/partners/" + id + "/name", { name: name }); },
      profile: function (id, d) { return post("/api/v1/partners/" + id + "/profile", d); },
      linkClient: function (id, clientId) { return post("/api/v1/partners/" + id + "/link-client", { clientId: clientId }); },
      unlinkClient: function (id) { return post("/api/v1/partners/" + id + "/unlink-client"); },
      remove: function (id) { return post("/api/v1/partners/" + id + "/remove"); }
    },

    catalog: {
      list: function () { return get("/api/v1/catalog"); },
      get: function (id) { return get("/api/v1/catalog/" + id); },
      add: function (kind, name) { return post("/api/v1/catalog", { kind: kind, name: name }); },
      rename: function (id, name) { return post("/api/v1/catalog/" + id + "/name", { name: name }); },
      details: function (id, d) { return post("/api/v1/catalog/" + id + "/details", d); },
      archive: function (id) { return post("/api/v1/catalog/" + id + "/archive"); },
      restore: function (id) { return post("/api/v1/catalog/" + id + "/restore"); },
      remove: function (id) { return post("/api/v1/catalog/" + id + "/remove"); }
    },

    // FE-017: аналитика с сервера (MV) — корреляции/сводка/топы/распределение
    analytics: {
      summary: function (period) { return get("/api/v1/analytics/summary?period=" + encodeURIComponent(period || "30")); },
      correlations: function () { return get("/api/v1/analytics/correlations"); },
      tops: function () { return get("/api/v1/analytics/tops"); },
      distribution: function () { return get("/api/v1/analytics/distribution"); }
    },

    // FE-018: BC «Клиент» — телефон/SMS OTP, client-JWT, link-partner
    clients: {
      register: function (phone) { return post("/api/v1/clients", { phone: phone }); },
      verify: function (id, code) { return post("/api/v1/clients/" + id + "/verify", { code: code }); },
      linkPartner: function (id, partnerId) { return post("/api/v1/clients/" + id + "/link-partner", { partnerId: partnerId }); },
      unlinkPartner: function (id) { return post("/api/v1/clients/" + id + "/unlink-partner"); },
      me: function () { return get("/api/v1/clients/me"); },
      get: function (id) { return get("/api/v1/clients/" + id); }
    }
  };
})();
