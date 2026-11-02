#!/usr/bin/env python3
"""Guard: workflow'ы обязаны иметь paths/paths-ignore, чтобы чат не гонял тяжёлые джобы.

Ловит регрессию «сообщение в чате → деплой/Pages/синк/тесты» (инцидент 2026-11-06):
если workflow с `on.push` без фильтра путей — fail. Исключение — лёгкие guard'ы,
которым можно триггериться на каждый пуш.

Запуск: python tools/check_workflow_paths.py
"""
import glob
import os
import sys

try:
    import yaml
except ImportError:  # pragma: no cover
    yaml = None

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WF_DIR = os.path.join(ROOT, ".github", "workflows")

# Лёгкие workflow'ы, которым разрешено триггериться на каждый пуш (без фильтра).
ALLOWLIST = {"guard-paths.yml"}


def _push_trigger(d):
    # PyYAML 1.1 парсит ключ `on` как boolean True.
    on = d.get("on") if isinstance(d, dict) else None
    if on is None and isinstance(d, dict):
        on = d.get(True)
    if not isinstance(on, dict):
        return None
    return on.get("push")


def main():
    if yaml is None:
        print("check_workflow_paths: PyYAML не установлен — пропускаю", file=sys.stderr)
        return 0

    bad = []
    for f in sorted(glob.glob(os.path.join(WF_DIR, "*.yml"))):
        name = os.path.basename(f)
        try:
            d = yaml.safe_load(open(f, encoding="utf-8"))
        except Exception as e:
            print(f"  ! {name}: не парсится ({e})", file=sys.stderr)
            bad.append(name)
            continue
        push = _push_trigger(d)
        if push is None:
            continue  # dispatch-only / нет push
        if isinstance(push, dict) and (push.get("paths") or push.get("paths-ignore")):
            continue  # фильтр есть
        if name in ALLOWLIST:
            continue  # разрешённый лёгкий guard
        bad.append(name)

    if bad:
        for n in bad:
            print(f"  ! {n}: on.push без paths/paths-ignore — триггерится на каждый пуш (в т.ч. чат)", file=sys.stderr)
        print(f"check_workflow_paths: FAIL ({len(bad)} workflow'ов без фильтра)")
        return 1

    print("check_workflow_paths: OK (у всех тяжёлых workflow есть paths/paths-ignore)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
