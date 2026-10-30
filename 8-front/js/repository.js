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
  function findPartnerId(partners, name) {
    for (var i = 0; i < (partners || []).length; i++) if (partners[i].name === name) return partners[i].id;
    return "";
  }

  // JournalReadModel → UI-запись (одного партнёра, плоские оргазмы)
  function mapRecord(rm, names) {
    var me = null, partner = null;
    (rm.orgasms || []).forEach(function (o) {
      if (o.participant === "me") me = o; else partner = o;
    });
    var pid = (rm.partners && rm.partners[0]) || "";
    return {
      id: rm.id,
      startedAt: rm.startedAt || "",
      durationMin: rm.durationMin || 0,
      partner: names[pid] || "",
      partnerId: pid,
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
    return { id: rm.id, name: rm.name || "", photo: rm.photo || "", gender: rm.gender || "", notes: rm.notes || "" };
  }

  function mapCatalogItem(rm) {
    return {
      id: rm.id, kind: rm.kind || "", name: rm.name || "", photo: rm.photo || "",
      notes: rm.notes || "", brand: rm.brand || "", archived: !!rm.archived, partner: rm.fantasyPartner || ""
    };
  }

  // ---- Журнал ----
  DB.getAllRecords = function () {
    return Api.partners.list().then(function (partners) {
      var names = nameMap(partners);
      return Api.journal.list().then(function (list) { return (list || []).map(function (rm) { return mapRecord(rm, names); }); });
    });
  };
  DB.getRecord = function (id) {
    return Api.partners.list().then(function (partners) {
      var names = nameMap(partners);
      return Api.journal.get(id).then(function (rm) { return rm ? mapRecord(rm, names) : null; });
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
  DB.getAllPartners = function () { return Api.partners.list().then(function (l) { return (l || []).map(mapPartner); }); };
  DB.getPartner = function (id) { return Api.partners.get(id).then(function (rm) { return rm ? mapPartner(rm) : null; }); };
  DB.deletePartner = function (id) { return Api.partners.remove(id); };
  DB.putPartner = function (p) {
    if (p.id) {
      return Api.partners.rename(p.id, p.name)
        .then(function () { return Api.partners.profile(p.id, { photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" }); })
        .then(function () { return p; });
    }
    return Api.partners.register(p.name).then(function (m) {
      var id = m.id;
      return Api.partners.profile(id, { photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" })
        .then(function () { p.id = id; return p; });
    });
  };

  // ---- Каталог ----
  DB.getAllCatalog = function () { return Api.catalog.list().then(function (l) { return (l || []).map(mapCatalogItem); }); };
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
