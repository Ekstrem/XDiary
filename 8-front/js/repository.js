/* XDiary — репозиторий (FE-001).
 * Переопределяет методы window.DB для журнала/партнёров/каталога на API-шлюз.
 * Соматика/сон/тренды/мета остаются в IndexedDB (db.js).
 * Маппинг: UI-модель ↔ API-DTO (camelCase).
 */
(function () {
  "use strict";
  var DB = window.DB;

  function nameMap(partners) {
    var m = {};
    (partners || []).forEach(function (p) { m[p.id] = p.name; });
    return m;
  }
  // FE-041: карта «id → удалён» — чтобы отличать мягко удалённого партнёра от ненайденного.
  // GET /partners удалённых не отдаёт, поэтому по умолчанию карта пуста и состояние = «недоступен».
  function deletedMap(partners) {
    var m = {};
    (partners || []).forEach(function (p) { if (p.deletedAt) m[p.id] = true; });
    return m;
  }
  function findPartnerId(partners, name) {
    for (var i = 0; i < (partners || []).length; i++) if (partners[i].name === name) return partners[i].id;
    return "";
  }

  // JournalReadModel → UI-запись (одного партнёра, плоские оргазмы)
  function mapRecord(rm, names, deleted) {
    var me = null, partner = null;
    (rm.orgasms || []).forEach(function (o) {
      if (o.participant === "me") me = o; else partner = o;
    });
    var pid = (rm.partners && rm.partners[0]) || "";
    var pname = names[pid] || "";
    return {
      id: rm.id,
      startedAt: rm.startedAt || "",
      durationMin: rm.durationMin || 0,
      partner: pname,
      partnerId: pid,
      // FE-041: три состояния по BC-Journal/02 §2.7 —
      // имя / «удалённый партнёр» (deletedAt известен) / «участник недоступен» (id есть, имени нет).
      partnerDeleted: !!pid && !!(deleted && deleted[pid]),
      place: rm.place || "",
      positions: rm.positions || [],
      tags: rm.tags || [],
      costumes: rm.costumes || [],
      toys: rm.toys || [],
      fantasies: rm.fantasies || [],
      contraceptives: rm.contraceptives || [],
      contraceptiveCount: rm.contraceptiveCount || 0,
      orgasmsMe: me ? (me.count || 0) : 0,
      orgasmsPartner: partner ? (partner.count || 0) : 0,
      orgasmMeType: me ? (me.types || []) : [],
      orgasmPartnerType: partner ? (partner.types || []) : [],
      orgasmMeRating: me ? (me.rating || 0) : 0,
      orgasmPartnerRating: partner ? (partner.rating || 0) : 0,
      garmin: !!rm.garmin,
      garminId: rm.garminId || "",
      createdAt: rm.createdAt || "",
      updatedAt: rm.updatedAt || ""
    };
  }

  function mapPartner(rm) {
    return { id: rm.id, name: rm.name || "", photo: rm.photo || "", gender: rm.gender || "", notes: rm.notes || "", clientId: rm.clientId || "", deletedAt: rm.deletedAt || "" };
  }

  function mapCatalogItem(rm) {
    return {
      id: rm.id, kind: rm.kind || "", name: rm.name || "", photo: rm.photo || "",
      notes: rm.notes || "", brand: rm.brand || "", archived: !!rm.archived, partner: rm.fantasyPartner || ""
    };
  }

  // ---- Журнал ----
  DB.getAllRecords = function () {
    // @AR 2026-11-10: раньше здесь был прямой Api.partners.list() — второй GET /partners
    // на каждую отрисовку журнала мимо кэша. Берём через DB.getAllPartners (мемо 2 с).
    return DB.getAllPartners().then(function (partners) {
      var names = nameMap(partners);
      var dels = deletedMap(partners);
      return Api.journal.list().then(function (list) { return (list || []).map(function (rm) { return mapRecord(rm, names, dels); }); });
    });
  };
  // FE-015: один запрос /partners + один /journal — визард не должен ходить в API на каждый шаг
  DB.getAllJournalData = function () {
    return DB.getAllPartners().then(function (partners) {
      var names = nameMap(partners);
      var dels = deletedMap(partners);
      return Api.journal.list().then(function (list) {
        return {
          partners: partners || [],
          records: (list || []).map(function (rm) { return mapRecord(rm, names, dels); })
        };
      });
    });
  };
  DB.getRecord = function (id) {
    return Api.partners.list().then(function (partners) {
      var names = nameMap(partners);
      var dels = deletedMap(partners);
      return Api.journal.get(id).then(function (rm) { return rm ? mapRecord(rm, names, dels) : null; });
    });
  };
  DB.deleteRecord = function (id) { return Api.journal.remove(id); };

  DB.putRecord = function (rec) {
    return Api.partners.list().then(function (partners) {
      var partnerId = rec.partnerId || findPartnerId(partners, rec.partner);
      var orgasms = [];
      if (rec.orgasmsMe > 0) orgasms.push({ participant: "me", count: rec.orgasmsMe, types: rec.orgasmMeType || [], rating: rec.orgasmMeRating || 0 });
      if (rec.orgasmsPartner > 0 && partnerId) orgasms.push({ participant: partnerId, count: rec.orgasmsPartner, types: rec.orgasmPartnerType || [], rating: rec.orgasmPartnerRating || 0 });

      var startP = rec.id ? Promise.resolve(rec.id) : Api.journal.start().then(function (m) { return m.id; });
      return startP.then(function (id) {
        var chain = Promise.resolve();
        if (rec.id) {
          chain = chain.then(function () {
            return Api.journal.get(id).then(function (cur) {
              var seq = Promise.resolve();
              ((cur && cur.partners) || []).forEach(function (pid) {
                seq = seq.then(function () { return Api.journal.removePartner(id, pid); });
              });
              return seq;
            });
          });
        }
        chain = chain.then(function () { return Api.journal.when(id, rec.startedAt || ""); });
        if (partnerId) chain = chain.then(function () { return Api.journal.addPartner(id, partnerId); });
        chain = chain.then(function () { return Api.journal.place(id, rec.place || ""); });
        chain = chain.then(function () { return Api.journal.positions(id, rec.positions || []); });
        chain = chain.then(function () {
          return Api.journal.accessories(id, { costumes: rec.costumes || [], toys: rec.toys || [], fantasies: rec.fantasies || [], tags: rec.tags || [] });
        });
        chain = chain.then(function () { return Api.journal.contraception(id, { contraceptives: rec.contraceptives || [], contraceptiveCount: rec.contraceptiveCount || 0 }); });
        chain = chain.then(function () { return Api.journal.duration(id, rec.durationMin || 0); });
        chain = chain.then(function () { return Api.journal.orgasms(id, orgasms); });
        chain = chain.then(function () { return Api.journal.complete(id); });
        return chain.then(function () { rec.id = id; return rec; });
      });
    });
  };

  // ---- Партнёры ----
  // FE-044 / read-paths §1 п.2: смешанная свежесть обязана быть названа.
  // Порог «данные могли устареть» — STALE_AFTER_HOURS (24 ч). Признак показывается КОГДА ДАННЫЕ
  // ПРИШЛИ ИЗ КЭША: живое чтение упало, а в IndexedDB копия есть. Свежие live-данные признака не дают.
  var STALE_AFTER_HOURS = 24;
  var fresh = { partners: null, catalog: null };
  function syncKey(kind) { return "syncAt." + kind; }
  function markSync(kind) { try { DB.setMeta(syncKey(kind), Date.now()); } catch (e) {} }
  function withCache(kind, live, cached) {
    return live().then(function (v) {
      markSync(kind);
      fresh[kind] = null;
      return v;
    }).catch(function (err) {
      return cached().then(function (rows) {
        if (!rows || !rows.length) { fresh[kind] = null; throw err; }
        return DB.getMeta(syncKey(kind)).then(function (m) {
          var at = (m && m.value) || 0;
          fresh[kind] = { fromCache: true, ageMs: at ? (Date.now() - at) : null };
          return rows;
        });
      });
    });
  }
  DB.readFreshness = function (kind) { return fresh[kind] || null; };

  // Мемо на 2 с: журнал и визард зовут партнёров в одном тике, второй запрос не нужен.
  var partnersMemo = null, partnersMemoAt = 0;
  var PARTNERS_MEMO_MS = 2000;
  function dropPartnersMemo() { partnersMemo = null; partnersMemoAt = 0; }
  DB.getAllPartners = function () {
    var now = Date.now();
    if (partnersMemo && (now - partnersMemoAt) < PARTNERS_MEMO_MS) return Promise.resolve(partnersMemo);
    return withCache("partners",
      function () { return Api.partners.list().then(function (l) { return (l || []).map(mapPartner); }); },
      function () { return DB.rawPartners().then(function (l) { return (l || []).map(mapPartner); }); })
      .then(function (list) { partnersMemo = list; partnersMemoAt = Date.now(); return list; });
  };
  DB.getPartner = function (id) { return Api.partners.get(id).then(function (rm) { return rm ? mapPartner(rm) : null; }); };
  // FE-050: мемо сбрасываем и на успехе — иначе вызов getAllPartners() между
  // сбросом и ответом заполнит мемо СТАРЫМ списком на PARTNERS_MEMO_MS.
  DB.deletePartner = function (id) {
    dropPartnersMemo();
    return Api.partners.remove(id).then(function (r) { dropPartnersMemo(); return r; });
  };
  // FE-045: profile после register/rename нужен только если форма отличается от дефолтов
  // (photo="", gender="female", notes="") — иначе это лишний вызов в цепочке создания.
  function needProfile(p) {
    if (p.photo) return true;
    if (p.gender && p.gender !== "female") return true;
    if (p.notes && String(p.notes).trim()) return true;
    return false;
  }
  // FE-045: один retry на 400/404 для идемпотентного profile — проекция может отставать.
  // Логируем «retry after lag», чтобы не превратить настоящую 400 в молчаливое скрытие.
  function retryOnce(fn) {
    return fn().catch(function (e) {
      var st = e && e.status;
      if (st !== 400 && st !== 404) throw e;
      return new Promise(function (res) { setTimeout(res, 200); }).then(function () {
        console.log("retry after lag: profile → " + st);
        return fn();
      });
    });
  }

  DB.putPartner = function (p) {
    dropPartnersMemo();
    var settle = function (v) { dropPartnersMemo(); return v; }; // FE-050: и на успехе
    if (p.id) {
      return Api.partners.rename(p.id, p.name).then(function () {
        if (!needProfile(p)) return settle(p);
        return retryOnce(function () {
          return Api.partners.profile(p.id, { photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" });
        }).then(function () { return settle(p); });
      });
    }
    return Api.partners.register(p.name).then(function (m) {
      var id = m.id;
      p.id = id;
      if (!needProfile(p)) return settle(p);
      return retryOnce(function () {
        return Api.partners.profile(id, { photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" });
      }).then(function () { return settle(p); });
    });
  };

  // ---- Каталог ----
  DB.getAllCatalog = function () {
    return withCache("catalog",
      function () { return Api.catalog.list().then(function (l) { return (l || []).map(mapCatalogItem); }); },
      function () { return DB.rawCatalog().then(function (l) { return (l || []).map(mapCatalogItem); }); });
  };
  DB.getCatalogItem = function (id) { return Api.catalog.get(id).then(function (rm) { return rm ? mapCatalogItem(rm) : null; }); };
  DB.deleteCatalogItem = function (id) { return Api.catalog.remove(id); };
  DB.putCatalogItem = function (it) {
    if (it.id) {
      var seq = Api.catalog.rename(it.id, it.name);
      seq = seq.then(function () {
        return Api.catalog.details(it.id, { photo: it.photo || "", notes: it.notes || "", brand: it.brand || "", fantasyPartner: it.partner || "" });
      });
      seq = seq.then(function () { return it.archived ? Api.catalog.archive(it.id) : Api.catalog.restore(it.id); });
      return seq.then(function () { return it; });
    }
    return Api.catalog.add(it.kind, it.name).then(function (m) {
      var id = m.id;
      return Api.catalog.details(id, { photo: it.photo || "", notes: it.notes || "", brand: it.brand || "", fantasyPartner: it.partner || "" })
        .then(function () { it.id = id; return it; });
    });
  };
})();
