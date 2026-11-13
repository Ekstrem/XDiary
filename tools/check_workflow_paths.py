#!/usr/bin/env python3
# КОПИЯ из LoopMaker/check_workflow_paths.py (источник правды там; правка — в обоих местах).
"""Guard: у тяжёлых workflow должен быть фильтр путей, иначе чат гоняет деплой/тесты.

Инцидент, из которого это выросло (XDiary, 2026-11-06): коммит сообщения в outbox
триггерил Pages/деплой/синк/тесты. Дорого и, хуже того, такой пуш отменял идущий
E2E (через `cancel-in-progress`).

Правило: если у workflow есть `on.push` **без** `paths` и **без** `paths-ignore` —
это ошибка. Исключение — лёгкие guard'ы, которым можно на каждый пуш; их список
берётся из `loop.config.json`:

    "ci": { "light_workflows": ["guard-paths.yml", "stats.yml", "chat-guards.yml"] }

Запуск:
    python tools/check_workflow_paths.py
    python tools/check_workflow_paths.py --config loop.config.json --root <dir>

Код возврата: 0 — все тяжёлые с фильтром, 1 — есть нарушения (или битый YAML).
Требуется PyYAML; без него — предупреждение и код 0 (чтобы не ломать чужой CI).
"""
import argparse
import glob
import json
import os
import shutil
import sys

try:
    import yaml
except ImportError:  # pragma: no cover
    yaml = None

CONFIG = "loop.config.json"
DEFAULT_LIGHT = ["guard-paths.yml", "stats.yml", "chat-guards.yml"]


def repo_root(arg=None):
    if arg:
        return os.path.abspath(arg)
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.dirname(here)


def load_config(root, path=None):
    path = path or os.path.join(root, CONFIG)
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def push_trigger(doc):
    """Секция `on.push` или None. PyYAML 1.1 парсит ключ `on` как True."""
    if not isinstance(doc, dict):
        return None
    on = doc.get("on", doc.get(True))
    if not isinstance(on, dict):
        return None
    return on.get("push")


def scan_workflows(wf_dir, light):
    """[(имя, причина)] — тяжёлые workflow без фильтра путей в on.push."""
    bad = []
    for path in sorted(glob.glob(os.path.join(wf_dir, "*.yml"))):
        name = os.path.basename(path)
        try:
            with open(path, encoding="utf-8") as f:
                doc = yaml.safe_load(f)
        except Exception as e:
            bad.append((name, "не парсится (%s)" % e))
            continue
        push = push_trigger(doc)
        if push is None:
            continue
        if isinstance(push, dict) and (push.get("paths") or push.get("paths-ignore")):
            continue
        if name in light:
            continue
        bad.append((name, "on.push без paths/paths-ignore"))
    return bad


def selftest():
    """Самопроверка гейта (AR-023: у этого гейта не было `--selftest`).

    Обе стороны: дефект (тяжёлый workflow без фильтра) ловится, норма (с фильтром,
    лёгкий из allow-list, dispatch-only) — нет. Без yaml честно rc=2 (норма @AR).
    """
    if yaml is None:
        print("::error::check_workflow_paths --selftest: PyYAML не установлен — НЕ ИЗМЕРЕНО (rc=2)",
              file=sys.stderr)
        return 2
    base = os.path.join(os.getcwd(), ".tmp-workflow-paths-selftest")
    shutil.rmtree(base, ignore_errors=True)
    wf = os.path.join(base, ".github", "workflows")
    os.makedirs(wf)

    def write(name, text):
        with open(os.path.join(wf, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)

    write("heavy-no-paths.yml", "on:\n  push:\n    branches: [main]\n")
    write("heavy-with-paths.yml",
          "on:\n  push:\n    branches: [main]\n    paths:\n      - 'src/**'\n")
    write("light-no-paths.yml", "on:\n  push:\n    branches: [main]\n")
    write("dispatch-only.yml", "on:\n  workflow_dispatch:\n")

    cases = []
    got = [n for n, _ in scan_workflows(wf, set())]
    cases.append(("дефект: тяжёлые без путей пойманы (оба)",
                  got == ["heavy-no-paths.yml", "light-no-paths.yml"]))
    got2 = [n for n, _ in scan_workflows(wf, {"light-no-paths.yml"})]
    cases.append(("лёгкий из allow-list не считается дефектом", got2 == ["heavy-no-paths.yml"]))
    got3 = [n for n, _ in scan_workflows(wf, {"heavy-no-paths.yml", "light-no-paths.yml"})]
    cases.append(("оба в allow-list -> пусто", got3 == []))
    cases.append(("workflow с paths не попадает", "heavy-with-paths.yml" not in got))
    cases.append(("dispatch-only не попадает", "dispatch-only.yml" not in got))

    shutil.rmtree(base, ignore_errors=True)
    passed = sum(1 for _, ok in cases if ok)
    for name, ok in cases:
        print("  [%s] %s" % ("OK " if ok else "FAIL", name))
    print("check_workflow_paths --selftest: %d/%d" % (passed, len(cases)))
    return 0 if passed == len(cases) else 1


def main():
    ap = argparse.ArgumentParser(description="Фильтры путей у workflow")
    ap.add_argument("--root", default=None)
    ap.add_argument("--config", default=None)
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка гейта (AR-023)")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    if yaml is None:
        # @TL 2026-11-13 01:13 (норма @AR «нет зависимости ⇒ rc=2»): «пропускаю и 0» — тихое
        # зелёное: ни один workflow не проверен, и отличить это от «всё в порядке» нельзя.
        # Возвращаем rc=2 (не измерено) — как в соглашении rc=0/1/2 (TE-108).
        print("::error::check_workflow_paths: PyYAML не установлен — НЕ ИЗМЕРЕНО (rc=2), "
              "гейт не проверил ни одного workflow", file=sys.stderr)
        return 2

    root = repo_root(args.root)
    cfg = load_config(root, args.config)
    light = set((cfg.get("ci") or {}).get("light_workflows") or DEFAULT_LIGHT)

    wf_dir = os.path.join(root, ".github", "workflows")
    if not os.path.isdir(wf_dir):
        print("check_workflow_paths: нет .github/workflows — пропускаю")
        return 0

    found = scan_workflows(wf_dir, light)
    bad = [n for n, _ in found]

    if bad:
        for n, why in found:
            print("  ! %s: %s — будет гонять тяжёлое "
                  "на любой коммит (в т.ч. сообщение в чат)" % (n, why), file=sys.stderr)
        print("check_workflow_paths: FAIL (%d)" % len(bad))
        return 1

    print("check_workflow_paths: OK (у всех тяжёлых workflow есть фильтр путей)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
