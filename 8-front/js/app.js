/* XDiary вЂ” Р»РѕРіРёРєР° РїСЂРёР»РѕР¶РµРЅРёСЏ (РІРёР·Р°СЂРґ-Р·Р°РїРёСЃСЊ, Р¶СѓСЂРЅР°Р», РїР°СЂС‚РЅС‘СЂС‹, РєР°С‚Р°Р»РѕРі, РЅР°СЃС‚СЂРѕР№РєРё) */
(function () {
  "use strict";

  function $(id) { return document.getElementById(id); }

  var CSV_HEADERS = ["id", "started_at", "duration_min", "partner", "place", "positions", "tags", "costumes", "toys", "fantasies", "contraceptives", "contraceptive_count", "orgasms_me", "orgasms_partner", "orgasm_me_type", "orgasm_me_rating", "orgasm_partner_type", "orgasm_partner_rating", "garmin"];
  var STEPS = ["Р”Р°С‚Р°", "РџР°СЂС‚РЅС‘СЂ", "РњРµСЃС‚Рѕ", "РџРѕР·С‹", "РђРєСЃРµСЃСЃСѓР°СЂС‹", "РљРѕРЅС‚СЂР°С†РµРїС†РёСЏ", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ", "РћСЂРіР°Р·РјС‹", "РС‚РѕРі"];
  var POSITIONS = [
    "РњРёСЃСЃРёРѕРЅРµСЂСЃРєР°СЏ", "Р”РѕРіРіРё (СЃР·Р°РґРё)", "РќР°РµР·РґРЅРёС†Р° (СЃРІРµСЂС…Сѓ)", "РћР±СЂР°С‚РЅР°СЏ РЅР°РµР·РґРЅРёС†Р°",
    "Р›РѕР¶РєРё", "69", "РЎС‚РѕСЏ", "РЎРёРґСЏ (РЅР° РєРѕР»РµРЅСЏС…)", "РћСЂР°Р»СЊРЅС‹Р№", "РљРѕР»РµРЅРЅРѕ-Р»РѕРєС‚РµРІР°СЏ",
    "РќРѕР¶РЅРёС†С‹", "РњРёСЃСЃРёРѕРЅРµСЂСЃРєР°СЏ (РЅРѕРіРё РІРІРµСЂС…)"
  ];
  var DURATION_PRESETS = [10, 15, 20, 30, 45, 60, 90];

  var KIND_ORDER = ["costume", "toy", "fantasy", "contraception"];
  var KIND_META = {
    costume: { title: "РљРѕСЃС‚СЋРјС‡РёРєРё", single: "РљРѕСЃС‚СЋРј", icon: "рџ§Ґ" },
    toy: { title: "РРіСЂСѓС€РєРё", single: "РРіСЂСѓС€РєР°", icon: "рџ§ё" },
    fantasy: { title: "Р¤Р°РЅС‚Р°Р·РёРё", single: "Р¤Р°РЅС‚Р°Р·РёСЏ", icon: "вњЁ" },
    contraception: { title: "РљРѕРЅС‚СЂР°С†РµРїС†РёСЏ", single: "РЎСЂРµРґСЃС‚РІРѕ", icon: "рџЌҐ" }
  };
  var KIND_FIELD = { costume: "costumes", toy: "toys", fantasy: "fantasies", contraception: "contraceptives" };

  // СЃРѕСЃС‚РѕСЏРЅРёРµ
  var draft = null;
  var wizardStep = 0;
  var editingId = null;
  var editingPartnerId = null;
  var partnerEditReturn = "partners";
  var partnerEditPhotoData = "";
  var partnerEditGender = "female";
  var myGender = "male";
  var catalogKind = "costume";
  var editingCatalogId = null;
  var catalogEditKind = "costume";
  var catalogEditBrand = "";
  var catalogEditArchived = false;
  var catalogEditPartner = "";
  var catalogEditReturn = "catalog";
  var catalogEditPhotoData = "";

  var detailRecordId = null;
  var detailMetric = "hr";
  var detailWindow = 30;
  var METRIC_ORDER = ["hr", "stress", "body_battery"];
  var METRIC_META = {
    hr: { label: "РџСѓР»СЊСЃ", color: "#e0567a", unit: "СѓРґ/РјРёРЅ" },
    stress: { label: "РЎС‚СЂРµСЃСЃ", color: "#f0a04b", unit: "" },
    body_battery: { label: "Body Battery", color: "#4ade80", unit: "" },
    hrv: { label: "HRV", color: "#60a5fa", unit: "РјСЃ" }
  };

  var trendMetric = "stress";
  var trendTab = "trends";
  var TREND_META = {
    stress: { label: "РЎС‚СЂРµСЃСЃ", color: "#f0a04b", field: "stress" },
    resting_hr: { label: "РџСѓР»СЊСЃ РїРѕРєРѕСЏ", color: "#e0567a", field: "resting_hr" },
    sleep: { label: "РЎРѕРЅ (РѕС†РµРЅРєР°)", color: "#4ade80", field: "score" },
    hrv: { label: "HRV", color: "#60a5fa", field: "hrv" }
  };
  var RU_MONTHS = { "СЏРЅРІ": 1, "С„РµРІ": 2, "РјР°СЂ": 3, "Р°РїСЂ": 4, "РјР°Р№": 5, "РёСЋРЅ": 6, "РёСЋР»": 7, "Р°РІРі": 8, "СЃРµРЅ": 9, "РѕРєС‚": 10, "РЅРѕСЏ": 11, "РґРµРє": 12 };
  var pendingImport = null;

  var APP_VERSION = "0.27"; // СЃРёРЅС…СЂРѕРЅРёР·РёСЂРѕРІР°С‚СЊ СЃ CACHE РІ sw.js

  // ---- СѓС‚РёР»РёС‚С‹ ----
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
  function minuteKeyFromDate(d) {
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) +
      "T" + pad(d.getHours()) + ":" + pad(d.getMinutes());
  }
  function toMinuteKey(iso) {
    var d = new Date(iso);
    if (isNaN(d.getTime())) return null;
    return minuteKeyFromDate(d);
  }
  function toDateKey(iso) {
    var s = String(iso || "");
    if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s;
    var d = new Date(s);
    if (isNaN(d.getTime())) return null;
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate());
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

  function confirmDialog(message) {
    return new Promise(function (resolve) {
      var modal = $("confirm-modal");
      $("confirm-text").textContent = message;
      modal.classList.remove("hidden");
      function close(v) {
        modal.classList.add("hidden");
        $("confirm-yes").removeEventListener("click", yes);
        $("confirm-no").removeEventListener("click", no);
        resolve(v);
      }
      function yes() { close(true); }
      function no() { close(false); }
      $("confirm-yes").addEventListener("click", yes);
      $("confirm-no").addEventListener("click", no);
    });
  }

  var TAB_VIEWS = ["journal", "partners", "catalog", "trends", "settings"];
  function showView(name) {
    ["lock", "journal", "partners", "catalog", "trends", "settings", "wizard", "partner-edit", "catalog-edit", "import", "detail"].forEach(function (v) {
      $("view-" + v).classList.toggle("hidden", v !== name);
    });
    var hasTab = TAB_VIEWS.indexOf(name) !== -1;
    $("tabbar").classList.toggle("hidden", !hasTab);
    TAB_VIEWS.forEach(function (t) {
      $("tab-" + t).classList.toggle("active", t === name && hasTab);
    });
  }

  function avatarHtml(o) {
    if (o.photo) return '<img class="avatar" src="' + o.photo + '" alt="">';
    var initial = (o.name || "?").trim().charAt(0).toUpperCase() || "?";
    return '<span class="avatar avatar-initial">' + esc(initial) + "</span>";
  }

  function fileToResizedDataUrl(file, maxSize) {
    return new Promise(function (resolve, reject) {
      var reader = new FileReader();
      reader.onload = function () {
        var img = new Image();
        img.onload = function () {
          var scale = Math.min(1, maxSize / Math.max(img.width, img.height));
          var w = Math.max(1, Math.round(img.width * scale));
          var h = Math.max(1, Math.round(img.height * scale));
          var canvas = document.createElement("canvas");
          canvas.width = w; canvas.height = h;
          canvas.getContext("2d").drawImage(img, 0, 0, w, h);
          resolve(canvas.toDataURL("image/jpeg", 0.85));
        };
        img.onerror = reject;
        img.src = reader.result;
      };
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
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

  // ---- Р±Р»РѕРєРёСЂРѕРІРєР° ----
  function ensureToken(pin) {
    return Api.auth.login(pin).catch(function () {
      return Api.privacy.setPin(pin).then(function () { return Api.auth.login(pin); });
    });
  }
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
        ensureToken(pin).then(function () { showView("journal"); }, function () { showView("journal"); });
      } else {
        $("pin-error").classList.remove("hidden");
        $("pin-input").value = "";
        $("pin-input").focus();
      }
    });
  }

  // ---- Р¶СѓСЂРЅР°Р» ----
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
        if (r.partner) meta.push("рџ‘¤ " + esc(r.partner));
        if (r.place) meta.push("рџ“Ќ " + esc(r.place));
        meta.push("вЏ± " + (r.durationMin || 0) + " РјРёРЅ");
        var org = (r.orgasmsMe || 0) + (r.orgasmsPartner || 0);

        var posHtml = "";
        if (r.positions && r.positions.length) {
          posHtml = '<div class="record-chips">' + r.positions.map(function (p) {
            return '<span class="chip-mini">' + esc(p) + "</span>";
          }).join("") + "</div>";
        }
        var marks = [];
        (r.costumes || []).forEach(function (x) { marks.push('<span class="chip-mini">рџ§Ґ ' + esc(x) + "</span>"); });
        (r.toys || []).forEach(function (x) { marks.push('<span class="chip-mini">рџ§ё ' + esc(x) + "</span>"); });
        (r.fantasies || []).forEach(function (x) { marks.push('<span class="chip-mini">вњЁ ' + esc(x) + "</span>"); });
        (r.tags || []).forEach(function (x) { marks.push('<span class="chip-mini tag">#' + esc(x) + "</span>"); });
        var marksHtml = marks.length ? '<div class="record-chips">' + marks.join("") + "</div>" : "";

        card.innerHTML =
          '<div class="record-card-top">' +
            '<span class="record-date">' +
              (r.garmin ? '<span class="garmin-badge">вЊљ</span>' : "") +
              esc(fmtDateTime(r.startedAt)) +
            "</span>" +
            '<span class="record-org">' + (org ? "рџ’— " + org : "") + "</span>" +
          "</div>" +
          '<div class="record-meta">' + meta.join(" В· ") + "</div>" +
          posHtml + marksHtml;

        card.addEventListener("click", function () { openDetail(r.id); });
        list.appendChild(card);
      });

      var places = new Set();
      records.forEach(function (r) { if (r.place) places.add(r.place); });
      fillDatalist("place-suggest", places);
    });
  }

  function lastPartnerName(records) {
    var sorted = records.slice().sort(function (a, b) { return (b.startedAt || "").localeCompare(a.startedAt || ""); });
    for (var i = 0; i < sorted.length; i++) {
      if (sorted[i].partner) return sorted[i].partner;
    }
    return "";
  }

  // ---- РІРёР·Р°СЂРґ ----
  function newDraft() {
    return {
      startedAt: toInputValue(new Date()), partner: "", place: "",
      positions: [], tags: [], costumes: [], toys: [], fantasies: [],
      durationMin: 0, orgasmsMe: 0, orgasmsPartner: 0,
      orgasmMeType: [], orgasmPartnerType: [], orgasmMeRating: 0, orgasmPartnerRating: 0,
      contraceptives: [], contraceptiveCount: 0,
      partnerTouched: false, garmin: false, garminId: ""
    };
  }

  function openWizard(id) {
    editingId = id || null;
    wizardStep = id ? STEPS.length - 1 : 0;
    if (id) {
      DB.getRecord(id).then(function (r) {
        if (!r) return;
        draft = {
          startedAt: r.startedAt || toInputValue(new Date()),
          partner: r.partner || "", place: r.place || "",
          positions: (r.positions || []).slice(), tags: (r.tags || []).slice(),
          costumes: (r.costumes || []).slice(), toys: (r.toys || []).slice(),
          fantasies: (r.fantasies || []).slice(),
          durationMin: r.durationMin || 0, orgasmsMe: r.orgasmsMe || 0,
          orgasmsPartner: r.orgasmsPartner || 0,
          orgasmMeType: (Array.isArray(r.orgasmMeType) ? r.orgasmMeType.slice() : (r.orgasmMeType ? [String(r.orgasmMeType)] : [])),
          orgasmPartnerType: (Array.isArray(r.orgasmPartnerType) ? r.orgasmPartnerType.slice() : (r.orgasmPartnerType ? [String(r.orgasmPartnerType)] : [])),
          orgasmMeRating: r.orgasmMeRating || 0, orgasmPartnerRating: r.orgasmPartnerRating || 0,
          contraceptives: (r.contraceptives || []).slice(), contraceptiveCount: r.contraceptiveCount || 0,
          partnerTouched: true, garmin: !!r.garmin, garminId: r.garminId || ""
        };
        showView("wizard");
        renderWizard();
      });
    } else {
      DB.getAllRecords().then(function (records) {
        draft = newDraft();
        draft.partner = lastPartnerName(records);
        draft.place = historyPlaces(records)[0] || "";
        showView("wizard");
        renderWizard();
      });
    }
  }

  function renderWizard() {
    $("wizard-title").textContent = editingId ? "Р—Р°РїРёСЃСЊ" : "РќРѕРІР°СЏ Р·Р°РїРёСЃСЊ";

    var prog = $("wizard-progress");
    prog.innerHTML = "";
    STEPS.forEach(function (s, i) {
      var d = document.createElement("button");
      d.type = "button";
      d.className = "progress-dot" + (i === wizardStep ? " active" : "") + (i < wizardStep ? " done" : "");
      d.title = s;
      (function (idx) {
        d.addEventListener("click", function () { wizardStep = idx; renderWizard(); });
      })(i);
      prog.appendChild(d);
    });

    var body = $("wizard-body");
    body.innerHTML = "";
    renderStep(body);

    var isLast = wizardStep === STEPS.length - 1;
    $("btn-wizard-prev").classList.toggle("hidden", wizardStep === 0);
    $("btn-wizard-next").classList.toggle("hidden", isLast);
    $("btn-wizard-save").classList.toggle("hidden", !isLast);
    $("btn-wizard-delete").classList.toggle("hidden", !(isLast && editingId));
  }

  function renderStep(body) {
    switch (wizardStep) {
      case 0: stepDate(body); break;
      case 1: stepPartner(body); break;
      case 2: stepPlace(body); break;
      case 3: stepPositions(body); break;
      case 4: stepMarks(body); break;
      case 5: stepContraception(body); break;
      case 6: stepDuration(body); break;
      case 7: stepOrgasms(body); break;
      case 8: stepSummary(body); break;
    }
  }

  function stepDate(body) {
    body.innerHTML =
      '<div class="step-label">РљРѕРіРґР° СЌС‚Рѕ Р±С‹Р»Рѕ?</div>' +
      '<input type="datetime-local" id="w-startedAt" value="' + esc(draft.startedAt) + '">';
    $("w-startedAt").addEventListener("input", function (e) { draft.startedAt = e.target.value; });
  }

  function stepPartner(body) {
    DB.getAllPartners().then(function (partners) {
      DB.getAllRecords().then(function (records) {
        body.innerHTML =
          '<div class="step-label">РЎ РєРµРј?</div>' +
          '<div class="partner-grid" id="w-partner-grid"></div>' +
          '<div class="field">' +
            '<input type="text" id="w-partner-custom" list="partner-suggest" placeholder="РР»Рё РІРІРµРґРёС‚Рµ РёРјСЏ" autocomplete="off">' +
          "</div>" +
          '<button type="button" class="btn" id="w-partner-none">Р‘РµР· РїР°СЂС‚РЅС‘СЂР°</button>';

        var grid = $("w-partner-grid");
        partners.forEach(function (p) {
          var c = document.createElement("button");
          c.type = "button";
          c.className = "partner-card" + (p.name === draft.partner ? " selected" : "");
          c.innerHTML = avatarHtml(p) + "<span>" + esc(p.name) + "</span>";
          c.addEventListener("click", function () {
            draft.partner = p.name;
            draft.partnerTouched = true;
            stepPartner(body);
          });
          grid.appendChild(c);
        });
        var add = document.createElement("button");
        add.type = "button";
        add.className = "partner-card partner-card-add";
        add.innerHTML = '<span class="avatar avatar-initial">пј‹</span><span>Р”РѕР±Р°РІРёС‚СЊ</span>';
        add.addEventListener("click", function () { partnerEditReturn = "wizard"; openPartnerEdit(null); });
        grid.appendChild(add);

        var inCatalog = partners.some(function (p) { return p.name === draft.partner; });
        var custom = $("w-partner-custom");
        custom.value = (draft.partner && !inCatalog) ? draft.partner : "";
        custom.addEventListener("input", function (e) {
          draft.partner = e.target.value.trim();
          draft.partnerTouched = true;
        });
        $("w-partner-none").addEventListener("click", function () {
          draft.partner = "";
          draft.partnerTouched = true;
          stepPartner(body);
        });

        var names = new Set();
        partners.forEach(function (p) { names.add(p.name); });
        records.forEach(function (r) { if (r.partner) names.add(r.partner); });
        fillDatalist("partner-suggest", names);
      });
    });
  }

  function historyPlaces(records) {
    var out = [];
    var sorted = records.slice().sort(function (a, b) { return (b.startedAt || "").localeCompare(a.startedAt || ""); });
    sorted.forEach(function (r) { if (r.place && out.indexOf(r.place) === -1) out.push(r.place); });
    return out;
  }

  function stepPlace(body) {
    DB.getAllRecords().then(function (records) {
      var places = historyPlaces(records);
      body.innerHTML =
        '<div class="step-label">Р“РґРµ?</div>' +
        '<div class="chips-grid" id="w-place-grid"></div>' +
        '<div class="field" style="margin-top:16px">' +
          '<input type="text" id="w-place" list="place-suggest" placeholder="РЎРІРѕС‘ РјРµСЃС‚Рѕ" autocomplete="off">' +
        "</div>";

      var grid = $("w-place-grid");
      places.forEach(function (p) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chip-toggle" + (draft.place === p ? " on" : "");
        b.textContent = p;
        b.addEventListener("click", function () {
          draft.place = (draft.place === p) ? "" : p;
          stepPlace(body);
        });
        grid.appendChild(b);
      });

      var input = $("w-place");
      input.value = draft.place;
      input.addEventListener("input", function (e) { draft.place = e.target.value.trim(); });
      input.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
          e.preventDefault();
          var v = input.value.trim();
          if (v) { draft.place = v; stepPlace(body); }
        }
      });
    });
  }

  function historyPositions(records) {
    var out = [];
    var sorted = records.slice().sort(function (a, b) { return (b.startedAt || "").localeCompare(a.startedAt || ""); });
    sorted.forEach(function (r) {
      (r.positions || []).forEach(function (p) { if (out.indexOf(p) === -1) out.push(p); });
    });
    return out.filter(function (p) { return POSITIONS.indexOf(p) === -1; });
  }

  function stepPositions(body) {
    DB.getAllRecords().then(function (records) {
      var all = POSITIONS.concat(historyPositions(records));
      body.innerHTML =
        '<div class="step-label">РџРѕР·С‹</div>' +
        '<div class="chips-grid" id="w-pos-grid"></div>' +
        '<div class="field" style="margin-top:16px">' +
          '<input type="text" id="w-pos-custom" placeholder="РЎРІРѕСЏ РїРѕР·Р°" autocomplete="off">' +
        "</div>";

      var grid = $("w-pos-grid");
      all.forEach(function (p) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chip-toggle" + (draft.positions.indexOf(p) !== -1 ? " on" : "");
        b.textContent = p;
        b.addEventListener("click", function () {
          var i = draft.positions.indexOf(p);
          if (i !== -1) draft.positions.splice(i, 1); else draft.positions.push(p);
          stepPositions(body);
        });
        grid.appendChild(b);
      });

      var custom = $("w-pos-custom");
      custom.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
          e.preventDefault();
          var v = custom.value.trim();
          if (v && draft.positions.indexOf(v) === -1) draft.positions.push(v);
          custom.value = "";
          stepPositions(body);
        }
      });
    });
  }

  // ---- Р°РєСЃРµСЃСЃСѓР°СЂС‹ (РєРѕСЃС‚СЋРјС‹ / РёРіСЂСѓС€РєРё / С„Р°РЅС‚Р°Р·РёРё / С‚РµРіРё) ----
  function stepMarks(body) {
    DB.getAllCatalog().then(function (items) {
      body.innerHTML = '<div class="step-label">РђРєСЃРµСЃСЃСѓР°СЂС‹</div>';

      var kinds = [
        { key: "costume", label: "РљРѕСЃС‚СЋРјС‡РёРєРё" },
        { key: "toy", label: "РРіСЂСѓС€РєРё" },
        { key: "fantasy", label: "Р РµР°Р»РёР·РѕРІР°РЅРЅС‹Рµ С„Р°РЅС‚Р°Р·РёРё" }
      ];
      kinds.forEach(function (k) {
        var field = KIND_FIELD[k.key];
        var grp = document.createElement("div");
        grp.className = "marks-group";
        grp.innerHTML =
          '<div class="marks-group-title">' + k.label +
            ' <button type="button" class="chip-add" data-kind="' + k.key + '">пј‹</button></div>' +
          '<div class="chips-grid"></div>';
        body.appendChild(grp);

        var grid = grp.querySelector(".chips-grid");
        var list = items.filter(function (it) { return it.kind === k.key; });
        list.forEach(function (it) {
          var b = document.createElement("button");
          b.type = "button";
          b.className = "chip-toggle" + (draft[field].indexOf(it.name) !== -1 ? " on" : "");
          b.textContent = it.name;
          b.addEventListener("click", function () {
            var arr = draft[field];
            var i = arr.indexOf(it.name);
            if (i !== -1) arr.splice(i, 1); else arr.push(it.name);
            stepMarks(body);
          });
          grid.appendChild(b);
        });
        if (!list.length) {
          var hint = document.createElement("span");
          hint.className = "muted";
          hint.textContent = "РџРѕРєР° РїСѓСЃС‚Рѕ";
          grid.appendChild(hint);
        }
        grp.querySelector(".chip-add").addEventListener("click", function () {
          catalogEditReturn = "wizard";
          openCatalogEdit(null, k.key);
        });
      });

      // С‚РµРіРё (СЃРІРѕР±РѕРґРЅС‹Рµ)
      var tags = document.createElement("div");
      tags.className = "marks-group";
      tags.innerHTML =
        '<div class="marks-group-title">РўРµРіРё</div>' +
        '<div class="chips-grid" id="w-tag-grid"></div>' +
        '<input type="text" id="w-tag-custom" placeholder="РЎРІРѕР№ С‚РµРі" autocomplete="off">';
      body.appendChild(tags);

      DB.getAllRecords().then(function (records) {
        var set = [];
        records.forEach(function (r) {
          (r.tags || []).forEach(function (t) { if (set.indexOf(t) === -1) set.push(t); });
        });
        var grid = $("w-tag-grid");
        set.forEach(function (t) {
          var b = document.createElement("button");
          b.type = "button";
          b.className = "chip-toggle" + (draft.tags.indexOf(t) !== -1 ? " on" : "");
          b.textContent = "#" + t;
          b.addEventListener("click", function () {
            var i = draft.tags.indexOf(t);
            if (i !== -1) draft.tags.splice(i, 1); else draft.tags.push(t);
            stepMarks(body);
          });
          grid.appendChild(b);
        });
        var custom = $("w-tag-custom");
        custom.addEventListener("keydown", function (e) {
          if (e.key === "Enter") {
            e.preventDefault();
            var v = custom.value.trim();
            if (v && draft.tags.indexOf(v) === -1) draft.tags.push(v);
            custom.value = "";
            stepMarks(body);
          }
        });
      });
    });
  }

  function stepContraception(body) {
    Promise.all([DB.getAllCatalog(), DB.getAllRecords()]).then(function (res) {
      var items = res[0], records = res[1];
      var used = [];
      records.slice().sort(function (a, b) { return (b.startedAt || "").localeCompare(a.startedAt || ""); }).forEach(function (r) {
        (r.contraceptives || []).forEach(function (c) { if (used.indexOf(c) === -1) used.push(c); });
      });
      var contra = items.filter(function (it) { return it.kind === "contraception" && !it.archived; });
      contra.sort(function (a, b) {
        var ia = used.indexOf(a.name), ib = used.indexOf(b.name);
        if (ia === -1 && ib === -1) return 0;
        if (ia === -1) return 1;
        if (ib === -1) return -1;
        return ia - ib;
      });
      var chips = contra.map(function (it) {
        return '<button type="button" class="chip-toggle contraception-tally" data-cname="' + esc(it.name) + '">' + esc(it.name) + "</button>";
      }).join("");
      body.innerHTML =
        '<div class="step-label">РљРѕРЅС‚СЂР°С†РµРїС†РёСЏ</div>' +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head"><span>РСЃРїРѕР»СЊР·РѕРІР°РЅРѕ</span>' +
            '<span class="orgasm-count" data-cnt="contra">' + draft.contraceptiveCount + "</span>" +
            '<button type="button" class="orgasm-reset" data-rcnt="contra" title="РЎР±СЂРѕСЃРёС‚СЊ">в†є</button>' +
          "</div>" +
          (chips ? '<div class="chips-grid orgasm-types">' + chips + "</div>" : '<p class="muted">РќРµС‚ СЃСЂРµРґСЃС‚РІ вЂ” РґРѕР±Р°РІСЊС‚Рµ РІ РљР°С‚Р°Р»РѕРіРµ (РІРєР»Р°РґРєР° В«РљРѕРЅС‚СЂР°С†РµРїС†РёСЏВ»).</p>') +
        "</div>";

      body.querySelectorAll(".contraception-tally").forEach(function (b) {
        b.addEventListener("click", function () {
          var nm = b.getAttribute("data-cname");
          b.classList.add("firing");
          setTimeout(function () {
            b.classList.remove("firing");
            draft.contraceptiveCount = (draft.contraceptiveCount || 0) + 1;
            var arr = draft.contraceptives || [];
            if (arr.indexOf(nm) === -1) arr.push(nm);
            draft.contraceptives = arr;
            var el = body.querySelector('[data-cnt="contra"]');
            if (el) el.textContent = draft.contraceptiveCount;
          }, 600);
        });
      });
      body.querySelectorAll(".orgasm-reset").forEach(function (b) {
        b.addEventListener("click", function () {
          draft.contraceptiveCount = 0;
          draft.contraceptives = [];
          var el = body.querySelector('[data-cnt="contra"]');
          if (el) el.textContent = "0";
        });
      });
    });
  }

  function stepDuration(body) {
    body.innerHTML =
      '<div class="step-label">РЎРєРѕР»СЊРєРѕ РґР»РёР»РѕСЃСЊ?</div>' +
      '<div class="chips-grid" id="w-dur-grid"></div>' +
      '<div class="field" style="margin-top:16px">' +
        '<input type="number" id="w-dur-custom" min="1" inputmode="numeric" placeholder="РЎРІРѕС‘ Р·РЅР°С‡РµРЅРёРµ, РјРёРЅ">' +
      "</div>";

    var grid = $("w-dur-grid");
    DURATION_PRESETS.forEach(function (d) {
      var b = document.createElement("button");
      b.type = "button";
      b.className = "chip-toggle" + (draft.durationMin === d ? " on" : "");
      b.textContent = d + " РјРёРЅ";
      b.addEventListener("click", function () { draft.durationMin = d; stepDuration(body); });
      grid.appendChild(b);
    });

    var custom = $("w-dur-custom");
    var presetSelected = DURATION_PRESETS.indexOf(draft.durationMin) !== -1;
    custom.value = (draft.durationMin && !presetSelected) ? draft.durationMin : "";
    custom.addEventListener("input", function (e) {
      var v = parseInt(e.target.value, 10);
      draft.durationMin = isNaN(v) ? 0 : v;
    });
  }

  function stepperHtml(key, label, val) {
    return '<div class="stepper-row">' +
      '<span class="stepper-label">' + label + "</span>" +
      '<button type="button" class="stepper-btn" data-act="minus" data-key="' + key + '">в€’</button>' +
      '<span class="stepper-val" data-val="' + key + '">' + val + "</span>" +
      '<button type="button" class="stepper-btn" data-act="plus" data-key="' + key + '">+</button>' +
    "</div>";
  }

  var ORGASM_TYPES = {
    male: ["РѕР±С‹С‡РЅС‹Р№", "РїСЂРѕСЃС‚Р°С‚РёС‡РµСЃРєРёР№"],
    female: ["РєР»РёС‚РѕСЂР°Р»СЊРЅС‹Р№", "РІР°РіРёРЅР°Р»СЊРЅС‹Р№"]
  };

  function orgasmTallyChips(key, gender) {
    var types = ORGASM_TYPES[gender] || ORGASM_TYPES.female;
    return types.map(function (t) {
      return '<button type="button" class="chip-toggle orgasm-tally" data-okey="' + key + '" data-otype="' + esc(t) + '">' + esc(t) + "</button>";
    }).join("");
  }

  function orgasmStars(key) {
    var rating = key === "me" ? draft.orgasmMeRating : draft.orgasmPartnerRating;
    var html = "";
    for (var i = 1; i <= 5; i++) {
      html += '<button type="button" class="star' + (rating >= i ? " on" : "") +
        '" data-okey="' + key + '" data-orating="' + i + '">' + (rating >= i ? "в…" : "в†") + "</button>";
    }
    return '<div class="orgasm-stars">' + html + "</div>";
  }

  function stepOrgasms(body) {
    DB.getAllPartners().then(function (partners) {
      var p = null;
      partners.forEach(function (x) { if (!p && x.name === draft.partner) p = x; });
      var partnerLabel = p
        ? avatarHtml(p) + "<span>" + esc(p.name) + "</span>"
        : (draft.partner ? esc(draft.partner) : "РџР°СЂС‚РЅС‘СЂ");
      var pGender = p ? (p.gender || "female") : "female";

      body.innerHTML =
        '<div class="step-label">РћСЂРіР°Р·РјС‹</div>' +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head"><span>РЇ</span>' +
            '<span class="orgasm-count" data-count="me">' + draft.orgasmsMe + "</span>" +
            '<button type="button" class="orgasm-reset" data-rkey="me" title="РЎР±СЂРѕСЃРёС‚СЊ">в†є</button>' +
          "</div>" +
          '<div class="chips-grid orgasm-types">' + orgasmTallyChips("me", myGender) + "</div>" +
          orgasmStars("me") +
        "</div>" +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head orgasm-partner">' + partnerLabel +
            '<span class="orgasm-count" data-count="partner">' + draft.orgasmsPartner + "</span>" +
            '<button type="button" class="orgasm-reset" data-rkey="partner" title="РЎР±СЂРѕСЃРёС‚СЊ">в†є</button>' +
          "</div>" +
          '<div class="chips-grid orgasm-types">' + orgasmTallyChips("partner", pGender) + "</div>" +
          orgasmStars("partner") +
        "</div>";

      body.querySelectorAll(".orgasm-tally").forEach(function (b) {
        b.addEventListener("click", function () {
          var key = b.getAttribute("data-okey");
          var t = b.getAttribute("data-otype");
          var countProp = key === "me" ? "orgasmsMe" : "orgasmsPartner";
          var typeProp = key === "me" ? "orgasmMeType" : "orgasmPartnerType";
          b.classList.add("firing");
          setTimeout(function () {
            b.classList.remove("firing");
            draft[countProp] = Math.min(99, (draft[countProp] || 0) + 1);
            var arr = draft[typeProp] || [];
            if (arr.indexOf(t) === -1) arr.push(t);
            draft[typeProp] = arr;
            var el = body.querySelector('[data-count="' + key + '"]');
            if (el) el.textContent = draft[countProp];
          }, 600);
        });
      });
      body.querySelectorAll(".orgasm-reset").forEach(function (b) {
        b.addEventListener("click", function () {
          var key = b.getAttribute("data-rkey");
          var countProp = key === "me" ? "orgasmsMe" : "orgasmsPartner";
          var typeProp = key === "me" ? "orgasmMeType" : "orgasmPartnerType";
          draft[countProp] = 0;
          draft[typeProp] = [];
          var el = body.querySelector('[data-count="' + key + '"]');
          if (el) el.textContent = "0";
        });
      });
      body.querySelectorAll("[data-orating]").forEach(function (b) {
        b.addEventListener("click", function () {
          var key = b.getAttribute("data-okey");
          var r = parseInt(b.getAttribute("data-orating"), 10);
          var prop = key === "me" ? "orgasmMeRating" : "orgasmPartnerRating";
          draft[prop] = (draft[prop] === r) ? 0 : r;
          stepOrgasms(body);
        });
      });
    });
  }

  function fmtOrg(type, rating) {
    var t = Array.isArray(type) ? type.join(", ") : (type || "");
    if (!t && !rating) return "";
    var s = " (" + t;
    if (rating) s += (t ? ", " : "") + rating + "/5";
    return s + ")";
  }

  function stepSummary(body) {
    var rows = [
      ["Р”Р°С‚Р°", fmtDateTime(draft.startedAt)],
      ["РџР°СЂС‚РЅС‘СЂ", draft.partner || "вЂ”"],
      ["РњРµСЃС‚Рѕ", draft.place || "вЂ”"],
      ["РџРѕР·С‹", draft.positions.join(", ") || "вЂ”"],
      ["РљРѕСЃС‚СЋРјС‹", draft.costumes.join(", ") || "вЂ”"],
      ["РРіСЂСѓС€РєРё", draft.toys.join(", ") || "вЂ”"],
      ["Р¤Р°РЅС‚Р°Р·РёРё", draft.fantasies.join(", ") || "вЂ”"],
      ["РљРѕРЅС‚СЂР°С†РµРїС†РёСЏ", (draft.contraceptiveCount > 0) ? (draft.contraceptives || []).join(", ") + " Г—" + draft.contraceptiveCount : "вЂ”"],
      ["РўРµРіРё", draft.tags.map(function (t) { return "#" + t; }).join(", ") || "вЂ”"],
      ["Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ", (draft.durationMin || 0) + " РјРёРЅ"],
      ["РћСЂРіР°Р·РјС‹", "РЇ: " + draft.orgasmsMe + fmtOrg(draft.orgasmMeType, draft.orgasmMeRating) +
        ", РїР°СЂС‚РЅС‘СЂ: " + draft.orgasmsPartner + fmtOrg(draft.orgasmPartnerType, draft.orgasmPartnerRating)]
    ];
    body.innerHTML =
      '<div class="step-label">Р’СЃС‘ РІРµСЂРЅРѕ?</div>' +
      '<div class="summary">' +
      rows.map(function (r) {
        return '<div class="summary-row"><span>' + r[0] + '</span><span class="summary-val">' + esc(r[1]) + "</span></div>";
      }).join("") +
      "</div>";
  }

  function saveWizard() {
    if (!draft.startedAt) { toast("РЈРєР°Р¶РёС‚Рµ РґР°С‚Сѓ Рё РІСЂРµРјСЏ"); wizardStep = 0; renderWizard(); return; }
    if (!draft.durationMin || draft.durationMin < 1) { toast("РЈРєР°Р¶РёС‚Рµ РґР»РёС‚РµР»СЊРЅРѕСЃС‚СЊ"); wizardStep = 6; renderWizard(); return; }
    var nowIso = new Date().toISOString();
    function apply(rec) {
      rec.startedAt = draft.startedAt;
      rec.durationMin = draft.durationMin;
      rec.partner = draft.partner;
      rec.place = draft.place;
      rec.positions = draft.positions.slice();
      rec.tags = draft.tags.slice();
      rec.costumes = draft.costumes.slice();
      rec.toys = draft.toys.slice();
      rec.fantasies = draft.fantasies.slice();
      rec.orgasmsMe = draft.orgasmsMe;
      rec.orgasmsPartner = draft.orgasmsPartner;
      rec.orgasmMeType = (draft.orgasmMeType || []).slice();
      rec.orgasmPartnerType = (draft.orgasmPartnerType || []).slice();
      rec.orgasmMeRating = draft.orgasmMeRating || 0;
      rec.orgasmPartnerRating = draft.orgasmPartnerRating || 0;
      rec.contraceptives = (draft.contraceptives || []).slice();
      rec.contraceptiveCount = draft.contraceptiveCount || 0;
      rec.garmin = !!draft.garmin;
      rec.garminId = draft.garminId || "";
      rec.updatedAt = nowIso;
      return rec;
    }
    var promise;
    if (editingId) {
      promise = DB.getRecord(editingId).then(function (ex) {
        var rec = apply(ex || {});
        rec.id = editingId;
        return DB.putRecord(rec);
      });
    } else {
      var rec = apply({ id: uuid(), createdAt: nowIso });
      promise = DB.putRecord(rec);
    }
    promise.then(function () {
      toast("РЎРѕС…СЂР°РЅРµРЅРѕ");
      showView("journal");
      renderJournal();
    }).catch(function (e) { toast("РћС€РёР±РєР°: " + e.message); });
  }

  function deleteFromWizard() {
    if (!editingId) return;
    confirmDialog("РЈРґР°Р»РёС‚СЊ СЌС‚Сѓ Р·Р°РїРёСЃСЊ?").then(function (ok) {
      if (!ok) return;
      DB.deleteRecord(editingId).then(function () {
        toast("РЈРґР°Р»РµРЅРѕ");
        showView("journal");
        renderJournal();
      });
    });
  }

  // ---- РїР°СЂС‚РЅС‘СЂС‹ ----
  function renderPartners() {
    DB.getAllPartners().then(function (partners) {
      var list = $("partners-list");
      list.innerHTML = "";
      $("partners-empty").classList.toggle("hidden", partners.length > 0);
      partners.forEach(function (p) {
        var c = document.createElement("button");
        c.type = "button";
        c.className = "partner-row";
        c.innerHTML =
          avatarHtml(p) +
          '<span class="partner-row-text">' +
            '<span class="partner-row-name">' + esc(p.name) + "</span>" +
            (p.notes ? '<span class="partner-row-notes">' + esc(p.notes) + "</span>" : "") +
          "</span>";
        c.addEventListener("click", function () { partnerEditReturn = "partners"; openPartnerEdit(p.id); });
        list.appendChild(c);
      });
    });
  }

  function renderMyGender() {
    if ($("my-gender-male")) $("my-gender-male").classList.toggle("active", myGender === "male");
    if ($("my-gender-female")) $("my-gender-female").classList.toggle("active", myGender === "female");
  }
  function setMyGender(g) {
    myGender = g;
    DB.setMeta("myGender", g);
    renderMyGender();
  }
  function renderPartnerGender() {
    if ($("pe-gender-male")) $("pe-gender-male").classList.toggle("active", partnerEditGender === "male");
    if ($("pe-gender-female")) $("pe-gender-female").classList.toggle("active", partnerEditGender === "female");
  }

  function openPartnerEdit(id) {
    editingPartnerId = id || null;
    $("partner-edit-title").textContent = id ? "РџР°СЂС‚РЅС‘СЂ" : "РќРѕРІС‹Р№ РїР°СЂС‚РЅС‘СЂ";
    $("pe-name").value = "";
    $("pe-notes").value = "";
    $("pe-photo").value = "";
    $("pe-photo-preview").innerHTML = "";
    partnerEditPhotoData = "";
    partnerEditGender = "female";
    renderPartnerGender();
    $("btn-partner-delete").classList.toggle("hidden", !id);
    if (id) {
      DB.getPartner(id).then(function (p) {
        if (!p) return;
        $("pe-name").value = p.name || "";
        $("pe-notes").value = p.notes || "";
        partnerEditPhotoData = p.photo || "";
        partnerEditGender = p.gender || "female";
        renderPartnerGender();
        if (partnerEditPhotoData) {
          $("pe-photo-preview").innerHTML = '<img class="avatar avatar-lg" src="' + partnerEditPhotoData + '" alt="">';
        }
      });
    }
    showView("partner-edit");
  }

  function savePartnerEdit() {
    var name = $("pe-name").value.trim();
    if (!name) { toast("РЈРєР°Р¶РёС‚Рµ РёРјСЏ"); return; }
    var wasNew = !editingPartnerId;
    var nowIso = new Date().toISOString();
    var promise;
    if (editingPartnerId) {
      promise = DB.getPartner(editingPartnerId).then(function (ex) {
        var p = ex || {};
        p.id = editingPartnerId;
        p.name = name;
        p.notes = $("pe-notes").value.trim();
        p.photo = partnerEditPhotoData;
        p.gender = partnerEditGender;
        p.updatedAt = nowIso;
        return DB.putPartner(p);
      });
    } else {
      promise = DB.putPartner({
        id: uuid(), name: name, notes: $("pe-notes").value.trim(),
        photo: partnerEditPhotoData, gender: partnerEditGender, createdAt: nowIso, updatedAt: nowIso
      });
    }
    promise.then(function () {
      toast("РЎРѕС…СЂР°РЅРµРЅРѕ");
      if (partnerEditReturn === "wizard") {
        if (wasNew) { draft.partner = name; draft.partnerTouched = true; }
        showView("wizard");
        renderWizard();
      } else {
        showView("partners");
        renderPartners();
      }
    });
  }

  function deletePartnerEdit() {
    if (!editingPartnerId) return;
    confirmDialog("РЈРґР°Р»РёС‚СЊ РїР°СЂС‚РЅС‘СЂР°?").then(function (ok) {
      if (!ok) return;
      DB.deletePartner(editingPartnerId).then(function () {
        toast("РЈРґР°Р»РµРЅРѕ");
        if (partnerEditReturn === "wizard") { showView("wizard"); renderWizard(); }
        else { showView("partners"); renderPartners(); }
      });
    });
  }

  // ---- РєР°С‚Р°Р»РѕРі (РєРѕСЃС‚СЋРјС‹ / РёРіСЂСѓС€РєРё / С„Р°РЅС‚Р°Р·РёРё) ----
  function renderCatalog() {
    DB.getAllCatalog().then(function (items) {
      var filtered = items.filter(function (it) { return it.kind === catalogKind; });
      filtered.sort(function (a, b) { return (a.archived ? 1 : 0) - (b.archived ? 1 : 0); });
      var recPromise = catalogKind === "fantasy" ? DB.getAllRecords() : Promise.resolve([]);
      recPromise.then(function (records) {
        var realized = new Set();
        records.forEach(function (r) { (r.fantasies || []).forEach(function (f) { realized.add(f); }); });
        var list = $("catalog-list");
        list.innerHTML = "";
        $("catalog-empty").classList.toggle("hidden", filtered.length > 0);
        filtered.forEach(function (it) {
          var c = document.createElement("button");
          c.type = "button";
          c.className = "partner-row" + (it.archived ? " archived" : "");
          var badge = (catalogKind === "fantasy" && realized.has(it.name)) ? '<span class="badge-realized">вњ“</span>' : "";
          var sub = [];
          if (it.brand) sub.push(esc(it.brand));
          if (it.partner) sub.push("рџ‘¤ " + esc(it.partner));
          if (it.notes) sub.push(esc(it.notes));
          c.innerHTML =
            avatarHtml(it) +
            '<span class="partner-row-text">' +
              '<span class="partner-row-name">' + esc(it.name) + " " + badge + (it.archived ? ' <span class="muted">(Р°СЂС…РёРІ)</span>' : "") + "</span>" +
              (sub.length ? '<span class="partner-row-notes">' + sub.join(" В· ") + "</span>" : "") +
            "</span>";
          c.addEventListener("click", function () { catalogEditReturn = "catalog"; openCatalogEdit(it.id, it.kind); });
          list.appendChild(c);
        });
      });
    });
  }

  function updateCatalogEditKindUI() {
    var isToy = catalogEditKind === "toy";
    var isFantasy = catalogEditKind === "fantasy";
    $("ce-brand-wrap").classList.toggle("hidden", !isToy);
    $("ce-archive").classList.toggle("hidden", !isToy);
    if (isToy) $("ce-archive").textContent = catalogEditArchived ? "Р’РµСЂРЅСѓС‚СЊ РёР· Р°СЂС…РёРІР°" : "Р’ Р°СЂС…РёРІ";
    $("ce-partner-wrap").classList.toggle("hidden", !isFantasy);
  }

  function populateCatalogPartnerSelect(selected) {
    DB.getAllPartners().then(function (partners) {
      var sel = $("ce-partner");
      if (!sel) return;
      sel.innerHTML = '<option value="">вЂ”</option>';
      partners.forEach(function (p) {
        var o = document.createElement("option");
        o.value = p.name;
        o.textContent = p.name;
        if (p.name === selected) o.selected = true;
        sel.appendChild(o);
      });
    });
  }

  function openCatalogEdit(id, kind) {
    editingCatalogId = id || null;
    catalogEditKind = kind || (id ? catalogEditKind : catalogKind);
    $("catalog-edit-title").textContent = KIND_META[catalogEditKind].single;
    $("ce-name").value = "";
    $("ce-notes").value = "";
    $("ce-brand").value = "";
    $("ce-photo").value = "";
    $("ce-photo-preview").innerHTML = "";
    catalogEditPhotoData = "";
    catalogEditBrand = "";
    catalogEditArchived = false;
    catalogEditPartner = "";
    updateCatalogEditKindUI();
    if (catalogEditKind === "fantasy") populateCatalogPartnerSelect("");
    $("btn-catalog-delete").classList.toggle("hidden", !id);
    if (id) {
      DB.getCatalogItem(id).then(function (it) {
        if (!it) return;
        catalogEditKind = it.kind || catalogEditKind;
        $("catalog-edit-title").textContent = KIND_META[catalogEditKind].single;
        $("ce-name").value = it.name || "";
        $("ce-notes").value = it.notes || "";
        $("ce-brand").value = it.brand || "";
        catalogEditPhotoData = it.photo || "";
        catalogEditBrand = it.brand || "";
        catalogEditArchived = !!it.archived;
        catalogEditPartner = it.partner || "";
        updateCatalogEditKindUI();
        if (catalogEditKind === "fantasy") populateCatalogPartnerSelect(catalogEditPartner);
        if (catalogEditPhotoData) {
          $("ce-photo-preview").innerHTML = '<img class="avatar avatar-lg" src="' + catalogEditPhotoData + '" alt="">';
        }
      });
    }
    showView("catalog-edit");
  }

  function saveCatalogEdit() {
    var name = $("ce-name").value.trim();
    if (!name) { toast("РЈРєР°Р¶РёС‚Рµ РЅР°Р·РІР°РЅРёРµ"); return; }
    var wasNew = !editingCatalogId;
    var nowIso = new Date().toISOString();
    var promise;
    if (editingCatalogId) {
      promise = DB.getCatalogItem(editingCatalogId).then(function (ex) {
        var it = ex || {};
        it.id = editingCatalogId;
        it.kind = it.kind || catalogEditKind;
        it.name = name;
        it.notes = $("ce-notes").value.trim();
        it.brand = catalogEditBrand;
        it.archived = !!catalogEditArchived;
        it.partner = catalogEditPartner;
        it.photo = catalogEditPhotoData;
        it.updatedAt = nowIso;
        return DB.putCatalogItem(it);
      });
    } else {
      promise = DB.putCatalogItem({
        id: uuid(), kind: catalogEditKind, name: name,
        notes: $("ce-notes").value.trim(), brand: catalogEditBrand, archived: !!catalogEditArchived,
        partner: catalogEditPartner,
        photo: catalogEditPhotoData,
        createdAt: nowIso, updatedAt: nowIso
      });
    }
    promise.then(function () {
      toast("РЎРѕС…СЂР°РЅРµРЅРѕ");
      if (catalogEditReturn === "wizard") {
        if (wasNew) {
          var field = KIND_FIELD[catalogEditKind];
          if (field && draft && draft[field].indexOf(name) === -1) draft[field].push(name);
        }
        showView("wizard");
        renderWizard();
      } else {
        showView("catalog");
        renderCatalog();
      }
    });
  }

  function deleteCatalogEdit() {
    if (!editingCatalogId) return;
    confirmDialog("РЈРґР°Р»РёС‚СЊ?").then(function (ok) {
      if (!ok) return;
      DB.deleteCatalogItem(editingCatalogId).then(function () {
        toast("РЈРґР°Р»РµРЅРѕ");
        if (catalogEditReturn === "wizard") { showView("wizard"); renderWizard(); }
        else { showView("catalog"); renderCatalog(); }
      });
    });
  }

  // ---- РїСЂРѕСЃРјРѕС‚СЂ Р·Р°РїРёСЃРё + СЃРѕРјР°С‚РёРєР° ----
  function openDetail(id) {
    detailRecordId = id;
    detailMetric = "hr";
    detailWindow = 30;
    showView("detail");
    renderDetail();
  }

  function renderDetail() {
    DB.getRecord(detailRecordId).then(function (r) {
      if (!r) { showView("journal"); return; }
      var body = $("detail-body");
      var org = (r.orgasmsMe || 0) + (r.orgasmsPartner || 0);
      var meta = [];
      if (r.partner) meta.push("рџ‘¤ " + esc(r.partner));
      if (r.place) meta.push("рџ“Ќ " + esc(r.place));
      meta.push("вЏ± " + (r.durationMin || 0) + " РјРёРЅ");

      var chips = [];
      (r.positions || []).forEach(function (p) { chips.push('<span class="chip-mini">' + esc(p) + "</span>"); });
      (r.costumes || []).forEach(function (x) { chips.push('<span class="chip-mini">рџ§Ґ ' + esc(x) + "</span>"); });
      (r.toys || []).forEach(function (x) { chips.push('<span class="chip-mini">рџ§ё ' + esc(x) + "</span>"); });
      (r.fantasies || []).forEach(function (x) { chips.push('<span class="chip-mini">вњЁ ' + esc(x) + "</span>"); });
      if ((r.contraceptives || []).length) {
        var contraTxt = "рџЌҐ " + (r.contraceptives || []).join(", ");
        if ((r.contraceptiveCount || 0) > 1) contraTxt += " Г—" + r.contraceptiveCount;
        chips.push('<span class="chip-mini">' + esc(contraTxt) + "</span>");
      }
      (r.tags || []).forEach(function (x) { chips.push('<span class="chip-mini tag">#' + esc(x) + "</span>"); });

      body.innerHTML =
        '<div class="detail-head">' +
          '<div class="detail-date">' +
            (r.garmin ? '<span class="garmin-badge">вЊљ</span>' : "") +
            esc(fmtDateTime(r.startedAt)) +
            (org ? ' <span class="record-org">рџ’— ' + org + "</span>" : "") +
          "</div>" +
          '<div class="record-meta">' + meta.join(" В· ") + "</div>" +
          (chips.length ? '<div class="record-chips">' + chips.join("") + "</div>" : "") +
        "</div>";

      var somatic = document.createElement("div");
      somatic.className = "detail-somatic";
      body.appendChild(somatic);
      renderSomatic(somatic, r);
    });
  }

  function renderSomatic(el, r) {
    var startMs = new Date(r.startedAt).getTime();
    var rangeStartMs = startMs - detailWindow * 60000;
    var rangeEndMs = startMs + ((r.durationMin || 0) + detailWindow) * 60000;
    var rangeStart = minuteKeyFromDate(new Date(rangeStartMs));
    var rangeEnd = minuteKeyFromDate(new Date(rangeEndMs));
    var day = toDateKey(r.startedAt);

    Promise.all([
      DB.getSomaticRange(rangeStart, rangeEnd),
      DB.getSleep(day),
      DB.getWeekly(day)
    ]).then(function (res) {
      var points = res[0] || [];
      var sl = res[1] || null;
      var wk = res[2] || null;
      points.sort(function (a, b) { return a.t < b.t ? -1 : (a.t > b.t ? 1 : 0); });

      var hrPts = points.filter(function (p) { return p.hr != null; }).map(function (p) { return p.hr; });
      var bbPts = points.filter(function (p) { return p.body_battery != null; })
        .sort(function (a, b) { return a.t < b.t ? -1 : 1; });
      var hrAvg = hrPts.length ? Math.round(hrPts.reduce(function (a, b) { return a + b; }, 0) / hrPts.length) : null;
      var hrMax = hrPts.length ? Math.max.apply(null, hrPts) : null;
      var bbFirst = bbPts.length ? bbPts[0].body_battery : null;
      var bbLast = bbPts.length ? bbPts[bbPts.length - 1].body_battery : null;
      var stress = (wk && wk.stress != null) ? wk.stress : null;
      var hrv = (wk && wk.hrv != null) ? wk.hrv : null;

      function row(label, value) {
        return '<div class="act-row"><span>' + label + "</span><b>" + value + "</b></div>";
      }
      var summary =
        '<div class="act-summary">' +
          row("Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ", (r.durationMin || 0) + " РјРёРЅ") +
          row("РџСѓР»СЊСЃ (СЃСЂ / РјР°РєСЃ)", hrAvg != null ? hrAvg + " / " + hrMax : "вЂ”") +
          row("Body Battery", (bbFirst != null && bbLast != null) ? bbFirst + " в†’ " + bbLast + " (" + (bbLast - bbFirst) + ")" : "вЂ”") +
          row("РЎС‚СЂРµСЃСЃ Р·Р° РґРµРЅСЊ", stress != null ? stress : "вЂ”") +
          row("РЎРѕРЅ", (sl && sl.score != null) ? sl.score + " (" + ((sl.durationMin || 0) / 60).toFixed(1) + " С‡)" : "вЂ”") +
          row("HRV (РЅРѕС‡СЊ)", hrv != null ? hrv : "вЂ”") +
        "</div>";

      el.innerHTML =
        "<h2>РЎРѕРјР°С‚РёРєР°</h2>" + summary +
        '<div class="chips-grid" id="metric-chips"></div>' +
        '<div class="chips-grid" id="window-chips" style="margin-top:8px"></div>' +
        '<div class="chart-wrap"><canvas id="somatic-canvas"></canvas></div>' +
        '<div class="somatic-stats" id="somatic-stats"></div>';

      var mc = el.querySelector("#metric-chips");
      METRIC_ORDER.forEach(function (k) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chip-toggle" + (detailMetric === k ? " on" : "");
        b.textContent = METRIC_META[k].label;
        b.addEventListener("click", function () { detailMetric = k; renderDetail(); });
        mc.appendChild(b);
      });

      var wc = el.querySelector("#window-chips");
      [15, 30, 60].forEach(function (w) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chip-toggle" + (detailWindow === w ? " on" : "");
        b.textContent = "В±" + w + " РјРёРЅ";
        b.addEventListener("click", function () { detailWindow = w; renderDetail(); });
        wc.appendChild(b);
      });

      var stats = drawChart($("somatic-canvas"), points, detailMetric, rangeStartMs, rangeEndMs, startMs);
      var s = $("somatic-stats");
      if (stats.count) {
        var m = METRIC_META[detailMetric];
        s.textContent = "РјРёРЅ " + Math.round(stats.min) + " В· СЃСЂ " + Math.round(stats.avg) +
          " В· РјР°РєСЃ " + Math.round(stats.max) + (m.unit ? " " + m.unit : "") + " В· С‚РѕС‡РµРє " + stats.count;
      } else {
        s.textContent = "";
      }
    });
  }

  function drawChart(canvas, points, metricKey, startMs, endMs, markerMs) {
    var ctx = canvas.getContext("2d");
    var dpr = window.devicePixelRatio || 1;
    var cssW = canvas.parentElement.clientWidth || 320;
    var cssH = 200;
    canvas.style.width = cssW + "px";
    canvas.style.height = cssH + "px";
    canvas.width = Math.round(cssW * dpr);
    canvas.height = Math.round(cssH * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    var meta = METRIC_META[metricKey];
    var valid = points.filter(function (p) {
      var v = p[metricKey];
      return v !== null && v !== undefined && !isNaN(v);
    });
    ctx.clearRect(0, 0, cssW, cssH);

    if (valid.length < 2) {
      ctx.fillStyle = "#a49db4";
      ctx.font = "14px sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("РќРµС‚ РґР°РЅРЅС‹С… РІ СЌС‚РѕРј РѕРєРЅРµ", cssW / 2, cssH / 2);
      return { min: null, avg: null, max: null, count: 0 };
    }

    var vals = valid.map(function (p) { return p[metricKey]; });
    var min = Math.min.apply(null, vals);
    var max = Math.max.apply(null, vals);
    if (max - min < 1) max = min + 1;
    var padY = (max - min) * 0.15;
    var yMin = Math.max(0, min - padY);
    var yMax = max + padY;
    var span = endMs - startMs || 1;

    function x(t) { return (t - startMs) / span * cssW; }
    function y(v) { return cssH - (v - yMin) / (yMax - yMin) * cssH; }

    ctx.strokeStyle = "#333040";
    ctx.fillStyle = "#a49db4";
    ctx.font = "11px sans-serif";
    ctx.lineWidth = 1;
    for (var g = 0; g <= 3; g++) {
      var gy = cssH * g / 3;
      var gv = yMax - (yMax - yMin) * g / 3;
      ctx.beginPath(); ctx.moveTo(0, gy); ctx.lineTo(cssW, gy); ctx.stroke();
      ctx.fillText(String(Math.round(gv)), 4, gy - 3);
    }

    ctx.beginPath();
    valid.forEach(function (p, i) {
      var px = x(new Date(p.t).getTime());
      var py = y(p[metricKey]);
      if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
    });
    ctx.strokeStyle = meta.color;
    ctx.lineWidth = 2;
    ctx.lineJoin = "round";
    ctx.stroke();

    ctx.lineTo(x(new Date(valid[valid.length - 1].t).getTime()), cssH);
    ctx.lineTo(x(new Date(valid[0].t).getTime()), cssH);
    ctx.closePath();
    ctx.fillStyle = meta.color + "22";
    ctx.fill();

    if (markerMs >= startMs && markerMs <= endMs) {
      var mx = x(markerMs);
      ctx.strokeStyle = "#f2eef7";
      ctx.setLineDash([4, 4]);
      ctx.beginPath(); ctx.moveTo(mx, 0); ctx.lineTo(mx, cssH); ctx.stroke();
      ctx.setLineDash([]);
    }

    var avg = vals.reduce(function (a, b) { return a + b; }, 0) / vals.length;
    return { min: min, avg: avg, max: max, count: vals.length };
  }

  // ---- РёРјРїРѕСЂС‚ СЃРѕРјР°С‚РёРєРё/СЃРЅР° ----
  function parseSomaticCsv(text) {
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
    function num(v) { if (v === "" || v == null) return null; var n = parseInt(v, 10); return isNaN(n) ? null : n; }
    var out = [];
    for (var r = 1; r < rows.length; r++) {
      var row = rows[r];
      var t = toMinuteKey(get(row, "timestamp", "time", "t", "datetime"));
      if (!t) continue;
      out.push({
        t: t,
        hr: num(get(row, "hr", "heart_rate")),
        stress: num(get(row, "stress")),
        body_battery: num(get(row, "body_battery", "bodybattery", "battery")),
        hrv: num(get(row, "hrv", "rmssd"))
      });
    }
    return out;
  }

  function parseSleepCsv(text) {
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
    function num(v) { if (v === "" || v == null) return null; var n = parseInt(v, 10); return isNaN(n) ? null : n; }
    var out = [];
    for (var r = 1; r < rows.length; r++) {
      var row = rows[r];
      var d = toDateKey(get(row, "date", "day"));
      if (!d) continue;
      out.push({
        date: d,
        score: num(get(row, "sleep_score", "score")),
        durationMin: num(get(row, "duration_min", "duration")),
        deepMin: num(get(row, "deep_min", "deep")),
        lightMin: num(get(row, "light_min", "light")),
        remMin: num(get(row, "rem_min", "rem")),
        awakeMin: num(get(row, "awake_min", "awake"))
      });
    }
    return out;
  }

  function importSomaticFile(file) {
    file.text().then(function (text) {
      var pts = parseSomaticCsv(text);
      if (!pts.length) { toast("РќРµС‚ РґР°РЅРЅС‹С… РІ С„Р°Р№Р»Рµ"); return; }
      DB.putSomaticBatch(pts).then(function () { toast("РЎРѕРјР°С‚РёРєР°: " + pts.length + " С‚РѕС‡РµРє"); });
    }).catch(function (e) { toast("РћС€РёР±РєР°: " + e.message); });
  }

  function importSleepFile(file) {
    file.text().then(function (text) {
      var rows = parseSleepCsv(text);
      if (!rows.length) { toast("РќРµС‚ РґР°РЅРЅС‹С… РІ С„Р°Р№Р»Рµ"); return; }
      DB.putSleepBatch(rows).then(function () { toast("РЎРѕРЅ: " + rows.length + " Р·Р°РїРёСЃРµР№"); });
    }).catch(function (e) { toast("РћС€РёР±РєР°: " + e.message); });
  }

  // ---- РёРјРїРѕСЂС‚ Garmin (РґРёР°Р»РѕРі СЃ Р°РІС‚РѕРѕРїСЂРµРґРµР»РµРЅРёРµРј) ----
  function parseUSDate(s) {
    var parts = String(s || "").trim().split("/");
    if (parts.length !== 3) return null;
    var a = parseInt(parts[0], 10), b = parseInt(parts[1], 10), y = parseInt(parts[2], 10);
    var m, d;
    if (a > 12 && b <= 12) { m = b; d = a; } else { m = a; d = b; }
    if (!m || !d || isNaN(y)) return null;
    return y + "-" + pad(m) + "-" + pad(d);
  }

  function monthOf(label) {
    var s = String(label).toLowerCase().trim();
    for (var key in RU_MONTHS) { if (s.indexOf(key) === 0) return RU_MONTHS[key]; }
    return null;
  }
  function parsePart(part, defaultMonth) {
    var s = String(part).trim().toLowerCase();
    var m = null;
    for (var key in RU_MONTHS) {
      if (s.indexOf(key) === 0) { m = RU_MONTHS[key]; s = s.slice(key.length); break; }
    }
    var day = parseInt(s, 10);
    return { month: (m || defaultMonth), day: day };
  }
  function parseDurationRu(s) {
    var str = String(s || "").toLowerCase();
    var hm = /(\d+)\s*С‡/.exec(str);
    var mm = /(\d+)\s*РјРёРЅ/.exec(str);
    var h = hm ? parseInt(hm[1], 10) : 0;
    var m = mm ? parseInt(mm[1], 10) : 0;
    return h * 60 + m;
  }

  function parseGarminTwoCol(text, field) {
    text = String(text || "").replace(/^\uFEFF/, "");
    var rows = CSV.parse(text);
    var out = [];
    for (var r = 1; r < rows.length; r++) {
      var row = rows[r];
      if (!row || row.length < 2) continue;
      var date = parseUSDate(row[0]);
      var val = parseInt(row[1], 10);
      if (!date || isNaN(val)) continue;
      var obj = { date: date };
      obj[field] = val;
      out.push(obj);
    }
    return out;
  }

  function parseSleepWeekly(text) {
    text = String(text || "").replace(/^\uFEFF/, "");
    var rows = CSV.parse(text);
    var out = [];
    var currentYear = new Date().getFullYear();
    for (var r = 1; r < rows.length; r++) {
      var row = rows[r];
      if (!row || row.length < 4) continue;
      var label = String(row[0]).trim();
      var norm = label.replace(/\s+-\s+/g, "|").replace(/-/g, "|");
      var parts = norm.split("|");
      if (parts.length < 2) continue;
      var first = parsePart(parts[0], null);
      var second = parsePart(parts[1], first.month);
      if (!first.month || isNaN(first.day) || isNaN(second.day)) continue;
      // Сѓ СЃС‚СЂРѕРє РїСЂРѕС€Р»РѕРіРѕ РіРѕРґР° Garmin РґРѕР±Р°РІР»СЏРµС‚ РєРѕР»РѕРЅРєСѓ СЃ РіРѕРґРѕРј: "Р”РµРє 25-31, 2025,76,..."
      var hasYear = /^\s*\d{4}\s*$/.test(String(row[1]));
      var year = hasYear ? parseInt(row[1], 10) : currentYear;
      var score = hasYear ? row[2] : row[1];
      var quality = hasYear ? row[3] : row[2];
      var durationStr = hasYear ? row[4] : row[3];
      currentYear = year;
      out.push({
        end: year + "-" + pad(second.month) + "-" + pad(second.day),
        start: year + "-" + pad(first.month) + "-" + pad(first.day),
        label: label,
        score: parseInt(score, 10) || null,
        quality: String(quality || ""),
        durationMin: parseDurationRu(durationStr)
      });
    }
    return out;
  }

  function detectImportType(rows) {
    if (!rows || !rows.length) return null;
    var header = rows[0].map(function (h) { return String(h).trim().toLowerCase(); });
    var joined = header.join(" ").toLowerCase();
    if (header.indexOf("timestamp") !== -1 || header.indexOf("time") !== -1 || header.indexOf("t") !== -1 || header.indexOf("datetime") !== -1) return "somatic_minute";
    if (header.indexOf("date") !== -1 && (joined.indexOf("sleep") !== -1 || header.indexOf("sleep_score") !== -1 || header.indexOf("duration_min") !== -1 || header.indexOf("deep_min") !== -1)) return "sleep_daily";
    if (header[0] === "РґР°С‚Р°" && (joined.indexOf("СЃСЂРµРґ") !== -1 || joined.indexOf("РєР°С‡РµСЃС‚РІ") !== -1 || joined.indexOf("СЂРµР·") !== -1)) return "sleep_weekly";
    if (joined.indexOf("СЃС‚СЂРµСЃСЃ") !== -1) return "stress_weekly";
    if (joined.indexOf("РїРѕРєРѕР№") !== -1 || joined.indexOf("РїСѓР»СЊСЃР°") !== -1) return "resting_weekly";
    return null;
  }

  function importWeekly(items, field) {
    return DB.getAllWeekly().then(function (existing) {
      var map = {};
      existing.forEach(function (e) { map[e.date] = e; });
      items.forEach(function (it) {
        var e = map[it.date] || { date: it.date };
        e[field] = it[field];
        map[it.date] = e;
      });
      var all = [];
      for (var k in map) all.push(map[k]);
      return DB.putWeeklyBatch(all);
    });
  }

  function openImport() {
    pendingImport = null;
    $("import-result").classList.add("hidden");
    showView("import");
  }

  function handleImportAnyFile(file) {
    file.text().then(function (text) {
      var rows = CSV.parse(String(text || "").replace(/^\uFEFF/, ""));
      var type = detectImportType(rows);
      if (!type) { toast("Р¤РѕСЂРјР°С‚ РЅРµ СЂР°СЃРїРѕР·РЅР°РЅ"); return; }
      var items, label;
      if (type === "stress_weekly") { items = parseGarminTwoCol(text, "stress"); label = "РќРµРґРµР»СЊРЅС‹Р№ СЃС‚СЂРµСЃСЃ"; }
      else if (type === "resting_weekly") { items = parseGarminTwoCol(text, "resting_hr"); label = "РџСѓР»СЊСЃ РїРѕРєРѕСЏ (РЅРµРґРµР»Рё)"; }
      else if (type === "sleep_weekly") { items = parseSleepWeekly(text); label = "РЎРѕРЅ (РЅРµРґРµР»Рё)"; }
      else if (type === "somatic_minute") { items = parseSomaticCsv(text); label = "РЎРѕРјР°С‚РёРєР° (РјРёРЅСѓС‚С‹)"; }
      else if (type === "sleep_daily") { items = parseSleepCsv(text); label = "РЎРѕРЅ (РґРЅРё)"; }
      if (!items.length) { toast("РќРµС‚ РґР°РЅРЅС‹С… РІ С„Р°Р№Р»Рµ"); return; }
      pendingImport = { type: type, items: items };
      $("import-type").textContent = label + " В· " + items.length + " Р·Р°РїРёСЃРµР№";
      $("import-preview").textContent = JSON.stringify(items.slice(0, 3));
      $("import-result").classList.remove("hidden");
    }).catch(function (e) { toast("РћС€РёР±РєР°: " + e.message); });
  }

  function applyImport() {
    if (!pendingImport) return;
    var t = pendingImport.type, items = pendingImport.items;
    var p;
    if (t === "stress_weekly") p = importWeekly(items, "stress");
    else if (t === "resting_weekly") p = importWeekly(items, "resting_hr");
    else if (t === "sleep_weekly") p = DB.putSleepWeeklyBatch(items);
    else if (t === "somatic_minute") p = DB.putSomaticBatch(items);
    else if (t === "sleep_daily") p = DB.putSleepBatch(items);
    p.then(function () {
      toast("РРјРїРѕСЂС‚РёСЂРѕРІР°РЅРѕ: " + items.length);
      pendingImport = null;
      $("import-result").classList.add("hidden");
    }).catch(function (e) { toast("РћС€РёР±РєР°: " + e.message); });
  }

  // ---- С‚СЂРµРЅРґС‹ ----
  function fmtDur(min) {
    if (min == null) return "";
    var m = min % 60, h = Math.floor(min / 60);
    return (h ? h + "С‡ " : "") + (m ? m + "РјРёРЅ" : (h ? "" : "0РјРёРЅ"));
  }

  function openTrends() { showView("trends"); renderTrends(); }

  function renderTrends() {
    var isCorr = trendTab === "corr";
    $("trend-trends").classList.toggle("hidden", isCorr);
    $("trend-corr").classList.toggle("hidden", !isCorr);
    $("trend-tabs").querySelectorAll("button").forEach(function (b) {
      b.classList.toggle("active", b.getAttribute("data-tab") === trendTab);
    });
    if (isCorr) { renderCorrelations(); return; }
    Promise.all([DB.getAllWeekly(), DB.getAllSleepWeekly()]).then(function (res) {
      var weekly = res[0].slice().sort(function (a, b) { return a.date < b.date ? -1 : (a.date > b.date ? 1 : 0); });
      var sleep = res[1].slice().sort(function (a, b) { return a.end < b.end ? -1 : (a.end > b.end ? 1 : 0); });

      var cc = $("trend-chips");
      cc.innerHTML = "";
      ["stress", "resting_hr", "sleep", "hrv"].forEach(function (k) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chip-toggle" + (trendMetric === k ? " on" : "");
        b.textContent = TREND_META[k].label;
        b.addEventListener("click", function () { trendMetric = k; renderTrends(); });
        cc.appendChild(b);
      });

      var canvas = $("trend-canvas");
      var list = $("trend-list");
      list.innerHTML = "";

      if (trendMetric === "sleep") {
        var pts = sleep.filter(function (s) { return s.score != null; }).map(function (s) { return { t: s.end, val: s.score }; });
        drawWeeklyChart(canvas, pts, TREND_META.sleep.color);
        $("trend-stats").textContent = sleep.length ? "РќРµРґРµР»СЊ: " + sleep.length : "";
        sleep.forEach(function (s) {
          var row = document.createElement("div");
          row.className = "sleep-card";
          row.innerHTML =
            '<div class="sleep-title">' + esc(s.label) + "</div>" +
            '<div class="sleep-line">РћС†РµРЅРєР°: <b>' + (s.score != null ? s.score : "вЂ”") + "</b> В· " + esc(s.quality || "") + " В· " + fmtDur(s.durationMin) + "</div>";
          list.appendChild(row);
        });
      } else {
        var field = TREND_META[trendMetric].field;
        var pts2 = weekly.filter(function (w) { return w[field] != null; }).map(function (w) { return { t: w.date, val: w[field] }; });
        drawWeeklyChart(canvas, pts2, TREND_META[trendMetric].color);
        $("trend-stats").textContent = pts2.length ? "РќРµРґРµР»СЊ: " + pts2.length : "";
      }
    });
  }

  function drawWeeklyChart(canvas, pts, color) {
    var ctx = canvas.getContext("2d");
    var dpr = window.devicePixelRatio || 1;
    var cssW = canvas.parentElement.clientWidth || 320;
    var cssH = 200;
    canvas.style.width = cssW + "px";
    canvas.style.height = cssH + "px";
    canvas.width = Math.round(cssW * dpr);
    canvas.height = Math.round(cssH * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, cssW, cssH);

    if (pts.length < 2) {
      ctx.fillStyle = "#a49db4";
      ctx.font = "14px sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("РќРµС‚ РґР°РЅРЅС‹С…", cssW / 2, cssH / 2);
      return;
    }

    var vals = pts.map(function (p) { return p.val; });
    var min = Math.min.apply(null, vals), max = Math.max.apply(null, vals);
    if (max - min < 1) max = min + 1;
    var yMin = Math.max(0, min - (max - min) * 0.15), yMax = max + (max - min) * 0.15;
    function x(i) { return pts.length === 1 ? cssW / 2 : i / (pts.length - 1) * cssW; }
    function y(v) { return cssH - (v - yMin) / (yMax - yMin) * cssH; }

    ctx.strokeStyle = "#333040"; ctx.fillStyle = "#a49db4"; ctx.font = "11px sans-serif"; ctx.lineWidth = 1;
    for (var g = 0; g <= 3; g++) {
      var gy = cssH * g / 3;
      var gv = yMax - (yMax - yMin) * g / 3;
      ctx.beginPath(); ctx.moveTo(0, gy); ctx.lineTo(cssW, gy); ctx.stroke();
      ctx.fillText(String(Math.round(gv)), 4, gy - 3);
    }

    ctx.beginPath();
    pts.forEach(function (p, i) {
      var px = x(i), py = y(p.val);
      if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
    });
    ctx.strokeStyle = color; ctx.lineWidth = 2; ctx.lineJoin = "round"; ctx.stroke();
    ctx.lineTo(x(pts.length - 1), cssH); ctx.lineTo(x(0), cssH); ctx.closePath();
    ctx.fillStyle = color + "22"; ctx.fill();

    ctx.fillStyle = "#a49db4";
    ctx.textAlign = "left"; ctx.fillText(pts[0].t.slice(2), 4, cssH - 4);
    ctx.textAlign = "center"; ctx.fillText(pts[Math.floor(pts.length / 2)].t.slice(2), cssW / 2, cssH - 4);
    ctx.textAlign = "right"; ctx.fillText(pts[pts.length - 1].t.slice(2), cssW - 4, cssH - 4);
  }

  // ---- РєРѕСЂСЂРµР»СЏС†РёРё ----
  function pearson(xs, ys) {
    var n = xs.length;
    if (n < 3) return null;
    var sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
    for (var i = 0; i < n; i++) {
      sx += xs[i]; sy += ys[i];
      sxx += xs[i] * xs[i]; syy += ys[i] * ys[i]; sxy += xs[i] * ys[i];
    }
    var num = n * sxy - sx * sy;
    var den = Math.sqrt((n * sxx - sx * sx) * (n * syy - sy * sy));
    if (!den) return null;
    return num / den;
  }

  function renderCorrelations() {
    Promise.all([DB.getAllRecords(), DB.getAllWeekly(), DB.getAllSleep()]).then(function (res) {
      var records = res[0].slice().sort(function (a, b) { return (a.startedAt || "") < (b.startedAt || "") ? -1 : 1; });
      var weekly = {};
      res[1].forEach(function (w) { weekly[w.date] = w; });
      var sleepByDate = {};
      res[2].forEach(function (s) { sleepByDate[s.date] = s; });
      var summary = $("corr-summary");
      var table = $("corr-table");
      if (records.length < 3) {
        summary.textContent = "РќСѓР¶РЅРѕ С…РѕС‚СЏ Р±С‹ 3 Р·Р°РїРёСЃРё.";
        table.innerHTML = "";
        return;
      }
      var tasks = records.map(function (r) {
        var startMs = new Date(r.startedAt).getTime();
        var rs = minuteKeyFromDate(new Date(startMs - 10 * 60000));
        var re = minuteKeyFromDate(new Date(startMs + ((r.durationMin || 0) + 10) * 60000));
        return DB.getSomaticRange(rs, re).then(function (points) {
          var hr = points.filter(function (p) { return p.hr != null; }).map(function (p) { return p.hr; });
          var bb = points.filter(function (p) { return p.body_battery != null; })
            .sort(function (a, b) { return a.t < b.t ? -1 : 1; });
          var day = String(r.startedAt || "").slice(0, 10);
          var w = weekly[day] || {};
          var sl = sleepByDate[day] || {};
          return {
            r: r,
            avgHr: hr.length ? hr.reduce(function (a, b) { return a + b; }, 0) / hr.length : null,
            maxHr: hr.length ? Math.max.apply(null, hr) : null,
            bbDrop: bb.length >= 2 ? bb[bb.length - 1].body_battery - bb[0].body_battery : null,
            stress: w.stress != null ? w.stress : null,
            sleepScore: sl.score != null ? sl.score : null
          };
        });
      });
      Promise.all(tasks).then(function (rows) {
        var html = '<table class="corr-table"><thead><tr><th>Р”Р°С‚Р°</th><th>РњРёРЅ</th><th>РћСЂРі</th><th>РџСѓР»СЊСЃ</th><th>РњР°РєСЃ</th><th>BB О”</th><th>РЎРѕРЅ</th><th>РЎС‚СЂРµСЃСЃ</th></tr></thead><tbody>';
        rows.forEach(function (x) {
          var org = (x.r.orgasmsMe || 0) + (x.r.orgasmsPartner || 0);
          html += "<tr><td>" + esc(String(x.r.startedAt || "").slice(0, 10)) + "</td>" +
            "<td>" + (x.r.durationMin || 0) + "</td>" +
            "<td>" + org + "</td>" +
            "<td>" + (x.avgHr != null ? Math.round(x.avgHr) : "вЂ”") + "</td>" +
            "<td>" + (x.maxHr != null ? x.maxHr : "вЂ”") + "</td>" +
            "<td>" + (x.bbDrop != null ? x.bbDrop : "вЂ”") + "</td>" +
            "<td>" + (x.sleepScore != null ? x.sleepScore : "вЂ”") + "</td>" +
            "<td>" + (x.stress != null ? x.stress : "вЂ”") + "</td></tr>";
        });
        html += "</tbody></table>";
        table.innerHTML = html;

        var coefs = [];
        function addCoef(field, label) {
          var arr = rows.filter(function (x) { return x[field] != null; });
          if (arr.length < 3) return;
          var c = pearson(arr.map(function (x) { return x.r.durationMin || 0; }), arr.map(function (x) { return x[field]; }));
          if (c != null) coefs.push({ label: label, c: c, n: arr.length });
        }
        addCoef("avgHr", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ в†” РїСѓР»СЊСЃ СЃСЂ");
        addCoef("maxHr", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ в†” РїСѓР»СЊСЃ РјР°РєСЃ");
        addCoef("stress", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ в†” СЃС‚СЂРµСЃСЃ Р·Р° РґРµРЅСЊ");
        addCoef("sleepScore", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ в†” РѕС†РµРЅРєР° СЃРЅР°");

        if (!coefs.length) {
          summary.textContent = "РќРµРґРѕСЃС‚Р°С‚РѕС‡РЅРѕ РґР°РЅРЅС‹С… РґР»СЏ РєРѕСЌС„С„РёС†РёРµРЅС‚РѕРІ.";
          return;
        }
        summary.innerHTML = coefs.map(function (x) {
          return '<div class="corr-coef"><span>' + x.label + "</span><b>" + x.c.toFixed(2) + '</b><span class="muted">n=' + x.n + "</span></div>";
        }).join("");
      });
    });
  }

  // ---- CSV ----
  function recordsToCsv(records) {
    var rows = [CSV_HEADERS];
    records.forEach(function (r) {
      rows.push([
        r.id || "", r.startedAt || "", r.durationMin || 0, r.partner || "", r.place || "",
        (r.positions || []).join(";"), (r.tags || []).join(";"),
        (r.costumes || []).join(";"), (r.toys || []).join(";"), (r.fantasies || []).join(";"),
        r.orgasmsMe || 0, r.orgasmsPartner || 0,
        (r.orgasmMeType || []).join(";"), r.orgasmMeRating || 0,
        (r.orgasmPartnerType || []).join(";"), r.orgasmPartnerRating || 0,
        (r.contraceptives || []).join(";"), r.contraceptiveCount || 0,
        r.garmin ? 1 : 0
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
        costumes: splitList(get(row, "costumes")),
        toys: splitList(get(row, "toys")),
        fantasies: splitList(get(row, "fantasies")),
        orgasmsMe: parseInt(get(row, "orgasms_me", "orgasmsme") || "0", 10) || 0,
        orgasmsPartner: parseInt(get(row, "orgasms_partner", "orgasmspartner") || "0", 10) || 0,
        orgasmMeType: splitList(get(row, "orgasm_me_type")),
        orgasmMeRating: parseInt(get(row, "orgasm_me_rating") || "0", 10) || 0,
        orgasmPartnerType: splitList(get(row, "orgasm_partner_type")),
        orgasmPartnerRating: parseInt(get(row, "orgasm_partner_rating") || "0", 10) || 0,
        contraceptives: splitList(get(row, "contraceptives")),
        contraceptiveCount: parseInt(get(row, "contraceptive_count") || "0", 10) || 0,
        garmin: String(get(row, "garmin")) === "1",
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
      toast("Р­РєСЃРїРѕСЂС‚РёСЂРѕРІР°РЅРѕ РІ CSV");
    });
  }

  function importCsvFile(file) {
    file.text().then(function (text) {
      var records = csvToRecords(text);
      if (!records.length) { toast("CSV РїСѓСЃС‚ РёР»Рё РЅРµ СЂР°СЃРїРѕР·РЅР°РЅ"); return; }
      var chain = Promise.resolve();
      records.forEach(function (rec) { chain = chain.then(function () { return DB.putRecord(rec); }); });
      chain.then(function () {
        toast("РРјРїРѕСЂС‚РёСЂРѕРІР°РЅРѕ Р·Р°РїРёСЃРµР№: " + records.length);
        renderJournal();
      });
    }).catch(function (e) { toast("РћС€РёР±РєР° РёРјРїРѕСЂС‚Р°: " + e.message); });
  }

  // ---- РњРёРіСЂР°С†РёСЏ IndexedDB в†’ Р±СЌРєРµРЅРґ (FE-003) ----
  var migrating = false;
  function forEachSeq(list, fn) {
    return list.reduce(function (p, item) { return p.then(function () { return fn(item); }); }, Promise.resolve());
  }
  function migrateData() {
    if (migrating) { toast("РњРёРіСЂР°С†РёСЏ СѓР¶Рµ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ"); return; }
    if (!window.confirm("РџРµСЂРµРЅРµСЃС‚Рё Р»РѕРєР°Р»СЊРЅС‹Рµ РґР°РЅРЅС‹Рµ (Р¶СѓСЂРЅР°Р»/РїР°СЂС‚РЅС‘СЂС‹/РєР°С‚Р°Р»РѕРі) РІ Р±СЌРєРµРЅРґ? РџРѕРІС‚РѕСЂРЅС‹Р№ Р·Р°РїСѓСЃРє РїСЂРѕРїСѓСЃС‚РёС‚ РґСѓР±Р»Рё.")) return;
    migrating = true;
    toast("РњРёРіСЂР°С†РёСЏ РЅР°С‡Р°С‚Р°вЂ¦");
    var nameToId = {};
    var n = { partners: 0, catalog: 0, records: 0, skipped: 0 };

    DB.rawPartners()
      .then(function (ps) {
        return forEachSeq(ps || [], function (p) {
          return DB.putPartner({ name: p.name, photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" })
            .then(function (saved) { nameToId[p.name] = saved.id; n.partners++; })
            .catch(function () {});
        });
      })
      .then(function () {
        return DB.rawCatalog().then(function (cs) {
          return forEachSeq(cs || [], function (it) {
            return DB.putCatalogItem({ kind: it.kind, name: it.name, photo: it.photo || "", notes: it.notes || "", brand: it.brand || "", archived: !!it.archived, partner: it.partner || "" })
              .then(function () { n.catalog++; })
              .catch(function () {});
          });
        });
      })
      .then(function () {
        return Promise.all([DB.rawRecords(), DB.getAllRecords()]).then(function (res) {
          var seen = {};
          (res[1] || []).forEach(function (r) { seen[(r.startedAt || "") + "|" + (r.partnerId || "") + "|" + (r.durationMin || 0)] = true; });
          return forEachSeq(res[0] || [], function (r) {
            var pid = nameToId[r.partner] || "";
            var key = (r.startedAt || "") + "|" + pid + "|" + (r.durationMin || 0);
            if (seen[key]) { n.skipped++; return Promise.resolve(); }
            seen[key] = true;
            r.partnerId = pid;
            return DB.putRecord(r).then(function () { n.records++; }).catch(function () {});
          });
        });
      })
      .then(function () {
        migrating = false;
        toast("РњРёРіСЂР°С†РёСЏ: РїР°СЂС‚РЅС‘СЂРѕРІ " + n.partners + ", РєР°С‚Р°Р»РѕРіР° " + n.catalog + ", Р·Р°РїРёСЃРµР№ " + n.records + " (РїСЂРѕРїСѓС‰РµРЅРѕ " + n.skipped + ")");
        renderJournal(); renderPartners(); renderCatalog();
      })
      .catch(function (e) {
        migrating = false;
        toast("РћС€РёР±РєР° РјРёРіСЂР°С†РёРё: " + e.message);
      });
  }

  // ---- PIN (РґРёР°Р»РѕРі) ----
  function refreshPinStatus() {
    Pin.hasPin().then(function (has) {
      $("pin-status").textContent = has ? "PIN-РєРѕРґ СѓСЃС‚Р°РЅРѕРІР»РµРЅ" : "PIN-РєРѕРґ РЅРµ СѓСЃС‚Р°РЅРѕРІР»РµРЅ";
      $("btn-pin-open").textContent = has ? "РЎРјРµРЅРёС‚СЊ PIN" : "РЈСЃС‚Р°РЅРѕРІРёС‚СЊ PIN";
    });
  }
  function openPinDialog() {
    Pin.hasPin().then(function (has) {
      $("pin-current-wrap").classList.toggle("hidden", !has);
      $("btn-pin-remove").classList.toggle("hidden", !has);
      $("pin-current").value = "";
      $("pin-new").value = "";
      $("pin-confirm").value = "";
      $("pin-modal").classList.remove("hidden");
      $("pin-new").focus();
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
          if (!ok) { toast("РўРµРєСѓС‰РёР№ PIN РЅРµРІРµСЂРµРЅ"); return false; }
          return true;
        });
      }
      proceed.then(function (ok) {
        if (!ok) return;
        if (!Pin.isValidPin(nw)) { toast("РќРѕРІС‹Р№ PIN: 4вЂ“8 С†РёС„СЂ"); return; }
        if (nw !== cf) { toast("PIN РЅРµ СЃРѕРІРїР°РґР°СЋС‚"); return; }
        Pin.setPin(nw).then(function () {
          Api.privacy.setPin(nw).catch(function () {});
          toast("PIN СЃРѕС…СЂР°РЅС‘РЅ");
          $("pin-modal").classList.add("hidden");
          refreshPinStatus();
        });
      });
    });
  }
  function removePinNow() {
    confirmDialog("РЈРґР°Р»РёС‚СЊ PIN-РєРѕРґ?").then(function (ok) {
      if (!ok) return;
      Pin.removePin().then(function () {
        Api.auth.logout();
        Api.privacy.removePin().catch(function () {});
        toast("PIN СѓРґР°Р»С‘РЅ");
        $("pin-modal").classList.add("hidden");
        refreshPinStatus();
      });
    });
  }

  // ---- РїРѕР»РЅР°СЏ РѕС‡РёСЃС‚РєР° ----
  function wipeAll() {
    confirmDialog("РЎС‚РµСЂРµС‚СЊ РІСЃРµ РґР°РЅРЅС‹Рµ (Р·Р°РїРёСЃРё, СЃРѕРјР°С‚РёРєСѓ, СЃРѕРЅ, С‚СЂРµРЅРґС‹) Рё PIN-РєРѕРґ? Р­С‚Рѕ РЅРµРѕР±СЂР°С‚РёРјРѕ.").then(function (ok) {
      if (!ok) return;
      Promise.all([DB.clearRecords(), DB.clearSomatic(), DB.clearSleep(), DB.clearWeekly(), DB.clearSleepWeekly(), Pin.removePin()]).then(function () {
        Api.auth.logout();
        toast("Р’СЃРµ РґР°РЅРЅС‹Рµ СѓРґР°Р»РµРЅС‹");
        renderJournal();
        refreshPinStatus();
      });
    });
  }

  // ---- РёРЅРёС†РёР°Р»РёР·Р°С†РёСЏ ----
  function init() {
    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.register("./sw.js").catch(function () {});
    }
    if ($("app-version")) $("app-version").textContent = "XDiary v" + APP_VERSION;

    $("pin-submit").addEventListener("click", unlock);
    $("pin-input").addEventListener("keydown", function (e) { if (e.key === "Enter") unlock(); });

    $("btn-new").addEventListener("click", function () { openWizard(null); });
    $("btn-lock").addEventListener("click", lock);

    $("tab-journal").addEventListener("click", function () { showView("journal"); });
    $("tab-partners").addEventListener("click", function () { renderPartners(); showView("partners"); });
    $("tab-catalog").addEventListener("click", function () { renderCatalog(); showView("catalog"); });
    $("tab-trends").addEventListener("click", function () { openTrends(); });
    $("tab-settings").addEventListener("click", function () { refreshPinStatus(); showView("settings"); });

    $("btn-partner-add").addEventListener("click", function () { partnerEditReturn = "partners"; openPartnerEdit(null); });
    $("btn-partner-edit-back").addEventListener("click", function () {
      if (partnerEditReturn === "wizard") { showView("wizard"); renderWizard(); }
      else { showView("partners"); renderPartners(); }
    });
    $("btn-partner-save").addEventListener("click", savePartnerEdit);
    $("btn-partner-delete").addEventListener("click", deletePartnerEdit);
    $("pe-photo").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (!f) return;
      fileToResizedDataUrl(f, 512).then(function (dataUrl) {
        partnerEditPhotoData = dataUrl;
        $("pe-photo-preview").innerHTML = '<img class="avatar avatar-lg" src="' + dataUrl + '" alt="">';
      }).catch(function () { toast("РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ С„РѕС‚Рѕ"); });
    });

    // РєР°С‚Р°Р»РѕРі
    $("btn-catalog-add").addEventListener("click", function () { catalogEditReturn = "catalog"; openCatalogEdit(null, catalogKind); });
    $("catalog-tabs").querySelectorAll("button").forEach(function (b) {
      b.addEventListener("click", function () {
        catalogKind = b.getAttribute("data-kind");
        $("catalog-tabs").querySelectorAll("button").forEach(function (x) { x.classList.toggle("active", x === b); });
        renderCatalog();
      });
    });
    $("btn-catalog-edit-back").addEventListener("click", function () {
      if (catalogEditReturn === "wizard") { showView("wizard"); renderWizard(); }
      else { showView("catalog"); renderCatalog(); }
    });
    $("btn-catalog-save").addEventListener("click", saveCatalogEdit);
    $("btn-catalog-delete").addEventListener("click", deleteCatalogEdit);
    $("ce-photo").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (!f) return;
      fileToResizedDataUrl(f, 512).then(function (dataUrl) {
        catalogEditPhotoData = dataUrl;
        $("ce-photo-preview").innerHTML = '<img class="avatar avatar-lg" src="' + dataUrl + '" alt="">';
      }).catch(function () { toast("РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ С„РѕС‚Рѕ"); });
    });
    $("ce-brand").addEventListener("input", function (e) { catalogEditBrand = e.target.value.trim(); });
    $("ce-partner").addEventListener("change", function (e) { catalogEditPartner = e.target.value; });
    $("ce-archive").addEventListener("click", function () {
      catalogEditArchived = !catalogEditArchived;
      $("ce-archive").textContent = catalogEditArchived ? "Р’РµСЂРЅСѓС‚СЊ РёР· Р°СЂС…РёРІР°" : "Р’ Р°СЂС…РёРІ";
    });

    $("btn-detail-back").addEventListener("click", function () { showView("journal"); });
    $("btn-detail-edit").addEventListener("click", function () { if (detailRecordId) openWizard(detailRecordId); });
    $("btn-import-open").addEventListener("click", openImport);
    $("btn-import-back").addEventListener("click", function () { showView("settings"); });
    $("btn-import-pick").addEventListener("click", function () { $("import-any-file").click(); });
    $("import-any-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) handleImportAnyFile(f);
      e.target.value = "";
    });
    $("btn-import-apply").addEventListener("click", applyImport);

    $("btn-wizard-close").addEventListener("click", function () { showView("journal"); });
    $("btn-wizard-prev").addEventListener("click", function () { if (wizardStep > 0) { wizardStep--; renderWizard(); } });
    $("btn-wizard-next").addEventListener("click", function () {
      if (wizardStep === 0 && !draft.startedAt) { toast("РЈРєР°Р¶РёС‚Рµ РґР°С‚Сѓ Рё РІСЂРµРјСЏ"); return; }
      if (wizardStep < STEPS.length - 1) { wizardStep++; renderWizard(); }
    });
    $("btn-wizard-save").addEventListener("click", saveWizard);
    $("btn-wizard-delete").addEventListener("click", deleteFromWizard);

    $("btn-pin-open").addEventListener("click", openPinDialog);
    $("btn-pin-set").addEventListener("click", applyPin);
    $("btn-pin-remove").addEventListener("click", removePinNow);
    $("pin-cancel").addEventListener("click", function () { $("pin-modal").classList.add("hidden"); });
    $("my-gender-male").addEventListener("click", function () { setMyGender("male"); });
    $("my-gender-female").addEventListener("click", function () { setMyGender("female"); });
    $("pe-gender-male").addEventListener("click", function () { partnerEditGender = "male"; renderPartnerGender(); });
    $("pe-gender-female").addEventListener("click", function () { partnerEditGender = "female"; renderPartnerGender(); });
    DB.getMeta("myGender").then(function (r) { myGender = (r && r.value) || "male"; renderMyGender(); });
    $("btn-export").addEventListener("click", downloadCsv);
    $("btn-import").addEventListener("click", function () { $("import-file").click(); });
    $("btn-migrate").addEventListener("click", migrateData);
    $("import-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) importCsvFile(f);
      e.target.value = "";
    });
    $("btn-wipe").addEventListener("click", wipeAll);

    $("trend-tabs").querySelectorAll("button").forEach(function (b) {
      b.addEventListener("click", function () { trendTab = b.getAttribute("data-tab"); renderTrends(); });
    });
    var apiBase = Api.config.base();
    if ($("api-base")) $("api-base").value = apiBase;
    $("api-base").addEventListener("change", function () {
      Api.config.setBase($("api-base").value.trim());
      toast("РђРґСЂРµСЃ API СЃРѕС…СЂР°РЅС‘РЅ");
    });
    // Р°РІС‚РѕР±Р»РѕРєРёСЂРѕРІРєР° РїРѕСЃР»Рµ РґРѕР»РіРѕРіРѕ СЃРєСЂС‹С‚РёСЏ РїСЂРёР»РѕР¶РµРЅРёСЏ
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
