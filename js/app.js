/* XDiary — логика приложения (визард-запись, журнал, партнёры, каталог, настройки) */
(function () {
  "use strict";

  function $(id) { return document.getElementById(id); }

  var CSV_HEADERS = ["id", "started_at", "duration_min", "partner", "place", "positions", "tags", "costumes", "toys", "fantasies", "orgasms_me", "orgasms_partner"];
  var STEPS = ["Дата", "Партнёр", "Место", "Позы", "Отметки", "Длительность", "Оргазмы", "Итог"];
  var POSITIONS = [
    "Миссионерская", "Догги (сзади)", "Наездница (сверху)", "Обратная наездница",
    "Ложки", "69", "Стоя", "Сидя (на коленях)", "Оральный", "Коленно-локтевая",
    "Ножницы", "Миссионерская (ноги вверх)"
  ];
  var DURATION_PRESETS = [10, 15, 20, 30, 45, 60, 90];

  var KIND_ORDER = ["costume", "toy", "fantasy"];
  var KIND_META = {
    costume: { title: "Костюмчики", single: "Костюм", icon: "🧥" },
    toy: { title: "Игрушки", single: "Игрушка", icon: "🧸" },
    fantasy: { title: "Фантазии", single: "Фантазия", icon: "✨" }
  };
  var KIND_FIELD = { costume: "costumes", toy: "toys", fantasy: "fantasies" };

  // состояние
  var draft = null;
  var wizardStep = 0;
  var editingId = null;
  var editingPartnerId = null;
  var partnerEditReturn = "partners";
  var partnerEditPhotoData = "";
  var catalogKind = "costume";
  var editingCatalogId = null;
  var catalogEditKind = "costume";
  var catalogEditReturn = "catalog";
  var catalogEditPhotoData = "";

  var detailRecordId = null;
  var detailMetric = "hr";
  var detailWindow = 30;
  var METRIC_ORDER = ["hr", "stress", "body_battery", "hrv"];
  var METRIC_META = {
    hr: { label: "Пульс", color: "#e0567a", unit: "уд/мин" },
    stress: { label: "Стресс", color: "#f0a04b", unit: "" },
    body_battery: { label: "Body Battery", color: "#4ade80", unit: "" },
    hrv: { label: "HRV", color: "#60a5fa", unit: "мс" }
  };

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

  var TAB_VIEWS = ["journal", "partners", "catalog", "settings"];
  function showView(name) {
    ["lock", "journal", "partners", "catalog", "settings", "wizard", "partner-edit", "catalog-edit"].forEach(function (v) {
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
            '<span class="record-date">' + esc(fmtDateTime(r.startedAt)) + "</span>" +
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
      durationMin: 0, orgasmsMe: 0, orgasmsPartner: 0, partnerTouched: false
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
          orgasmsPartner: r.orgasmsPartner || 0, partnerTouched: true
        };
        showView("wizard");
        renderWizard();
      });
    } else {
      DB.getAllRecords().then(function (records) {
        draft = newDraft();
        draft.partner = lastPartnerName(records);
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
      case 5: stepDuration(body); break;
      case 6: stepOrgasms(body); break;
      case 7: stepSummary(body); break;
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

  function stepPlace(body) {
    body.innerHTML =
      '<div class="step-label">Где?</div>' +
      '<input type="text" id="w-place" list="place-suggest" placeholder="Например: дома" value="' + esc(draft.place) + '">';
    $("w-place").addEventListener("input", function (e) { draft.place = e.target.value.trim(); });
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

  // ---- отметки (костюмы / игрушки / фантазии / другие) ----
  function stepMarks(body) {
    DB.getAllCatalog().then(function (items) {
      body.innerHTML = '<div class="step-label">Отметки</div>';

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

      // другие отметки (свободные тэги)
      var tags = document.createElement("div");
      tags.className = "marks-group";
      tags.innerHTML =
        '<div class="marks-group-title">Другие отметки</div>' +
        '<div class="chips-grid" id="w-tag-grid"></div>' +
        '<input type="text" id="w-tag-custom" placeholder="Своя отметка" autocomplete="off">';
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

  function stepOrgasms(body) {
    body.innerHTML =
      '<div class="step-label">Оргазмы</div>' +
      stepperHtml("me", "Я", draft.orgasmsMe) +
      stepperHtml("partner", "Партнёр", draft.orgasmsPartner);

    body.querySelectorAll(".stepper-btn").forEach(function (b) {
      b.addEventListener("click", function () {
        var key = b.getAttribute("data-key");
        var act = b.getAttribute("data-act");
        var prop = key === "me" ? "orgasmsMe" : "orgasmsPartner";
        draft[prop] = act === "plus" ? Math.min(99, draft[prop] + 1) : Math.max(0, draft[prop] - 1);
        body.querySelector('[data-val="' + key + '"]').textContent = draft[prop];
      });
    });
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
      ["Отметки", draft.tags.map(function (t) { return "#" + t; }).join(", ") || "—"],
      ["Длительность", (draft.durationMin || 0) + " мин"],
      ["Оргазмы", "Я: " + draft.orgasmsMe + ", партнёр: " + draft.orgasmsPartner]
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
    if (!draft.durationMin || draft.durationMin < 1) { toast("Укажите длительность"); wizardStep = 5; renderWizard(); return; }
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
    if (!window.confirm("Удалить эту запись?")) return;
    DB.deleteRecord(editingId).then(function () {
      toast("Удалено");
      showView("journal");
      renderJournal();
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

  function openPartnerEdit(id) {
    editingPartnerId = id || null;
    $("partner-edit-title").textContent = id ? "Партнёр" : "Новый партнёр";
    $("pe-name").value = "";
    $("pe-notes").value = "";
    $("pe-photo").value = "";
    $("pe-photo-preview").innerHTML = "";
    partnerEditPhotoData = "";
    $("btn-partner-delete").classList.toggle("hidden", !id);
    if (id) {
      DB.getPartner(id).then(function (p) {
        if (!p) return;
        $("pe-name").value = p.name || "";
        $("pe-notes").value = p.notes || "";
        partnerEditPhotoData = p.photo || "";
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
        p.updatedAt = nowIso;
        return DB.putPartner(p);
      });
    } else {
      promise = DB.putPartner({
        id: uuid(), name: name, notes: $("pe-notes").value.trim(),
        photo: partnerEditPhotoData, createdAt: nowIso, updatedAt: nowIso
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
    if (!window.confirm("Удалить партнёра?")) return;
    DB.deletePartner(editingPartnerId).then(function () {
      toast("Удалено");
      if (partnerEditReturn === "wizard") { showView("wizard"); renderWizard(); }
      else { showView("partners"); renderPartners(); }
    });
  }

  // ---- каталог (костюмы / игрушки / фантазии) ----
  function renderCatalog() {
    DB.getAllCatalog().then(function (items) {
      var filtered = items.filter(function (it) { return it.kind === catalogKind; });
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
          c.className = "partner-row";
          var badge = (catalogKind === "fantasy" && realized.has(it.name)) ? '<span class="badge-realized">✓</span>' : "";
          c.innerHTML =
            avatarHtml(it) +
            '<span class="partner-row-text">' +
              '<span class="partner-row-name">' + esc(it.name) + " " + badge + "</span>" +
              (it.notes ? '<span class="partner-row-notes">' + esc(it.notes) + "</span>" : "") +
            "</span>";
          c.addEventListener("click", function () { catalogEditReturn = "catalog"; openCatalogEdit(it.id, it.kind); });
          list.appendChild(c);
        });
      });
    });
  }

  function openCatalogEdit(id, kind) {
    editingCatalogId = id || null;
    catalogEditKind = kind || (id ? catalogEditKind : catalogKind);
    $("catalog-edit-title").textContent = KIND_META[catalogEditKind].single;
    $("ce-name").value = "";
    $("ce-notes").value = "";
    $("ce-photo").value = "";
    $("ce-photo-preview").innerHTML = "";
    catalogEditPhotoData = "";
    $("btn-catalog-delete").classList.toggle("hidden", !id);
    if (id) {
      DB.getCatalogItem(id).then(function (it) {
        if (!it) return;
        catalogEditKind = it.kind || catalogEditKind;
        $("catalog-edit-title").textContent = KIND_META[catalogEditKind].single;
        $("ce-name").value = it.name || "";
        $("ce-notes").value = it.notes || "";
        catalogEditPhotoData = it.photo || "";
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
        it.photo = catalogEditPhotoData;
        it.updatedAt = nowIso;
        return DB.putCatalogItem(it);
      });
    } else {
      promise = DB.putCatalogItem({
        id: uuid(), kind: catalogEditKind, name: name,
        notes: $("ce-notes").value.trim(), photo: catalogEditPhotoData,
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
    if (!window.confirm("Удалить?")) return;
    DB.deleteCatalogItem(editingCatalogId).then(function () {
      toast("Удалено");
      if (catalogEditReturn === "wizard") { showView("wizard"); renderWizard(); }
      else { showView("catalog"); renderCatalog(); }
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
      (r.tags || []).forEach(function (x) { chips.push('<span class="chip-mini tag">#' + esc(x) + "</span>"); });

      body.innerHTML =
        '<div class="detail-head">' +
          '<div class="detail-date">' + esc(fmtDateTime(r.startedAt)) +
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

    el.innerHTML =
      "<h2>Соматика</h2>" +
      '<div class="chips-grid" id="metric-chips"></div>' +
      '<div class="chips-grid" id="window-chips" style="margin-top:8px"></div>' +
      '<div class="chart-wrap"><canvas id="somatic-canvas"></canvas></div>' +
      '<div class="somatic-stats" id="somatic-stats"></div>' +
      '<div class="sleep-card" id="sleep-card"></div>';

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

    DB.getSomaticRange(rangeStart, rangeEnd).then(function (points) {
      points.sort(function (a, b) { return a.t < b.t ? -1 : (a.t > b.t ? 1 : 0); });
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

    DB.getSleep(toDateKey(r.startedAt)).then(function (sl) {
      var c = $("sleep-card");
      if (!sl) { c.innerHTML = ""; return; }
      var h = ((sl.durationMin || 0) / 60).toFixed(1);
      c.innerHTML =
        '<div class="sleep-title">😴 Сон</div>' +
        '<div class="sleep-line">Оценка: <b>' + (sl.score != null ? sl.score : "—") + "</b> · " + h + " ч</div>" +
        (sl.deepMin != null
          ? '<div class="sleep-line muted">Глубокий ' + sl.deepMin + " · Лёгкий " + sl.lightMin +
            " · REM " + sl.remMin + " · Бодрствование " + sl.awakeMin + " мин</div>"
          : "");
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

  // ---- CSV ----
  function recordsToCsv(records) {
    var rows = [CSV_HEADERS];
    records.forEach(function (r) {
      rows.push([
        r.id || "", r.startedAt || "", r.durationMin || 0, r.partner || "", r.place || "",
        (r.positions || []).join(";"), (r.tags || []).join(";"),
        (r.costumes || []).join(";"), (r.toys || []).join(";"), (r.fantasies || []).join(";"),
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
        costumes: splitList(get(row, "costumes")),
        toys: splitList(get(row, "toys")),
        fantasies: splitList(get(row, "fantasies")),
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
    Promise.all([DB.clearRecords(), DB.clearSomatic(), DB.clearSleep(), Pin.removePin()]).then(function () {
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

    $("pin-submit").addEventListener("click", unlock);
    $("pin-input").addEventListener("keydown", function (e) { if (e.key === "Enter") unlock(); });

    $("btn-new").addEventListener("click", function () { openWizard(null); });
    $("btn-lock").addEventListener("click", lock);

    $("tab-journal").addEventListener("click", function () { showView("journal"); });
    $("tab-partners").addEventListener("click", function () { renderPartners(); showView("partners"); });
    $("tab-catalog").addEventListener("click", function () { renderCatalog(); showView("catalog"); });
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

    $("btn-detail-back").addEventListener("click", function () { showView("journal"); });
    $("btn-detail-edit").addEventListener("click", function () { if (detailRecordId) openWizard(detailRecordId); });
    $("btn-import-somatic").addEventListener("click", function () { $("somatic-file").click(); });
    $("btn-import-sleep").addEventListener("click", function () { $("sleep-file").click(); });
    $("somatic-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) importSomaticFile(f);
      e.target.value = "";
    });
    $("sleep-file").addEventListener("change", function (e) {
      var f = e.target.files && e.target.files[0];
      if (f) importSleepFile(f);
      e.target.value = "";
    });

    $("btn-wizard-close").addEventListener("click", function () { showView("journal"); });
    $("btn-wizard-prev").addEventListener("click", function () { if (wizardStep > 0) { wizardStep--; renderWizard(); } });
    $("btn-wizard-next").addEventListener("click", function () {
      if (wizardStep === 0 && !draft.startedAt) { toast("Укажите дату и время"); return; }
      if (wizardStep < STEPS.length - 1) { wizardStep++; renderWizard(); }
    });
    $("btn-wizard-save").addEventListener("click", saveWizard);
    $("btn-wizard-delete").addEventListener("click", deleteFromWizard);

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
