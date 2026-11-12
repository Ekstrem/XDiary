#!/usr/bin/env python3
"""check_yaml_dups.py — дубль ключа в одном YAML-блоке (AR-024).

Зачем: YAML молча оставляет ПОСЛЕДНЕЕ значение при повторе ключа в одном mapping.
Файл при этом «валиден», а половина данных исчезает без ошибки — так в `api-index.yaml`
у `Client.Get` пропало доказательство (второй `codes_status`/`verification`).

`yaml.safe_load` для проверки НЕ годится: он уже потерял информацию. Работаем через
`yaml.compose()` и смотрим узлы `MappingNode` — у каждого ключа есть `start_mark.line`.

Коды: `0` — дублей нет; `1` — есть дубли (печатает `файл:строка: ключ`); `2` — не измерено
(нет PyYAML / нет файлов / 0 кейсов в самопроверке).
"""
import argparse
import glob
import os
import sys

DEFAULT_TARGETS = [
    "04-architecture/api-index.yaml",
    ".github/workflows/*.yml",
    ".github/workflows/*.yaml",
    "7-deploy/**/*.yml",
    "7-deploy/**/*.yaml",
]


def dups_in_yaml_text(text):
    """[(строка, ключ)] — повторы ключа В ОДНОМ mapping. Вложенные одноимённые не дубль."""
    import yaml
    node = yaml.compose(text)
    found = []

    def walk(n):
        if isinstance(n, yaml.MappingNode):
            seen = set()
            for key, value in n.value:
                name = getattr(key, "value", None)
                if isinstance(name, str):
                    if name in seen:
                        found.append((key.start_mark.line + 1, name))
                    else:
                        seen.add(name)
                walk(value)
        elif isinstance(n, yaml.SequenceNode):
            for item in n.value:
                walk(item)

    walk(node)
    return sorted(found)


def dups_in_yaml(path):
    with open(path, encoding="utf-8") as fh:
        return dups_in_yaml_text(fh.read())


def expand(targets):
    files = []
    for pat in targets:
        files.extend(glob.glob(pat, recursive=True))
    return sorted({f for f in files if os.path.isfile(f)})


def selftest():
    import shutil as _sh
    work = os.path.join(os.getcwd(), ".tmp-yaml-dups-selftest")
    _sh.rmtree(work, ignore_errors=True)
    os.makedirs(work, exist_ok=True)
    cases = []
    try:
        p = os.path.join(work, "dup.yaml")
        open(p, "w", encoding="utf-8").write(
            "Client.Get:\n  codes_status: [200]\n  codes_status: [401]\n")
        cases.append(("дубль ключа в одном блоке", len(dups_in_yaml(p)) == 1))

        p = os.path.join(work, "nested.yaml")
        open(p, "w", encoding="utf-8").write(
            "request:\n  required: [a]\ntarget_request:\n  required: [b]\n")
        cases.append(("вложенные одноимённые под разными родителями — не дубль",
                      dups_in_yaml(p) == []))

        p = os.path.join(work, "nested_dup.yaml")
        open(p, "w", encoding="utf-8").write("a:\n  b:\n    k: 1\n    k: 2\n")
        cases.append(("дубль во вложенном блоке", len(dups_in_yaml(p)) == 1))

        p = os.path.join(work, "triple.yaml")
        open(p, "w", encoding="utf-8").write("k: 1\nk: 2\nk: 3\n")
        cases.append(("три повтора = 2 находки", len(dups_in_yaml(p)) == 2))

        p = os.path.join(work, "clean.yaml")
        open(p, "w", encoding="utf-8").write("a: 1\nb:\n  c: 2\n")
        cases.append(("чистый файл", dups_in_yaml(p) == []))
    finally:
        _sh.rmtree(work, ignore_errors=True)

    if not cases:
        print("::error::check_yaml_dups: 0 кейсов — самопроверка ничего не проверила")
        return 2
    ok = 0
    for name, passed in cases:
        print("  [%s] %s" % ("OK " if passed else "FAIL", name))
        ok += 1 if passed else 0
    print("check_yaml_dups --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="дубли ключей в YAML (AR-024)")
    ap.add_argument("--file", action="append", default=None,
                    help="файл или шаблон (можно несколько); по умолчанию — индекс + workflows + 7-deploy")
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)

    if a.selftest:
        return selftest()

    try:
        import yaml  # noqa: F401
    except ImportError:
        print("::error::check_yaml_dups: НЕ ИЗМЕРЕНО — нет PyYAML (pip install pyyaml)")
        return 2

    os.chdir(a.root)
    targets = a.file or DEFAULT_TARGETS
    files = expand(targets)
    if not files:
        print("::error::check_yaml_dups: НЕ ИЗМЕРЕНО — не нашёл файлов по целям %s" % targets)
        return 2

    bad = 0
    for f in files:
        try:
            found = dups_in_yaml(f)
        except Exception as ex:
            print("::warning::check_yaml_dups: %s не разобран (%s) — это отдельный гейт валидности"
                  % (f, type(ex).__name__))
            continue
        for line, name in found:
            print("::error::check_yaml_dups: %s:%d: дубль ключа «%s» — YAML молча оставит "
                  "последнее значение (AR-024)" % (f, line, name))
            bad += 1
    print("check_yaml_dups: проверено файлов %d | дублей %d" % (len(files), bad))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
