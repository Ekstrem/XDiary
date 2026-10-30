/* XDiary — логика приложения (визард-запись, журнал, партнёры, каталог, настройки) */
(function () {
  "use strict";

  function $(id) { return document.getElementById(id); }

  var CSV_HEADERS = ["id", "started_at", "duration_min", "partner", "place", "positions", "tags", "costumes", "toys", "fantasies", "contraceptives", "contraceptive_count", "orgasms_me", "orgasms_partner", "orgasm_me_type", "orgasm_me_rating", "orgasm_partner_type", "orgasm_partner_rating", "garmin"];
  var STEPS = ["Дата", "Партнёр", "Место", "Позы", "Аксессуары", "Контрацепция", "Длительность", "Оргазмы", "Итог"];
  var POSITIONS = [
    "Миссионерская", "Догги (сзади)", "Наездница (сверху)", "Обратная наездница",
    "Ложки", "69", "Стоя", "Сидя (на коленях)", "Оральный", "Коленно-локтевая",
    "Ножницы", "Миссионерская (ноги вверх)"
  ];
  var DURATION_PRESETS = [10, 15, 20, 30, 45, 60, 90];

  var KIND_ORDER = ["costume", "toy", "fantasy", "contraception"];
  var KIND_META = {
    costume: { title: "Костюмчики", single: "Костюм", icon: "🧥" },
    toy: { title: "Игрушки", single: "Игрушка", icon: "🧸" },
    fantasy: { title: "Фантазии", single: "Фантазия", icon: "✨" },
    contraception: { title: "Контрацепция", single: "Средство", icon: "🍥" }
  };
  var KIND_FIELD = { costume: "costumes", toy: "toys", fantasy: "fantasies", contraception: "contraceptives" };

  // состояние
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
    hr: { label: "Пульс", color: "#e0567a", unit: "уд/мин" },
    stress: { label: "Стресс", color: "#f0a04b", unit: "" },
    body_battery: { label: "Body Battery", color: "#4ade80", unit: "" },
    hrv: { label: "HRV", color: "#60a5fa", unit: "мс" }
  };

  var trendMetric = "stress";
  var trendTab = "trends";
  var TREND_META = {
    stress: { label: "Стресс", color: "#f0a04b", field: "stress" },
    resting_hr: { label: "Пульс покоя", color: "#e0567a", field: "resting_hr" },
    sleep: { label: "Сон (оценка)", color: "#4ade80", field: "score" },
    hrv: { label: "HRV", color: "#60a5fa", field: "hrv" }
  };
  var RU_MONTHS = { "янв": 1, "фев": 2, "мар": 3, "апр": 4, "май": 5, "июн": 6, "июл": 7, "авг": 8, "сен": 9, "окт": 10, "ноя": 11, "дек": 12 };
  var pendingImport = null;

  var garminProxy = "";
  var garminLastSync = 0;
  var garminLastSyncDate = "";
  var pendingActivity = null;
  var currentSuggestion = null;
  var HEART_CHARS = "❤♥🖤💔❣";
  var DEFAULT_GARMIN_PROXY = "http://192.168.1.224:8765";
  var APP_VERSION = "0.27"; // синхронизировать с CACHE в sw.js

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
    ["lock", "journal", "partners", "catalog", "trends", "settings", "wizard", "partner-edit", "catalog-edit", "import", "garmin", "detail"].forEach(function (v) {
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
        var marks = [];
        (r.costumes || []).forEach(function (x) { marks.push('<span class="chip-mini">🧥 ' + esc(x) + "</span>"); });
        (r.toys || []).forEach(function (x) { marks.push('<span class="chip-mini">🧸 ' + esc(x) + "</span>"); });
        (r.fantasies || []).forEach(function (x) { marks.push('<span class="chip-mini">✨ ' + esc(x) + "</span>"); });
        (r.tags || []).forEach(function (x) { marks.push('<span class="chip-mini tag">#' + esc(x) + "</span>"); });
        var marksHtml = marks.length ? '<div class="record-chips">' + marks.join("") + "</div>" : "";

        card.innerHTML =
          '<div class="record-card-top">' +
            '<span class="record-date">' +
              (r.garmin ? '<span class="garmin-badge">⌚</span>' : "") +
              esc(fmtDateTime(r.startedAt)) +
            "</span>" +
            '<span class="record-org">' + (org ? "💗 " + org : "") + "</span>" +
          "</div>" +
          '<div class="record-meta">' + meta.join(" · ") + "</div>" +
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

  // ---- визард ----
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
        if (pendingActivity) {
          var s = String(pendingActivity.startLocal || "").replace(" ", "T").slice(0, 16);
          if (s) draft.startedAt = s;
          var dm = Math.round((parseFloat(pendingActivity.durationSec) || 0) / 60);
          if (dm > 0) draft.durationMin = dm;
          draft.garmin = true;
          draft.garminId = pendingActivity.id ? String(pendingActivity.id) : "";
          wizardStep = 1;
          pendingActivity = null;
        }
        showView("wizard");
        renderWizard();
      });
    }
  }

  function renderWizard() {
    $("wizard-title").textContent = editingId ? "Запись" : "Новая запись";

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
      '<div class="step-label">Когда это было?</div>' +
      '<input type="datetime-local" id="w-startedAt" value="' + esc(draft.startedAt) + '">';
    $("w-startedAt").addEventListener("input", function (e) { draft.startedAt = e.target.value; });
  }

  function stepPartner(body) {
    DB.getAllPartners().then(function (partners) {
      DB.getAllRecords().then(function (records) {
        body.innerHTML =
          '<div class="step-label">С кем?</div>' +
          '<div class="partner-grid" id="w-partner-grid"></div>' +
          '<div class="field">' +
            '<input type="text" id="w-partner-custom" list="partner-suggest" placeholder="Или введите имя" autocomplete="off">' +
          "</div>" +
          '<button type="button" class="btn" id="w-partner-none">Без партнёра</button>';

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
        add.innerHTML = '<span class="avatar avatar-initial">＋</span><span>Добавить</span>';
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
        '<div class="step-label">Где?</div>' +
        '<div class="chips-grid" id="w-place-grid"></div>' +
        '<div class="field" style="margin-top:16px">' +
          '<input type="text" id="w-place" list="place-suggest" placeholder="Своё место" autocomplete="off">' +
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
        '<div class="step-label">Позы</div>' +
        '<div class="chips-grid" id="w-pos-grid"></div>' +
        '<div class="field" style="margin-top:16px">' +
          '<input type="text" id="w-pos-custom" placeholder="Своя поза" autocomplete="off">' +
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

  // ---- аксессуары (костюмы / игрушки / фантазии / теги) ----
  function stepMarks(body) {
    DB.getAllCatalog().then(function (items) {
      body.innerHTML = '<div class="step-label">Аксессуары</div>';

      var kinds = [
        { key: "costume", label: "Костюмчики" },
        { key: "toy", label: "Игрушки" },
        { key: "fantasy", label: "Реализованные фантазии" }
      ];
      kinds.forEach(function (k) {
        var field = KIND_FIELD[k.key];
        var grp = document.createElement("div");
        grp.className = "marks-group";
        grp.innerHTML =
          '<div class="marks-group-title">' + k.label +
            ' <button type="button" class="chip-add" data-kind="' + k.key + '">＋</button></div>' +
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
          hint.textContent = "Пока пусто";
          grid.appendChild(hint);
        }
        grp.querySelector(".chip-add").addEventListener("click", function () {
          catalogEditReturn = "wizard";
          openCatalogEdit(null, k.key);
        });
      });

      // теги (свободные)
      var tags = document.createElement("div");
      tags.className = "marks-group";
      tags.innerHTML =
        '<div class="marks-group-title">Теги</div>' +
        '<div class="chips-grid" id="w-tag-grid"></div>' +
        '<input type="text" id="w-tag-custom" placeholder="Свой тег" autocomplete="off">';
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
        '<div class="step-label">Контрацепция</div>' +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head"><span>Использовано</span>' +
            '<span class="orgasm-count" data-cnt="contra">' + draft.contraceptiveCount + "</span>" +
            '<button type="button" class="orgasm-reset" data-rcnt="contra" title="Сбросить">↺</button>' +
          "</div>" +
          (chips ? '<div class="chips-grid orgasm-types">' + chips + "</div>" : '<p class="muted">Нет средств — добавьте в Каталоге (вкладка «Контрацепция»).</p>') +
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
      '<div class="step-label">Сколько длилось?</div>' +
      '<div class="chips-grid" id="w-dur-grid"></div>' +
      '<div class="field" style="margin-top:16px">' +
        '<input type="number" id="w-dur-custom" min="1" inputmode="numeric" placeholder="Своё значение, мин">' +
      "</div>";

    var grid = $("w-dur-grid");
    DURATION_PRESETS.forEach(function (d) {
      var b = document.createElement("button");
      b.type = "button";
      b.className = "chip-toggle" + (draft.durationMin === d ? " on" : "");
      b.textContent = d + " мин";
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
      '<button type="button" class="stepper-btn" data-act="minus" data-key="' + key + '">−</button>' +
      '<span class="stepper-val" data-val="' + key + '">' + val + "</span>" +
      '<button type="button" class="stepper-btn" data-act="plus" data-key="' + key + '">+</button>' +
    "</div>";
  }

  var ORGASM_TYPES = {
    male: ["обычный", "простатический"],
    female: ["клиторальный", "вагинальный"]
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
        '" data-okey="' + key + '" data-orating="' + i + '">' + (rating >= i ? "★" : "☆") + "</button>";
    }
    return '<div class="orgasm-stars">' + html + "</div>";
  }

  function stepOrgasms(body) {
    DB.getAllPartners().then(function (partners) {
      var p = null;
      partners.forEach(function (x) { if (!p && x.name === draft.partner) p = x; });
      var partnerLabel = p
        ? avatarHtml(p) + "<span>" + esc(p.name) + "</span>"
        : (draft.partner ? esc(draft.partner) : "Партнёр");
      var pGender = p ? (p.gender || "female") : "female";

      body.innerHTML =
        '<div class="step-label">Оргазмы</div>' +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head"><span>Я</span>' +
            '<span class="orgasm-count" data-count="me">' + draft.orgasmsMe + "</span>" +
            '<button type="button" class="orgasm-reset" data-rkey="me" title="Сбросить">↺</button>' +
          "</div>" +
          '<div class="chips-grid orgasm-types">' + orgasmTallyChips("me", myGender) + "</div>" +
          orgasmStars("me") +
        "</div>" +
        '<div class="orgasm-block">' +
          '<div class="orgasm-head orgasm-partner">' + partnerLabel +
            '<span class="orgasm-count" data-count="partner">' + draft.orgasmsPartner + "</span>" +
            '<button type="button" class="orgasm-reset" data-rkey="partner" title="Сбросить">↺</button>' +
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
      ["Дата", fmtDateTime(draft.startedAt)],
      ["Партнёр", draft.partner || "—"],
      ["Место", draft.place || "—"],
      ["Позы", draft.positions.join(", ") || "—"],
      ["Костюмы", draft.costumes.join(", ") || "—"],
      ["Игрушки", draft.toys.join(", ") || "—"],
      ["Фантазии", draft.fantasies.join(", ") || "—"],
      ["Контрацепция", (draft.contraceptiveCount > 0) ? (draft.contraceptives || []).join(", ") + " ×" + draft.contraceptiveCount : "—"],
      ["Теги", draft.tags.map(function (t) { return "#" + t; }).join(", ") || "—"],
      ["Длительность", (draft.durationMin || 0) + " мин"],
      ["Оргазмы", "Я: " + draft.orgasmsMe + fmtOrg(draft.orgasmMeType, draft.orgasmMeRating) +
        ", партнёр: " + draft.orgasmsPartner + fmtOrg(draft.orgasmPartnerType, draft.orgasmPartnerRating)]
    ];
    body.innerHTML =
      '<div class="step-label">Всё верно?</div>' +
      '<div class="summary">' +
      rows.map(function (r) {
        return '<div class="summary-row"><span>' + r[0] + '</span><span class="summary-val">' + esc(r[1]) + "</span></div>";
      }).join("") +
      "</div>";
  }

  function saveWizard() {
    if (!draft.startedAt) { toast("Укажите дату и время"); wizardStep = 0; renderWizard(); return; }
    if (!draft.durationMin || draft.durationMin < 1) { toast("Укажите длительность"); wizardStep = 6; renderWizard(); return; }
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
      toast("Сохранено");
      showView("journal");
      renderJournal();
    }).catch(function (e) { toast("Ошибка: " + e.message); });
  }

  function deleteFromWizard() {
    if (!editingId) return;
    confirmDialog("Удалить эту запись?").then(function (ok) {
      if (!ok) return;
      DB.deleteRecord(editingId).then(function () {
        toast("Удалено");
        showView("journal");
        renderJournal();
      });
    });
  }

  // ---- партнёры ----
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
    $("partner-edit-title").textContent = id ? "Партнёр" : "Новый партнёр";
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
    if (!name) { toast("Укажите имя"); return; }
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
      toast("Сохранено");
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
    confirmDialog("Удалить партнёра?").then(function (ok) {
      if (!ok) return;
      DB.deletePartner(editingPartnerId).then(function () {
        toast("Удалено");
        if (partnerEditReturn === "wizard") { showView("wizard"); renderWizard(); }
        else { showView("partners"); renderPartners(); }
      });
    });
  }

  // ---- каталог (костюмы / игрушки / фантазии) ----
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
          var badge = (catalogKind === "fantasy" && realized.has(it.name)) ? '<span class="badge-realized">✓</span>' : "";
          var sub = [];
          if (it.brand) sub.push(esc(it.brand));
          if (it.partner) sub.push("👤 " + esc(it.partner));
          if (it.notes) sub.push(esc(it.notes));
          c.innerHTML =
            avatarHtml(it) +
            '<span class="partner-row-text">' +
              '<span class="partner-row-name">' + esc(it.name) + " " + badge + (it.archived ? ' <span class="muted">(архив)</span>' : "") + "</span>" +
              (sub.length ? '<span class="partner-row-notes">' + sub.join(" · ") + "</span>" : "") +
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
    if (isToy) $("ce-archive").textContent = catalogEditArchived ? "Вернуть из архива" : "В архив";
    $("ce-partner-wrap").classList.toggle("hidden", !isFantasy);
  }

  function populateCatalogPartnerSelect(selected) {
    DB.getAllPartners().then(function (partners) {
      var sel = $("ce-partner");
      if (!sel) return;
      sel.innerHTML = '<option value="">—</option>';
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
    if (!name) { toast("Укажите название"); return; }
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
      toast("Сохранено");
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
    confirmDialog("Удалить?").then(function (ok) {
      if (!ok) return;
      DB.deleteCatalogItem(editingCatalogId).then(function () {
        toast("Удалено");
        if (catalogEditReturn === "wizard") { showView("wizard"); renderWizard(); }
        else { showView("catalog"); renderCatalog(); }
      });
    });
  }

  // ---- просмотр записи + соматика ----
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
      if (r.partner) meta.push("👤 " + esc(r.partner));
      if (r.place) meta.push("📍 " + esc(r.place));
      meta.push("⏱ " + (r.durationMin || 0) + " мин");

      var chips = [];
      (r.positions || []).forEach(function (p) { chips.push('<span class="chip-mini">' + esc(p) + "</span>"); });
      (r.costumes || []).forEach(function (x) { chips.push('<span class="chip-mini">🧥 ' + esc(x) + "</span>"); });
      (r.toys || []).forEach(function (x) { chips.push('<span class="chip-mini">🧸 ' + esc(x) + "</span>"); });
      (r.fantasies || []).forEach(function (x) { chips.push('<span class="chip-mini">✨ ' + esc(x) + "</span>"); });
      if ((r.contraceptives || []).length) {
        var contraTxt = "🍥 " + (r.contraceptives || []).join(", ");
        if ((r.contraceptiveCount || 0) > 1) contraTxt += " ×" + r.contraceptiveCount;
        chips.push('<span class="chip-mini">' + esc(contraTxt) + "</span>");
      }
      (r.tags || []).forEach(function (x) { chips.push('<span class="chip-mini tag">#' + esc(x) + "</span>"); });

      body.innerHTML =
        '<div class="detail-head">' +
          '<div class="detail-date">' +
            (r.garmin ? '<span class="garmin-badge">⌚</span>' : "") +
            esc(fmtDateTime(r.startedAt)) +
            (org ? ' <span class="record-org">💗 ' + org + "</span>" : "") +
          "</div>" +
          '<div class="record-meta">' + meta.join(" · ") + "</div>" +
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
          row("Длительность", (r.durationMin || 0) + " мин") +
          row("Пульс (ср / макс)", hrAvg != null ? hrAvg + " / " + hrMax : "—") +
          row("Body Battery", (bbFirst != null && bbLast != null) ? bbFirst + " → " + bbLast + " (" + (bbLast - bbFirst) + ")" : "—") +
          row("Стресс за день", stress != null ? stress : "—") +
          row("Сон", (sl && sl.score != null) ? sl.score + " (" + ((sl.durationMin || 0) / 60).toFixed(1) + " ч)" : "—") +
          row("HRV (ночь)", hrv != null ? hrv : "—") +
        "</div>";

      el.innerHTML =
        "<h2>Соматика</h2>" + summary +
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
        b.textContent = "±" + w + " мин";
        b.addEventListener("click", function () { detailWindow = w; renderDetail(); });
        wc.appendChild(b);
      });

      var stats = drawChart($("somatic-canvas"), points, detailMetric, rangeStartMs, rangeEndMs, startMs);
      var s = $("somatic-stats");
      if (stats.count) {
        var m = METRIC_META[detailMetric];
        s.textContent = "мин " + Math.round(stats.min) + " · ср " + Math.round(stats.avg) +
          " · макс " + Math.round(stats.max) + (m.unit ? " " + m.unit : "") + " · точек " + stats.count;
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
      ctx.fillText("Нет данных в этом окне", cssW / 2, cssH / 2);
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

  // ---- импорт соматики/сна ----
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
      if (!pts.length) { toast("Нет данных в файле"); return; }
      DB.putSomaticBatch(pts).then(function () { toast("Соматика: " + pts.length + " точек"); });
    }).catch(function (e) { toast("Ошибка: " + e.message); });
  }

  function importSleepFile(file) {
    file.text().then(function (text) {
      var rows = parseSleepCsv(text);
      if (!rows.length) { toast("Нет данных в файле"); return; }
      DB.putSleepBatch(rows).then(function () { toast("Сон: " + rows.length + " записей"); });
    }).catch(function (e) { toast("Ошибка: " + e.message); });
  }

  // ---- импорт Garmin (диалог с автоопределением) ----
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
    var hm = /(\d+)\s*ч/.exec(str);
    var mm = /(\d+)\s*мин/.exec(str);
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
      // у строк прошлого года Garmin добавляет колонку с годом: "Дек 25-31, 2025,76,..."
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
    if (header[0] === "дата" && (joined.indexOf("сред") !== -1 || joined.indexOf("качеств") !== -1 || joined.indexOf("рез") !== -1)) return "sleep_weekly";
    if (joined.indexOf("стресс") !== -1) return "stress_weekly";
    if (joined.indexOf("покой") !== -1 || joined.indexOf("пульса") !== -1) return "resting_weekly";
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
      if (!type) { toast("Формат не распознан"); return; }
      var items, label;
      if (type === "stress_weekly") { items = parseGarminTwoCol(text, "stress"); label = "Недельный стресс"; }
      else if (type === "resting_weekly") { items = parseGarminTwoCol(text, "resting_hr"); label = "Пульс покоя (недели)"; }
      else if (type === "sleep_weekly") { items = parseSleepWeekly(text); label = "Сон (недели)"; }
      else if (type === "somatic_minute") { items = parseSomaticCsv(text); label = "Соматика (минуты)"; }
      else if (type === "sleep_daily") { items = parseSleepCsv(text); label = "Сон (дни)"; }
      if (!items.length) { toast("Нет данных в файле"); return; }
      pendingImport = { type: type, items: items };
      $("import-type").textContent = label + " · " + items.length + " записей";
      $("import-preview").textContent = JSON.stringify(items.slice(0, 3));
      $("import-result").classList.remove("hidden");
    }).catch(function (e) { toast("Ошибка: " + e.message); });
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
      toast("Импортировано: " + items.length);
      pendingImport = null;
      $("import-result").classList.add("hidden");
    }).catch(function (e) { toast("Ошибка: " + e.message); });
  }

  // ---- тренды ----
  function fmtDur(min) {
    if (min == null) return "";
    var m = min % 60, h = Math.floor(min / 60);
    return (h ? h + "ч " : "") + (m ? m + "мин" : (h ? "" : "0мин"));
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
        $("trend-stats").textContent = sleep.length ? "Недель: " + sleep.length : "";
        sleep.forEach(function (s) {
          var row = document.createElement("div");
          row.className = "sleep-card";
          row.innerHTML =
            '<div class="sleep-title">' + esc(s.label) + "</div>" +
            '<div class="sleep-line">Оценка: <b>' + (s.score != null ? s.score : "—") + "</b> · " + esc(s.quality || "") + " · " + fmtDur(s.durationMin) + "</div>";
          list.appendChild(row);
        });
      } else {
        var field = TREND_META[trendMetric].field;
        var pts2 = weekly.filter(function (w) { return w[field] != null; }).map(function (w) { return { t: w.date, val: w[field] }; });
        drawWeeklyChart(canvas, pts2, TREND_META[trendMetric].color);
        $("trend-stats").textContent = pts2.length ? "Недель: " + pts2.length : "";
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
      ctx.fillText("Нет данных", cssW / 2, cssH / 2);
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

  // ---- Garmin: прокси + синхронизация ----
  var garminSettingsLoaded = null;

  function loadGarminSettings() {
    if (garminSettingsLoaded) return garminSettingsLoaded;
    garminSettingsLoaded = Promise.all([DB.getMeta("garminProxy"), DB.getMeta("garminLastSyncDate")]).then(function (res) {
      garminProxy = (res[0] && res[0].value) || DEFAULT_GARMIN_PROXY;
      garminLastSyncDate = (res[1] && res[1].value) || "";
      if (!res[0] || !res[0].value) DB.setMeta("garminProxy", DEFAULT_GARMIN_PROXY);
      if ($("gem-proxy")) $("gem-proxy").value = garminProxy;
    });
    return garminSettingsLoaded;
  }

  function saveGarminProxy() {
    garminProxy = $("gem-proxy").value.trim();
    DB.setMeta("garminProxy", garminProxy);
  }
  function garminFetch(path) {
    var base = String(garminProxy || "").replace(/\/+$/, "");
    if (!base) return Promise.reject(new Error("не задан адрес прокси"));
    return fetch(base + path, { headers: { Accept: "application/json" } }).then(function (r) {
      if (!r.ok) throw new Error("HTTP " + r.status);
      return r.json();
    });
  }

  function isoDay(offsetDays) {
    var d = new Date();
    d.setDate(d.getDate() + offsetDays);
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate());
  }

  var currentSyncStep = "";
  var SYNC_LABELS = { stress: "Стресс", somatic: "Пульс + Body Battery", sleep: "Сон", hrv: "HRV", activities: "Занятия" };

  function syncBar(text, state) {
    var bar = $("sync-bar");
    var bt = $("sync-bar-text");
    if (!bar) return;
    bar.classList.remove("hidden");
    bar.classList.remove("ok", "err", "busy");
    if (state) bar.classList.add(state);
    if (bt) bt.textContent = text;
  }

  function syncSetStep(name, state, countText) {
    var li = document.querySelector('#sync-steps li[data-step="' + name + '"]');
    if (li) {
      var ico = li.querySelector(".sync-ico");
      var cnt = li.querySelector(".sync-count");
      if (state === "active") {
        currentSyncStep = name;
        ico.textContent = "⏳";
      } else if (state === "done") {
        ico.textContent = "✅";
        li.classList.add("done");
      } else if (state === "err") {
        ico.textContent = "❌";
        li.classList.add("err");
      }
      if (cnt) cnt.textContent = countText || "";
    }
    var label = SYNC_LABELS[name] || name;
    if (state === "active") syncBar("⏳ " + label + "…", "busy");
    else if (state === "done") syncBar("✅ " + label + (countText || ""), "ok");
    else if (state === "err") syncBar("⚠️ " + label, "err");
  }

  function garminSync(silent) {
    if (!garminProxy) { if (!silent) toast("Укажите адрес прокси (ПК)"); return; }
    var to = isoDay(0);
    var from = (garminLastSyncDate && garminLastSyncDate < isoDay(-30)) ? garminLastSyncDate : isoDay(-30);
    var modal = $("sync-modal");

    document.querySelectorAll("#sync-steps li").forEach(function (li) {
      li.classList.remove("done", "err");
      li.querySelector(".sync-ico").textContent = "⏳";
      li.querySelector(".sync-count").textContent = "";
    });
    if (silent) {
      syncBar("⏳ Синхронизация…", "busy");
    } else if (modal) {
      modal.classList.remove("hidden");
    }

    var chain = Promise.resolve();

    syncSetStep("stress", "active");
    chain = chain.then(function () {
      return garminFetch("/local/stress").then(function (data) {
        var items = [];
        Object.keys(data.stress || {}).forEach(function (d) {
          if (data.stress[d] != null) items.push({ date: d, stress: data.stress[d] });
        });
        syncSetStep("stress", "done", "· " + items.length + " дн.");
        return importWeekly(items, "stress");
      });
    });

    syncSetStep("somatic", "active");
    chain = chain.then(function () {
      return garminFetch("/local/somatic").then(function (data) {
        var somatic = (data.somatic || []).map(function (row) {
          return { t: row[0], hr: row[1], body_battery: row[2] };
        });
        var restItems = [];
        Object.keys(data.resting_hr || {}).forEach(function (d) {
          restItems.push({ date: d, resting_hr: data.resting_hr[d] });
        });
        var c = data.counts || {};
        syncSetStep("somatic", "done", "· пульс " + (c.hr != null ? c.hr : somatic.length) + ", BB " + (c.bb != null ? c.bb : "?"));
        return Promise.all([
          DB.putSomaticBatch(somatic),
          importWeekly(restItems, "resting_hr")
        ]);
      });
    });

    syncSetStep("sleep", "active");
    chain = chain.then(function () {
      return garminFetch("/local/sleep").then(function (data) {
        var sleeps = (data.sleep || []).map(function (s) {
          return {
            date: s.date, score: s.score, durationMin: s.durationMin,
            deepMin: s.deepMin, lightMin: s.lightMin, remMin: s.remMin, awakeMin: s.awakeMin
          };
        });
        syncSetStep("sleep", "done", "· " + sleeps.length + " ночей");
        return DB.putSleepBatch(sleeps);
      });
    });

    syncSetStep("hrv", "active");
    chain = chain.then(function () {
      return garminFetch("/local/hrv").then(function (data) {
        var items = [];
        Object.keys(data.hrv || {}).forEach(function (d) {
          items.push({ date: d, hrv: data.hrv[d] });
        });
        syncSetStep("hrv", "done", "· " + items.length + " дн.");
        return importWeekly(items, "hrv");
      });
    });

    syncSetStep("activities", "active");
    chain = chain.then(function () {
      return garminFetch("/local/activities").then(function (list) {
        var hearts = (list || []).filter(isHeartActivity);
        syncSetStep("activities", "done", "· " + hearts.length);
        return hearts.length;
      }).catch(function () {
        syncSetStep("activities", "done", "· —");
        return null;
      });
    });

    chain.then(function () {
      garminLastSync = Date.now();
      garminLastSyncDate = to;
      DB.setMeta("garminLastSyncDate", to);
      if (silent) {
        var d = new Date();
        var hhmm = ("0" + d.getHours()).slice(-2) + ":" + ("0" + d.getMinutes()).slice(-2);
        syncBar("✅ Синхронизировано " + hhmm, "ok");
        setTimeout(function () { var bar = $("sync-bar"); if (bar) bar.classList.add("hidden"); }, 4000);
      } else {
        toast("Статистика обновлена");
        setTimeout(function () { if (modal) modal.classList.add("hidden"); }, 1000);
      }
    }).catch(function (e) {
      syncSetStep(currentSyncStep, "err");
      if (silent) {
        syncBar("⚠️ Синхронизация: " + e.message, "err");
        setTimeout(function () { var bar = $("sync-bar"); if (bar) bar.classList.add("hidden"); }, 6000);
      } else {
        toast("Ошибка синхронизации: " + e.message);
        setTimeout(function () { if (modal) modal.classList.add("hidden"); }, 1600);
      }
    });
  }

  function isHeartActivity(a) {
    var n = String((a && a.name) || "");
    for (var i = 0; i < HEART_CHARS.length; i++) {
      if (n.indexOf(HEART_CHARS[i]) !== -1) return true;
    }
    return false;
  }

  function garminCheckSuggestions() {
    loadGarminSettings().then(function () {
      if (!garminProxy) return;
      garminFetch("/activities?from=" + isoDay(-10) + "&to=" + isoDay(0)).then(function (list) {
        var hearts = (list || []).filter(isHeartActivity);
        if (!hearts.length) { hideSuggestion(); return; }
        DB.getAllRecords().then(function (records) {
          var logged = records.map(function (r) { return new Date(r.startedAt).getTime(); });
          var candidates = hearts.filter(function (a) {
            var t = new Date(String(a.startLocal).replace(" ", "T")).getTime();
            if (isNaN(t)) return false;
            for (var i = 0; i < logged.length; i++) {
              if (Math.abs(logged[i] - t) < 15 * 60000) return false;
            }
            return true;
          });
          candidates.sort(function (a, b) {
            return new Date(String(b.startLocal).replace(" ", "T")) - new Date(String(a.startLocal).replace(" ", "T"));
          });
          if (candidates.length) showSuggestion(candidates[0]);
          else hideSuggestion();
        });
      }).catch(function () { /* тихо: прокси недоступен */ });
    });
  }

  function showSuggestion(a) {
    currentSuggestion = a;
    var when = fmtDateTime(String(a.startLocal).replace(" ", "T"));
    $("suggest-text").textContent = "Возможно, было занятие " + when + " (" + (a.type || "other") + "). Внести в журнал?";
    $("garmin-suggest").classList.remove("hidden");
  }
  function hideSuggestion() {
    currentSuggestion = null;
    $("garmin-suggest").classList.add("hidden");
  }
  function openWizardWithActivity(a) {
    pendingActivity = a;
    openWizard(null);
  }

  function garminTest() {
    var st = $("gem-status");
    if (!garminProxy) { toast("Укажите адрес прокси"); return; }
    if (st) st.textContent = "Проверка…";
    garminFetch("/health").then(function () {
      if (st) st.textContent = "Прокси доступен ✓";
      toast("Прокси доступен");
    }).catch(function (e) {
      var msg = (e && e.message) ? e.message : String(e);
      if (st) st.textContent = "Недоступен: " + msg + " (VPN? прокси запущен?)";
      toast("Прокси недоступен: " + msg);
    });
  }

  // ---- Garmin: выбор занятия ----
  var garminHearts = [];
  var garminShown = 0;
  var GARMIN_PAGE = 10;

  function openGarminPicker() {
    showView("garmin");
    renderGarminPicker();
  }

  function renderGarminPage() {
    var list = $("garmin-list");
    var more = $("garmin-more");
    if (!list) return;
    list.innerHTML = "";
    garminHearts.slice(0, garminShown).forEach(function (a) {
      var row = document.createElement("div");
      row.className = "garmin-item";
      var dur = Math.round((a.durationSec || 0) / 60);
      row.innerHTML =
        '<div class="garmin-item-main">' +
          '<span class="garmin-item-date">' + esc(String(a.startLocal || "").slice(0, 16)) + "</span>" +
          '<span class="garmin-item-meta">' + esc(a.type || "other") + " · " + dur + " мин</span>" +
        "</div>" +
        '<button class="btn primary" type="button">Внести</button>';
      row.querySelector("button").addEventListener("click", function () {
        openWizardWithActivity(a);
      });
      list.appendChild(row);
    });
    if (more) {
      if (garminShown < garminHearts.length) {
        more.classList.remove("hidden");
        more.textContent = "Показать ещё (" + (garminHearts.length - garminShown) + ")";
      } else {
        more.classList.add("hidden");
      }
    }
  }

  function garminMore() {
    garminShown = Math.min(garminShown + GARMIN_PAGE, garminHearts.length);
    renderGarminPage();
  }

  function renderGarminPicker() {
    var list = $("garmin-list");
    var empty = $("garmin-empty");
    var st = $("garmin-status");
    garminHearts = [];
    garminShown = 0;
    list.innerHTML = "";
    empty.classList.add("hidden");
    loadGarminSettings().then(function () {
      if (!garminProxy) {
        empty.classList.remove("hidden");
        empty.querySelector("p").textContent = "Настройте прокси в Настройках.";
        return;
      }
      if (st) st.classList.remove("hidden");
      garminFetch("/local/activities").then(function (items) {
        var hearts = (items || []).filter(isHeartActivity);
        return DB.getAllRecords().then(function (records) {
          var imported = {};
          var importedStart = {};
          records.forEach(function (r) {
            if (r.garminId) imported[String(r.garminId)] = true;
            if (r.garmin && r.startedAt) importedStart[r.startedAt] = true;
          });
          garminHearts = hearts.filter(function (a) {
            var startKey = String(a.startLocal || "").replace(" ", "T").slice(0, 16);
            return !imported[String(a.id)] && !importedStart[startKey];
          });
        });
      }).then(function () {
        if (st) st.classList.add("hidden");
        if (!garminHearts.length) {
          empty.classList.remove("hidden");
          empty.querySelector("p").textContent = "Нет «сердечных» занятий за последние 30 дней.";
          return;
        }
        garminShown = Math.min(GARMIN_PAGE, garminHearts.length);
        renderGarminPage();
      }).catch(function () {
        if (st) st.classList.add("hidden");
        empty.classList.remove("hidden");
        empty.querySelector("p").textContent = "Не удалось связаться с прокси.";
      });
    });
  }

  // ---- корреляции ----
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
        summary.textContent = "Нужно хотя бы 3 записи.";
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
        var html = '<table class="corr-table"><thead><tr><th>Дата</th><th>Мин</th><th>Орг</th><th>Пульс</th><th>Макс</th><th>BB Δ</th><th>Сон</th><th>Стресс</th></tr></thead><tbody>';
        rows.forEach(function (x) {
          var org = (x.r.orgasmsMe || 0) + (x.r.orgasmsPartner || 0);
          html += "<tr><td>" + esc(String(x.r.startedAt || "").slice(0, 10)) + "</td>" +
            "<td>" + (x.r.durationMin || 0) + "</td>" +
            "<td>" + org + "</td>" +
            "<td>" + (x.avgHr != null ? Math.round(x.avgHr) : "—") + "</td>" +
            "<td>" + (x.maxHr != null ? x.maxHr : "—") + "</td>" +
            "<td>" + (x.bbDrop != null ? x.bbDrop : "—") + "</td>" +
            "<td>" + (x.sleepScore != null ? x.sleepScore : "—") + "</td>" +
            "<td>" + (x.stress != null ? x.stress : "—") + "</td></tr>";
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
        addCoef("avgHr", "Длительность ↔ пульс ср");
        addCoef("maxHr", "Длительность ↔ пульс макс");
        addCoef("stress", "Длительность ↔ стресс за день");
        addCoef("sleepScore", "Длительность ↔ оценка сна");

        if (!coefs.length) {
          summary.textContent = "Недостаточно данных для коэффициентов.";
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

  // ---- PIN (диалог) ----
  function refreshPinStatus() {
    Pin.hasPin().then(function (has) {
      $("pin-status").textContent = has ? "PIN-код установлен" : "PIN-код не установлен";
      $("btn-pin-open").textContent = has ? "Сменить PIN" : "Установить PIN";
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
          if (!ok) { toast("Текущий PIN неверен"); return false; }
          return true;
        });
      }
      proceed.then(function (ok) {
        if (!ok) return;
        if (!Pin.isValidPin(nw)) { toast("Новый PIN: 4–8 цифр"); return; }
        if (nw !== cf) { toast("PIN не совпадают"); return; }
        Pin.setPin(nw).then(function () {
          toast("PIN сохранён");
          $("pin-modal").classList.add("hidden");
          refreshPinStatus();
        });
      });
    });
  }
  function removePinNow() {
    confirmDialog("Удалить PIN-код?").then(function (ok) {
      if (!ok) return;
      Pin.removePin().then(function () {
        toast("PIN удалён");
        $("pin-modal").classList.add("hidden");
        refreshPinStatus();
      });
    });
  }

  // ---- полная очистка ----
  function wipeAll() {
    confirmDialog("Стереть все данные (записи, соматику, сон, тренды) и PIN-код? Это необратимо.").then(function (ok) {
      if (!ok) return;
      Promise.all([DB.clearRecords(), DB.clearSomatic(), DB.clearSleep(), DB.clearWeekly(), DB.clearSleepWeekly(), Pin.removePin()]).then(function () {
        toast("Все данные удалены");
        renderJournal();
        refreshPinStatus();
      });
    });
  }

  // ---- инициализация ----
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
      }).catch(function () { toast("Не удалось загрузить фото"); });
    });

    // каталог
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
      }).catch(function () { toast("Не удалось загрузить фото"); });
    });
    $("ce-brand").addEventListener("input", function (e) { catalogEditBrand = e.target.value.trim(); });
    $("ce-partner").addEventListener("change", function (e) { catalogEditPartner = e.target.value; });
    $("ce-archive").addEventListener("click", function () {
      catalogEditArchived = !catalogEditArchived;
      $("ce-archive").textContent = catalogEditArchived ? "Вернуть из архива" : "В архив";
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
      if (wizardStep === 0 && !draft.startedAt) { toast("Укажите дату и время"); return; }
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
    $("import-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) importCsvFile(f);
      e.target.value = "";
    });
    $("btn-wipe").addEventListener("click", wipeAll);

    $("gem-proxy").addEventListener("change", saveGarminProxy);

    $("btn-garmin-sync").addEventListener("click", function () { garminSync(false); });
    $("btn-garmin-test").addEventListener("click", garminTest);
    $("btn-garmin-add").addEventListener("click", openGarminPicker);
    $("btn-garmin-back").addEventListener("click", function () { showView("journal"); });
    $("garmin-more").addEventListener("click", garminMore);
    $("trend-tabs").querySelectorAll("button").forEach(function (b) {
      b.addEventListener("click", function () { trendTab = b.getAttribute("data-tab"); renderTrends(); });
    });
    $("suggest-add").addEventListener("click", function () {
      if (currentSuggestion) openWizardWithActivity(currentSuggestion);
    });
    $("suggest-hide").addEventListener("click", hideSuggestion);
    loadGarminSettings().then(function () {
      setTimeout(function () { garminSync(true); }, 500);
    });

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
