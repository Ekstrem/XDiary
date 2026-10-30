/* XDiary — PIN-код: PBKDF2-SHA256 с солью, хранится только хеш */
(function () {
  "use strict";

  var ITERATIONS = 100000;
  var enc = new TextEncoder();

  function bufToB64(buf) {
    var bytes = new Uint8Array(buf);
    var s = "";
    for (var i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
    return btoa(s);
  }
  function b64ToBuf(b64) {
    var s = atob(b64);
    var bytes = new Uint8Array(s.length);
    for (var i = 0; i < s.length; i++) bytes[i] = s.charCodeAt(i);
    return bytes;
  }
  function bufToHex(buf) {
    return Array.prototype.map.call(new Uint8Array(buf), function (b) {
      return b.toString(16).padStart(2, "0");
    }).join("");
  }
  function hexToBuf(hex) {
    var bytes = new Uint8Array(hex.length / 2);
    for (var i = 0; i < bytes.length; i++) bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
    return bytes;
  }

  function derive(pin, salt) {
    return crypto.subtle.importKey("raw", enc.encode(pin), "PBKDF2", false, ["deriveBits"])
      .then(function (key) {
        return crypto.subtle.deriveBits(
          { name: "PBKDF2", hash: "SHA-256", salt: salt, iterations: ITERATIONS },
          key,
          256
        );
      })
      .then(function (bits) { return new Uint8Array(bits); });
  }

  function timingSafeEqual(a, b) {
    if (a.length !== b.length) return false;
    var diff = 0;
    for (var i = 0; i < a.length; i++) diff |= a[i] ^ b[i];
    return diff === 0;
  }

  window.Pin = {
    isValidPin: function (pin) { return /^\d{4,8}$/.test(pin); },
    hasPin: function () { return DB.getMeta("pin").then(function (r) { return !!r; }); },
    setPin: function (pin) {
      var salt = crypto.getRandomValues(new Uint8Array(16));
      return derive(pin, salt).then(function (hash) {
        return DB.setMeta("pin", { salt: bufToB64(salt), hash: bufToHex(hash), iterations: ITERATIONS });
      });
    },
    verifyPin: function (pin) {
      return DB.getMeta("pin").then(function (rec) {
        if (!rec || !rec.value) return true; // PIN не установлен
        var v = rec.value;
        var salt = b64ToBuf(v.salt);
        return derive(pin, salt).then(function (hash) {
          return timingSafeEqual(hash, hexToBuf(v.hash));
        });
      });
    },
    removePin: function () { return DB.deleteMeta("pin"); }
  };
})();
