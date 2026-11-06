"""check_cors_entry.py — вход с другого origin: preflight и ACAO, а не только коды.

Реальный дефект (`DO-106`, 2026-11-06, P0): приложение на `https://ekstrem.github.io`, API — на другом
origin. На проде CORS **не включён** (в `7-deploy` нет ни одного упоминания CORS; APIM в репозитории не
описан), поэтому браузер режет ответ, и пользователь видит «Нет связи с сервером», хотя сервер жив:
`OPTIONS /api/v1/clients` с `Origin` → **401**, `Access-Control-Allow-Origin` **нет**. На dev (YARP)
preflight → **204**, `ACAO=*`.

Почему это не ловилось: каноны `check_apim_auth` / `check_public_entry` проверяют **коды без `Origin`** и без
preflight — дефект «зелёный» по всем гейтам. Это тот же класс, что «зелено на бумаге»: проверялось не то
поведение, которое нужно браузеру.

Что проверяет этот гейт (два запроса, как просил @TL `2026-11-06 12:19`):

1. **preflight** `OPTIONS <base>/api/v1/clients` с `Origin` и `Access-Control-Request-Method: POST`
   → ждём `2xx` **и** `Access-Control-Allow-Origin` (либо `*`, либо ровно наш origin);
2. **фактический запрос** `POST <base>/api/v1/clients` с `Origin` (тело намеренно пустое — важен **заголовок
   ответа**, а не успех бизнес-операции) → ждём `Access-Control-Allow-Origin`.

Коды: `0` — preflight и ACAO в порядке; `1` — нарушение (нет ACAO / preflight не 2xx); `2` — не измерено
(нет `--base`, сеть недоступна) — «не измерено» никогда не считается зелёным.
"""

import argparse
import json
import sys
import urllib.error
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

PROBE_PATH = "/api/v1/clients"


def _acao_ok(value, origin):
    if not value:
        return False
    v = value.strip()
    return v == "*" or v.lower() == (origin or "").strip().lower()


def evaluate(preflight, post, origin):
    """preflight/post: {'status': int, 'acao': str|None} → (issues, stats).

    Чистая функция: её и проверяет `--selftest`, без сети и без сокетов.
    """
    issues = []
    st = preflight.get("status")
    if not isinstance(st, int) or not (200 <= st < 300):
        issues.append(f"preflight `OPTIONS {PROBE_PATH}` → {st}: браузер не пропустит запрос "
                      f"(ждали 2xx)")
    if not _acao_ok(preflight.get("acao"), origin):
        issues.append(f"preflight без `Access-Control-Allow-Origin` (получили {preflight.get('acao')!r}) — "
                      f"браузер заблокирует вход с другого origin")
    if not _acao_ok(post.get("acao"), origin):
        issues.append(f"ответ `POST {PROBE_PATH}` без `Access-Control-Allow-Origin` "
                      f"(получили {post.get('acao')!r}, status {post.get('status')}) — "
                      f"пользователь увидит «Нет связи с сервером» при живом сервере")
    stats = {"preflight": st, "preflight_acao": preflight.get("acao"),
             "post": post.get("status"), "post_acao": post.get("acao")}
    return issues, stats


def receipt_line(stats, issues):
    return (f"квитанция: preflight {stats['preflight']} ACAO={stats['preflight_acao']!r} | "
            f"POST {stats['post']} ACAO={stats['post_acao']!r} | нарушено: {len(issues)}")


def probe(base, origin, timeout=15):
    """Два запроса к живому стенду. Возвращает (preflight, post, error)."""
    def headers_acao(hdrs):
        for k, v in hdrs.items():
            if k.lower() == "access-control-allow-origin":
                return v
        return None

    pre = {"status": None, "acao": None}
    post = {"status": None, "acao": None}
    try:
        req = urllib.request.Request(base.rstrip("/") + PROBE_PATH, method="OPTIONS", headers={
            "Origin": origin,
            "Access-Control-Request-Method": "POST",
            "Access-Control-Request-Headers": "authorization,content-type",
            "User-Agent": "xdiary-cors-probe",
        })
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                pre = {"status": r.status, "acao": headers_acao(dict(r.headers))}
        except urllib.error.HTTPError as e:  # 4xx тоже ответ: ACAO в нём может быть
            pre = {"status": e.code, "acao": headers_acao(dict(e.headers))}

        req2 = urllib.request.Request(
            base.rstrip("/") + PROBE_PATH,
            data=json.dumps({"phone": ""}).encode(),
            method="POST",
            headers={"Origin": origin, "Content-Type": "application/json", "User-Agent": "xdiary-cors-probe"},
        )
        try:
            with urllib.request.urlopen(req2, timeout=timeout) as r:
                post = {"status": r.status, "acao": headers_acao(dict(r.headers))}
        except urllib.error.HTTPError as e:
            post = {"status": e.code, "acao": headers_acao(dict(e.headers))}
    except (urllib.error.URLError, OSError) as e:
        return pre, post, f"стенд недоступен: {e}"
    return pre, post, None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", help="адрес API, например https://xdiary-vps1.…")
    ap.add_argument("--origin", default="https://ekstrem.github.io", help="Origin фронта")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--preflight-status", type=int, help="для офлайн-проверки: подставить статус preflight")
    ap.add_argument("--preflight-acao", help="для офлайн-проверки: подставить ACAO preflight")
    ap.add_argument("--post-status", type=int, help="для офлайн-проверки: подставить статус POST")
    ap.add_argument("--post-acao", help="для офлайн-проверки: подставить ACAO POST")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.preflight_status is not None or a.post_status is not None:
        pre = {"status": a.preflight_status, "acao": a.preflight_acao}
        post = {"status": a.post_status, "acao": a.post_acao}
    else:
        if not a.base:
            print("::error::check_cors_entry: нет --base — не измерено (rc=2)")
            return 2
        pre, post, err = probe(a.base, a.origin)
        if err:
            print(f"::error::check_cors_entry: {err} — не измерено (rc=2)")
            return 2

    issues, stats = evaluate(pre, post, a.origin)
    for m in issues:
        print(f"::error::check_cors_entry: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0

    def case(name, pre, post, origin, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(pre, post, origin)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:190]}")

    o = "https://ekstrem.github.io"
    case("dev-норма: 204 + ACAO *", {"status": 204, "acao": "*"}, {"status": 200, "acao": "*"}, o, 0,
         ["preflight 204", "нарушено: 0"])

    case("прод-дефект DO-106: 401 без ACAO", {"status": 401, "acao": None}, {"status": 401, "acao": None}, o, 1,
         ["401", "браузер не пропустит", "«Нет связи с сервером»"])

    case("preflight ок, но у POST нет ACAO", {"status": 204, "acao": "*"}, {"status": 200, "acao": None}, o, 1,
         ["ответ `POST", "без `Access-Control-Allow-Origin`"])

    case("ACAO указывает на чужой origin — не считается", {"status": 204, "acao": "https://evil.example"},
         {"status": 200, "acao": "https://evil.example"}, o, 1, ["без `Access-Control-Allow-Origin`"])

    case("ACAO ровно наш origin — норма", {"status": 200, "acao": o}, {"status": 200, "acao": o}, o, 0,
         ["нарушено: 0"])

    case("preflight не 2xx, но ACAO есть — всё равно нарушение", {"status": 500, "acao": "*"},
         {"status": 200, "acao": "*"}, o, 1, ["ждали 2xx"])

    total += 1
    line = receipt_line({"preflight": 204, "preflight_acao": "*", "post": 200, "post_acao": "*"}, [])
    if "preflight 204" in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_cors_entry --selftest: {ok}/{total} "
          f"(preflight без 2xx/ACAO или POST без ACAO = rc=1: браузер режет ответ при живом сервере)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
