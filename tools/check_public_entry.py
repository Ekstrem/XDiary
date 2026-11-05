#!/usr/bin/env python
"""check_public_entry.py — внешняя проба публичного входа (DO-076, APIM в пути).

Зачем: с 2026-11-05 APIM стоит **в пути трафика**, то есть стал единой точкой отказа
публичного API, и автоматической деградации нет (зафиксировано в `adr-023`). Значит
нужно хотя бы **обнаружение**: ни один workflow до сих пор не проверял вход снаружи —
`/health` идёт мимо APIM, поэтому мёртвый gateway никто бы не заметил.

Проба выполняется **из интернета** (runner), это принципиально: замер с прода/дев идёт
tailnet-путём (резолв в `100.x` → Caddy `:443`) и не показывает, что видят пользователи.

Проверки:
  1. `/health` → 200 (идёт мимо APIM — контроль, что жив YARP);
  2. `/api/v1/catalog` → **401** и есть `x-gravitee-*` (APIM в пути и жив);
  3. несуществующий путь → **404** на проде (маршрутизация APIM работает; YARP дал бы 401).
   **Объявленное расхождение контуров (DO-090):** на dev вход идёт через YARP, поэтому
   несуществующий путь отдаёт **401**, а не 404. Ожидание — флаг `--unknown-expect {404,401}`
   (по умолчанию 404 = прод); для dev запускать с `401`, чтобы это было утверждением,
   а не случайностью.

    python tools/check_public_entry.py --base https://xdiary-vps1.taile0b6ee.ts.net
    python tools/check_public_entry.py --selftest

rc: 0 — вход жив и идёт через APIM; 1 — нарушение; 2 — не измерено (сеть/стенд недоступны).
"""
import argparse
import ssl
import sys
import urllib.error
import urllib.request

UA = 'xdiary-public-probe/1'
TIMEOUT = 15


def fetch(url):
    """Возвращает (код, заголовки-словарь) или (None, {}) при сетевой ошибке."""
    req = urllib.request.Request(url, headers={'User-Agent': UA})
    ctx = ssl.create_default_context()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=TIMEOUT) as r:
            return r.status, {k.lower(): v for k, v in r.headers.items()}
    except urllib.error.HTTPError as exc:
        return exc.code, {k.lower(): v for k, v in exc.headers.items()}
    except Exception:
        return None, {}


def classify(health, catalog, catalog_hdr):
    """Базовые проверки: (ok, fails[], unknown[]). Коды: int | None."""
    fails, unknown = [], []
    if health is None:
        unknown.append('/health не ответил (стенд/сеть)')
    elif health != 200:
        fails.append('/health: %s (ожидали 200)' % health)

    if catalog is None:
        unknown.append('/api/v1/catalog не ответил (стенд/сеть)')
    else:
        if catalog != 401:
            fails.append('/api/v1/catalog: %s (ожидали 401; 404 = APIM без маршрута, '
                         '502/503 = APIM/сервис мёртв)' % catalog)
        if not any(k.startswith('x-gravitee') for k in catalog_hdr):
            fails.append('/api/v1/catalog без X-Gravitee — трафик идёт МИМО APIM')
    return (not fails and not unknown), fails, unknown


def classify_full(health, catalog, catalog_hdr, unknown, unknown_expect=404):
    ok, fails, unknowns = classify(health, catalog, catalog_hdr)
    if unknown is None:
        unknowns.append('несуществующий путь не ответил (стенд/сеть)')
    elif unknown != unknown_expect:
        fails.append('несуществующий путь: %s (ожидали %s — %s)'
                     % (unknown, unknown_expect,
                        'прод: APIM' if unknown_expect == 404
                        else 'dev: YARP, объявленное расхождение контуров'))
    return (not fails and not unknowns), fails, unknowns


def selftest():
    ok = total = 0

    def case(args, want, label, unknown_expect=404):
        nonlocal ok, total
        total += 1
        good, _f, _u = classify_full(*args, unknown_expect=unknown_expect)
        if good == want:
            ok += 1
        else:
            print('  [FAIL] %s: ok=%s, ждали %s' % (label, good, want))

    G = {'x-gravitee-transaction-id': 'x'}
    case((200, 401, G, 404), True, 'норма: health 200, catalog 401+gravitee, unknown 404')
    case((200, 401, {}, 404), False, 'нет x-gravitee (трафик мимо APIM)')
    case((200, 401, G, 401), False, 'unknown 401 при ожидании 404 (вход в YARP, не в APIM)')
    # DO-090 (@TL 2026-11-05 14:14): dev отвечает 401 — объявленное расхождение, не дефект
    case((200, 401, G, 401), True, 'dev: unknown 401 при --unknown-expect 401', 401)
    case((200, 401, G, 404), False, 'dev с ожиданием 401, а пришёл 404', 401)
    case((200, 502, G, 404), False, 'catalog 502 (APIM мёртв)')
    case((200, 404, G, 404), False, 'catalog 404 (нет маршрута в APIM)')
    case((500, 401, G, 404), False, '/health не 200')
    case((None, None, {}, None), False, 'стенд недоступен — не засчитываем')

    print('check_public_entry --selftest: %d/%d' % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser(description='Внешняя проба публичного входа')
    ap.add_argument('--base', default='https://xdiary-vps1.taile0b6ee.ts.net')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--unknown-expect', type=int, choices=(401, 404), default=404,
                    help='ожидаемый код на несуществующий путь: 404 прод (APIM), 401 dev (YARP)')
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    base = a.base.rstrip('/')
    health, _ = fetch(base + '/health')
    catalog, hdr = fetch(base + '/api/v1/catalog')
    unknown, _ = fetch(base + '/api/v1/nonexistent-probe-xyz')

    print('  %-28s %s' % ('/health', health))
    print('  %-28s %s  x-gravitee=%d' % ('/api/v1/catalog', catalog,
                                        sum(1 for k in hdr if k.startswith('x-gravitee'))))
    print('  %-28s %s' % ('/api/v1/nonexistent-probe-xyz', unknown))

    good, fails, unknowns = classify_full(health, catalog, hdr, unknown)
    for f in fails:
        print('::error::public-entry: %s' % f)
    for u in unknowns:
        print('::warning::public-entry: не измерено — %s' % u)
    print('Публичный вход: %s' % ('ОК (через APIM)' if good else
                                  ('НЕ ИЗМЕРЕНО' if unknowns and not fails else 'НАРУШЕНИЕ')))
    return 0 if good else (2 if unknowns and not fails else 1)


if __name__ == '__main__':
    sys.exit(main())
