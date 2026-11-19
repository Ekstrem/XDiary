#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_api_calls.py — линт: строковые вызовы `/api/v1/...` в коде существуют в api-index.

Зачем (AR 2026-11-19 14:31, дефект 502 на `bff/analytics-summary`): несуществующий путь
`/api/v1/journal` (у Journal есть только `/api/v1/journal/records`) дошёл до `main`,
потому что ни один гейт не сверял СТРОКИ ВЫЗОВОВ с реестром операций:
  - компилятор путь не видит (это литерал),
  - `check_apim_auth` проверяет auth, а не существование,
  - `check_api_index`/`gen_openapi` сверяют **спеку**, а не `HttpClient`-вызовы.
Результат: 404 на апстриме → `HttpRequestException` → 502 наружу, при зелёных гейтах.

Что делает: сканирует `6-msa/**/*.cs` на литералы вида `/api/v1/...` (в т.ч. внутри
интерполяций `$"{base}/api/v1/x"`) и сверяет с `path:` из `04-architecture/api-index.yaml`.
Шаблоны `{id}` нормализуются в `{}` с обеих сторон.

Почему без ложных срабатываний по построению:
  - пути с параметрами (`{id}`, `{**catch-all}`) нормализуются;
  - комментарии (`//`, `/* */`, `#`) и строки в тестах (`*Tests`, `9-tests`) не сканируются;
  - allowlist с причиной — только точечно, а не «папка целиком».

Коды: 0 — все вызовы существуют; 1 — есть неизвестные пути; 2 — индекс не прочитан.
"""
from __future__ import annotations

import argparse
import os
import re
import sys

API_INDEX = os.path.join("04-architecture", "api-index.yaml")
SCAN_ROOT = "6-msa"
# Литерал пути: начинается с /api/v1/ и содержит только допустимые символы.
CALL_RX = re.compile(r"/api/v1/[A-Za-z0-9_\-/{}.]*")
PATH_LINE_RX = re.compile(r"^\s*path:\s*[\"']([^\"']+)[\"']", re.M)
# Строки-«источники», которые не являются вызовами (докблоки, примеры в комментариях).
SKIP_LINE_RX = re.compile(r"(^\s*//|^\s*\*|^\s*/\*|^\s*#)")
# Папки/файлы, где литерал — часть теста, а не продукт.
SKIP_PATH_PARTS = ("/tests/", "Tests/", "Test/", "Migrations/", "/obj/", "/bin/")

# Точечные исключения: путь -> причина. Пусто = ничего не скрываем.
ALLOW = {
    # Пример: "/api/v1/example": "внешний контракт третьей стороны (см. ADR-0XX)",
}


def norm(path: str) -> str:
    """`/api/v1/x/{id}` -> `/api/v1/x/{}`; `**`-catch-all тоже в `{}`."""
    return re.sub(r"\{[^}]*\}", "{}", path.rstrip("/") or "/")


def read_index(root: str) -> set[str]:
    p = os.path.join(root, API_INDEX)
    with open(p, encoding="utf-8") as fh:
        text = fh.read()
    return {norm(m.group(1)) for m in PATH_LINE_RX.finditer(text)}


def scan_file(path: str) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            for i, line in enumerate(fh, 1):
                if SKIP_LINE_RX.match(line):
                    continue
                for m in CALL_RX.finditer(line):
                    lit = m.group(0)
                    # отбрасываем «пустой» хвост и одиночный префикс
                    if norm(lit) == "/api/v1":
                        continue
                    out.append((i, lit))
    except OSError as exc:  # нечитаемый файл — это не «чисто»
        print("  ! не читается %s: %s" % (path, exc), file=sys.stderr)
    return out


def iter_cs(root: str):
    base = os.path.join(root, SCAN_ROOT)
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin", "node_modules")]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            full = os.path.join(dirpath, name)
            rel = "/" + os.path.relpath(full, root).replace("\\", "/")
            if any(part in rel for part in SKIP_PATH_PARTS):
                continue
            yield full, rel


def run(root: str, verbose: bool = False) -> int:
    try:
        known = read_index(root)
    except OSError as exc:
        print("check_api_calls: не прочитан %s: %s" % (API_INDEX, exc), file=sys.stderr)
        return 2
    if not known:
        print("check_api_calls: в %s не найдено ни одного `path:`" % API_INDEX, file=sys.stderr)
        return 2

    bad: list[str] = []
    total = 0
    for full, rel in iter_cs(root):
        for lineno, lit in scan_file(full):
            total += 1
            if lit in ALLOW or norm(lit) in known:
                continue
            bad.append("  %s:%d  %s" % (rel.lstrip("/"), lineno, lit))
    if verbose:
        print("проверено вызовов: %d, путей в индексе: %d" % (total, len(known)))
    if not bad:
        print("check_api_calls: OK — все %d вызовов есть в api-index" % total)
        return 0
    print("check_api_calls: НАЙДЕНЫ вызовы путей, которых нет в %s:" % API_INDEX)
    for line in sorted(set(bad)):
        print(line)
    print("ИТОГ: неизвестных путей %d (правка: либо код под индекс, либо индекс под код — @AR)" % len(set(bad)))
    return 1


def selftest() -> int:
    """Самопроверка нормализации и детекции — без файловой системы."""
    ok = True

    def check(cond: bool, name: str) -> None:
        nonlocal ok
        print("  [%s] %s" % ("OK " if cond else "FAIL", name))
        ok = ok and cond

    check(norm("/api/v1/journal/{id}") == "/api/v1/journal/{}", "нормализация {id}")
    check(norm("/api/v1/catalog/") == "/api/v1/catalog", "хвостовой слэш")
    check(norm("/api/v1") == "/api/v1", "префикс не ломается")
    line = 'await GetStringAsync(c, $"{base}/api/v1/journal", auth, ct);'
    got = [m.group(0) for m in CALL_RX.finditer(line)]
    check(got == ["/api/v1/journal"], "литерал внутри интерполяции найден")
    known = {norm("/api/v1/journal/records")}
    check(norm("/api/v1/journal") not in known, "несуществующий путь отличим")
    check(norm("/api/v1/journal/records") in known, "существующий путь принимается")
    print("selftest: %s" % ("OK" if ok else "FAIL"))
    return 0 if ok else 1


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description="Линт: вызовы /api/v1/... существуют в api-index.yaml")
    ap.add_argument("--root", default=os.getcwd(), help="корень репозитория")
    ap.add_argument("--selftest", action="store_true", help="проверить сам линт")
    ap.add_argument("--verbose", action="store_true", help="печатать счётчики")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()
    return run(args.root, args.verbose)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
