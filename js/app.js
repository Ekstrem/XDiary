/* XDiary — логика приложения */
(function () {
  "use strict";

  function $(id) { return document.getElementById(id); }

  var CSV_HEADERS = ["id", "started_at", "duration_min", "partner", "place", "positions", "tags", "orgasms_me", "orgasms_partner"];

  // состояние
  var currentRecordId = null;
  var orgasms = { me: 0, partner: 0 };
  var positionsChips = null;
  var tagsChips = null;

  // ---- утилиты ----
  function pad(n) { return String(n).padStart(2, "0"); }
  function uuid() {
    if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
    return "id-" + Date.now() + "-" + Math.random().toString(36).slice(2);
  }
  function toInputValue(d) {
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) +
      "T" + pad(d.getHours()) + ":" + pad(d.getMinutes());
  }
  function fmtDateTime(iso) {
    if (!iso) return "";
    var d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return pad(d.getDate()) + "." + pad(d.getMonth() + 1) + "." + d.getFullYear() +
      " " + pad(d.getHours()) + ":" + pad(d.getMinutes());
  }
  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function toast(msg, ms) {
    var t = $("toast");
    t.textContent = msg;
    t.classList.remove("hidden");
    clearTimeout(t._timer);
    t._timer = setTimeout(function () { t.classList.add("hidden"); }, ms || 2200);
  }
  function showView(name) {
    ["lock", "journal", "form", "settings"].forEach(function (v) {
      $("view-" + v).classList.toggle("hidden", v !== name);
    });
  }

  // ---- блокировка ----
  function lock() {
    Pin.hasPin().then(function (hasPin) {
      if (!hasPin) { showView("journal"); return; }
      $("pin-input").value = "";
      $("pin-error").classList.add("hidden");
      showView("lock");
      setTimeout(function () { $("pin-input").focus(); }, 60);
    });
  }
  function unlock() {
    var pin = $("pin-input").value;
    Pin.verifyPin(pin).then(function (ok) {
      if (ok) {
        $("pin-error").classList.add("hidden");
        $("pin-input").value = "";
        showView("journal");
      } else {
        $("pin-error").classList.remove("hidden");
        $("pin-input").value = "";
        $("pin-input").focus();
      }
    });
  }

  // ---- чипы (позы / тэги) ----
  function makeChips(inputId, listId) {
    var input = $(inputId);
    var list = $(listId);
    var items = [];

    function render() {
      list.innerHTML = "";
      items.forEach(function (v, i) {
        var chip = document.createElement("span");
        chip.className = "chip";
        chip.textContent = v;
        var x = document.createElement("button");
        x.type = "button";
        x.className = "chip-x";
        x.textContent = "×";
        x.setAttribute("aria-label", "Удалить");
        x.addEventListener("click", function () { items.splice(i, 1); render(); });
        chip.appendChild(x);
        list.appendChild(chip);
      });
    }
    function add() {
      var v = input.value.trim();
      if (v && items.indexOf(v) === -1) { items.push(v); render(); }
      input.value = "";
      input.focus();
    }
    input.addEventListener("keydown", function (e) {
      if (e.key === "Enter" || e.key === "," || e.key === ";") { e.preventDefault(); add(); }
    });
    input.addEventListener("change", function () { if (input.value.trim()) add(); });

    return {
      get: function () { return items.slice(); },
      set: function (arr) { items = (arr || []).slice(); render(); }
    };
  }

  // ---- оргазмы ----
  function renderOrgasms() {
    $("f-orgasms-me-value").textContent = orgasms.me;
    $("f-orgasms-partner-value").textContent = orgasms.partner;
  }
  function bindStepper(minusId, plusId, key) {
    $(minusId).addEventListener("click", function () { orgasms[key] = Math.max(0, orgasms[key] - 1); renderOrgasms(); });
    $(plusId).addEventListener("click", function () { orgasms[key] = Math.min(99, orgasms[key] + 1); renderOrgasms(); });
  }

  // ---- журнал ----
  function renderJournal() {
    DB.getAllRecords().then(function (records) {
      records.sort(function (a, b) { return (b.startedAt || "").localeCompare(a.startedAt || ""); });
      var list = $("journal-list");
      list.innerHTML = "";
      $("journal-empty").classList.toggle("hidden", records.length > 0);

      records.forEach(function (r) {
        var card = document.createElement("button");
        card.type = "button";
        card.className = "record-card";

        var meta = [];
        if (r.partner) meta.push("👤 " + esc(r.partner));
        if (r.place) meta.push("📍 " + esc(r.place));
        meta.push("⏱ " + (r.durationMin || 0) + " мин");
        var org = (r.orgasmsMe || 0) + (r.orgasmsPartner || 0);

        var posHtml = "";
        if (r.positions && r.positions.length) {
          posHtml = '<div class="record-chips">' + r.positions.map(function (p) {
            return '<span class="chip-mini">' + esc(p) + "</span>";
          }).join("") + "</div>";
        }
        var tagsHtml = "";
        if (r.tags && r.tags.length) {
          tagsHtml = '<div class="record-chips record-chips-tags">' + r.tags.map(function (t) {
            return '<span class="chip-mini tag">#' + esc(t) + "</span>";
          }).join("") + "</div>";
        }

        card.innerHTML =
          '<div class="record-card-top">' +
            '<span class="record-date">' + esc(fmtDateTime(r.startedAt)) + "</span>" +
            '<span class="record-org">' + (org ? "💗 " + org : "") + "</span>" +
          "</div>" +
          '<div class="record-meta">' + meta.join(" · ") + "</div>" +
          posHtml + tagsHtml;

        card.addEventListener("click", function () { openForm(r.id); });
        list.appendChild(card);
      });

      renderSuggestions(records);
    });
  }

  function renderSuggestions(records) {
    var partners = new Set(), places = new Set(), positions = new Set(), tags = new Set();
    records.forEach(function (r) {
      if (r.partner) partners.add(r.partner);
      if (r.place) places.add(r.place);
      (r.positions || []).forEach(function (p) { positions.add(p); });
      (r.tags || []).forEach(function (t) { tags.add(t); });
    });
    fillDatalist("partner-suggest", partners);
    fillDatalist("place-suggest", places);
    fillDatalist("positions-suggest", positions);
    fillDatalist("tags-suggest", tags);
  }
  function fillDatalist(id, set) {
    var dl = $(id);
    dl.innerHTML = "";
    Array.from(set).sort(function (a, b) { return a.localeCompare(b, "ru"); }).forEach(function (v) {
      var o = document.createElement("option");
      o.value = v;
      dl.appendChild(o);
    });
  }

  // ---- форма ----
  function openForm(id) {
    currentRecordId = id || null;
    $("btn-delete").classList.toggle("hidden", !id);
    $("form-title").textContent = id ? "Запись" : "Новая запись";
    $("f-startedAt").value = toInputValue(new Date());
    $("f-duration").value = "";
    $("f-partner").value = "";
    $("f-place").value = "";
    positionsChips.set([]);
    tagsChips.set([]);
    orgasms = { me: 0, partner: 0 };
    renderOrgasms();

    if (id) {
      DB.getRecord(id).then(function (r) {
        if (!r) return;
        $("f-startedAt").value = r.startedAt || toInputValue(new Date());
        $("f-duration").value = r.durationMin || "";
        $("f-partner").value = r.partner || "";
        $("f-place").value = r.place || "";
        positionsChips.set(r.positions || []);
        tagsChips.set(r.tags || []);
        orgasms.me = r.orgasmsMe || 0;
        orgasms.partner = r.orgasmsPartner || 0;
        renderOrgasms();
      });
    }
    showView("form");
  }

  function collectForm() {
    var startedAt = $("f-startedAt").value;
    if (!startedAt) { toast("Укажите дату и время"); return null; }
    var durationMin = parseInt($("f-duration").value, 10);
    if (!durationMin || durationMin < 1) { toast("Укажите длительность (мин)"); return null; }
    return {
      startedAt: startedAt,
      durationMin: durationMin,
      partner: $("f-partner").value.trim(),
      place: $("f-place").value.trim(),
      positions: positionsChips.get(),
      tags: tagsChips.get(),
      orgasmsMe: orgasms.me,
      orgasmsPartner: orgasms.partner
    };
  }

  function saveRecord() {
    var data = collectForm();
    if (!data) return;
    var nowIso = new Date().toISOString();
    var promise;
    if (currentRecordId) {
      promise = DB.getRecord(currentRecordId).then(function (existing) {
        var rec = existing || {};
        Object.keys(data).forEach(function (k) { rec[k] = data[k]; });
        rec.id = currentRecordId;
        rec.updatedAt = nowIso;
        return DB.putRecord(rec);
      });
    } else {
      var rec = Object.assign({ id: uuid(), createdAt: nowIso, updatedAt: nowIso }, data);
      promise = DB.putRecord(rec);
    }
    promise.then(function () {
      toast("Сохранено");
      showView("journal");
      renderJournal();
    }).catch(function (e) { toast("Ошибка: " + e.message); });
  }

  function deleteCurrent() {
    if (!currentRecordId) return;
    if (!window.confirm("Удалить эту запись?")) return;
    DB.deleteRecord(currentRecordId).then(function () {
      toast("Удалено");
      showView("journal");
      renderJournal();
    });
  }

  // ---- CSV ----
  function recordsToCsv(records) {
    var rows = [CSV_HEADERS];
    records.forEach(function (r) {
      rows.push([
        r.id || "", r.startedAt || "", r.durationMin || 0, r.partner || "", r.place || "",
        (r.positions || []).join(";"), (r.tags || []).join(";"),
        r.orgasmsMe || 0, r.orgasmsPartner || 0
      ]);
    });
    return CSV.stringify(rows);
  }

  function csvToRecords(text) {
    text = String(text || "").replace(/^\uFEFF/, "");
    var rows = CSV.parse(text);
    if (!rows.length) return [];
    var header = rows[0].map(function (h) { return String(h).trim().toLowerCase(); });
    var idx = {};
    header.forEach(function (h, i) { idx[h] = i; });
    function get(row) {
      for (var a = 1; a < arguments.length; a++) {
        var i = idx[arguments[a]];
        if (i !== undefined && row[i] !== undefined) return row[i];
      }
      return "";
    }
    function splitList(s) {
      if (!s) return [];
      return String(s).split(";").map(function (x) { return x.trim(); }).filter(Boolean);
    }
    var out = [];
    for (var r = 1; r < rows.length; r++) {
      var row = rows[r];
      var id = String(get(row, "id") || "").trim();
      out.push({
        id: id || uuid(),
        startedAt: String(get(row, "started_at", "startedat") || ""),
        durationMin: parseInt(get(row, "duration_min", "durationmin") || "0", 10) || 0,
        partner: String(get(row, "partner") || ""),
        place: String(get(row, "place") || ""),
        positions: splitList(get(row, "positions")),
        tags: splitList(get(row, "tags")),
        orgasmsMe: parseInt(get(row, "orgasms_me", "orgasmsme") || "0", 10) || 0,
        orgasmsPartner: parseInt(get(row, "orgasms_partner", "orgasmspartner") || "0", 10) || 0,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      });
    }
    return out;
  }

  function downloadCsv() {
    DB.getAllRecords().then(function (records) {
      var csv = "\uFEFF" + recordsToCsv(records);
      var blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
      var url = URL.createObjectURL(blob);
      var a = document.createElement("a");
      a.href = url;
      a.download = "xdiary-" + new Date().toISOString().slice(0, 10) + ".csv";
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
      toast("Экспортировано в CSV");
    });
  }

  function importCsvFile(file) {
    file.text().then(function (text) {
      var records = csvToRecords(text);
      if (!records.length) { toast("CSV пуст или не распознан"); return; }
      var chain = Promise.resolve();
      records.forEach(function (rec) { chain = chain.then(function () { return DB.putRecord(rec); }); });
      chain.then(function () {
        toast("Импортировано записей: " + records.length);
        renderJournal();
      });
    }).catch(function (e) { toast("Ошибка импорта: " + e.message); });
  }

  // ---- PIN (настройки) ----
  function refreshPinStatus() {
    Pin.hasPin().then(function (has) {
      $("pin-status").textContent = has ? "PIN-код установлен" : "PIN-код не установлен";
      $("pin-current-wrap").classList.toggle("hidden", !has);
      $("btn-pin-remove").classList.toggle("hidden", !has);
      $("btn-pin-set").textContent = has ? "Изменить PIN" : "Установить PIN";
      $("pin-current").value = "";
      $("pin-new").value = "";
      $("pin-confirm").value = "";
    });
  }
  function applyPin() {
    Pin.hasPin().then(function (has) {
      var cur = $("pin-current").value;
      var nw = $("pin-new").value;
      var cf = $("pin-confirm").value;
      var proceed = Promise.resolve(true);
      if (has) {
        proceed = Pin.verifyPin(cur).then(function (ok) {
          if (!ok) { toast("Текущий PIN неверен"); return false; }
          return true;
        });
      }
      proceed.then(function (ok) {
        if (!ok) return;
        if (!Pin.isValidPin(nw)) { toast("Новый PIN: 4–8 цифр"); return; }
        if (nw !== cf) { toast("PIN не совпадают"); return; }
        Pin.setPin(nw).then(function () { toast("PIN сохранён"); refreshPinStatus(); });
      });
    });
  }
  function removePinNow() {
    if (!window.confirm("Удалить PIN-код?")) return;
    Pin.removePin().then(function () { toast("PIN удалён"); refreshPinStatus(); });
  }

  // ---- полная очистка ----
  function wipeAll() {
    var word = $("wipe-input").value.trim();
    if (word !== "УДАЛИТЬ") { toast('Введите «УДАЛИТЬ» для подтверждения'); return; }
    Promise.all([DB.clearRecords(), Pin.removePin()]).then(function () {
      $("wipe-input").value = "";
      toast("Все данные удалены");
      renderJournal();
      refreshPinStatus();
    });
  }

  // ---- инициализация ----
  function init() {
    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.register("./sw.js").catch(function () {});
    }

    positionsChips = makeChips("positions-input", "positions-list");
    tagsChips = makeChips("tags-input", "tags-list");
    bindStepper("f-orgasms-me-minus", "f-orgasms-me-plus", "me");
    bindStepper("f-orgasms-partner-minus", "f-orgasms-partner-plus", "partner");

    $("pin-submit").addEventListener("click", unlock);
    $("pin-input").addEventListener("keydown", function (e) { if (e.key === "Enter") unlock(); });

    $("btn-new").addEventListener("click", function () { openForm(null); });
    $("btn-lock").addEventListener("click", lock);
    $("btn-settings").addEventListener("click", function () { refreshPinStatus(); showView("settings"); });

    $("btn-form-back").addEventListener("click", function () { showView("journal"); });
    $("btn-form-save").addEventListener("click", saveRecord);
    $("btn-delete").addEventListener("click", deleteCurrent);
    $("record-form").addEventListener("submit", function (e) { e.preventDefault(); saveRecord(); });

    $("btn-settings-back").addEventListener("click", function () { showView("journal"); });
    $("btn-pin-set").addEventListener("click", applyPin);
    $("btn-pin-remove").addEventListener("click", removePinNow);
    $("btn-export").addEventListener("click", downloadCsv);
    $("btn-import").addEventListener("click", function () { $("import-file").click(); });
    $("import-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) importCsvFile(f);
      e.target.value = "";
    });
    $("btn-wipe").addEventListener("click", wipeAll);

    // автоблокировка после долгого скрытия приложения
    var hiddenAt = 0;
    document.addEventListener("visibilitychange", function () {
      if (document.hidden) hiddenAt = Date.now();
      else if (hiddenAt && Date.now() - hiddenAt > 2 * 60 * 1000) lock();
    });

    renderJournal();
    lock();
  }

  document.addEventListener("DOMContentLoaded", init);
})();
