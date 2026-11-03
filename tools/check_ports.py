#!/usr/bin/env python3
"""check_ports.py — гейт «внешняя поверхность = только шлюз» (ADR-026, @AR 2026-11-13 03:30).

Почему именно это, а не «кто дотянется изнутри сети»: список «кто может дотянуться»
всегда одинаков (все контейнеры сети `xdiary`), он не меняется и ничего не блокирует —
это документация, а не контроль. Меняться может **публикация порта на хост**: любая
`ports:` у сервиса кроме шлюза = реальная экспозиция снаружи хоста, то есть утечка
поверхности, которую должен закрывать шлюз.

Правило: в `7-deploy/podman-compose.yml` объявление `ports:` допустимо **только у**
`api-gateway` и ровно **один раз**. Иначе rc=1 с именем сервиса.

    python tools/check_ports.py            # по репозиторию
    python tools/check_ports.py --file f   # другой compose (для негативного контроля)
    python tools/check_ports.py --selftest
"""
import argparse
import os
import re
import sys

COMPOSE = "7-deploy/podman-compose.yml"
ALLOWED = "api-gateway"
SERVICE = re.compile(r"^  ([a-zA-Z0-9_.-]+):\s*$")
PORTS = re.compile(r"^\s{4,}ports:\s*$|^\s{4,}ports:\s*\S")


def scan(text):
    """[(сервис, строка, значение)] — каждое объявление ports: с привязкой к сервису."""
    cur, out = None, []
    for i, line in enumerate(text.splitlines(), 1):
        m = SERVICE.match(line)
        if m:
            cur = m.group(1)
            continue
        if PORTS.match(line):
            out.append((cur, i, line.strip()))
    return out


def selftest():
    ok = 0
    cases = [
        ("  api-gateway:\n    ports:\n      - '8080:8080'\n", 1, True),
        ("  journal:\n    ports:\n      - '8081:8080'\n", 1, False),
        ("  journal:\n    image: x\n", 0, True),
        ("  api-gateway:\n    ports:\n      - '80:80'\n  partners:\n    ports:\n      - '81:81'\n", 2, False),
    ]
    for text, count, allowed in cases:
        found = scan(text)
        bad = [s for s, _, _ in found if s != ALLOWED]
        got_ok = (not bad) and len(found) <= 1
        if len(found) == count and got_ok == allowed:
            ok += 1
        else:
            print("  [FAIL] найдено %d (ждали %d), допустимо=%s (ждали %s)"
                  % (len(found), count, got_ok, allowed))
    print("check_ports --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--file", default=None)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    path = a.file or os.path.join(root, COMPOSE)
    if not os.path.exists(path):
        print("::error::нет %s — гейт поверхности не может быть зелёным без compose" % COMPOSE)
        return 1

    found = scan(open(path, encoding="utf-8").read())
    bad = [(s, n, v) for s, n, v in found if s != ALLOWED]
    for service, line, value in bad:
        print("::error::%s (стр.%d) публикует порт: %s — внешняя поверхность только у шлюза "
              "(ADR-026)" % (service or '?', line, value))
    if len(found) > 1 and not bad:
        print("::error::объявлений ports: %d — допустимо ровно одно, у %s" % (len(found), ALLOWED))
        bad = [("?", 0, "count")]

    names = sorted({s for s, _, _ in found})
    print("check_ports: объявлений ports: %d, сервисы с портами: %s, нарушений %d"
          % (len(found), ", ".join(names) or "—", len(bad)))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
