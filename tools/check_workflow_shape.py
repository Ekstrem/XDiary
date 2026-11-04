#!/usr/bin/env python
"""Гейт «workflow не сломан формой» (находка @DO 2026-11-04: `deploy.yml` был невалиден).

Что случилось: при удалении `inputs.tag` (DO-066) в `deploy.yml` остался **пустой** блок
`inputs:` под `workflow_dispatch:`. GitHub такой файл не принимает, и это видно не как
«упал шаг», а как прогон **без единого джоба**: `status=completed, conclusion=failure,
created == updated, jobs: 0`. То есть прод-деплой был мёртв, а сообщение об ошибке не
указывало на файл.

Проверки:
  1) файл парсится как YAML;
  2) есть `on` и `jobs`, `jobs` непустой;
  3) нигде нет **пустых** `inputs:`/`env:`/`jobs:`/`steps:` (значение-`None` — та же ошибка);
  4) у каждого job есть `runs-on` или `uses`;
  5) у каждого job есть шаги (`steps` непустой) — иначе джоб «есть», но ничего не делает.

    python tools/check_workflow_shape.py
    python tools/check_workflow_shape.py --selftest
"""
import argparse
import os
import shutil
import sys

import yaml

WF_DIR = os.path.join(".github", "workflows")
EMPTY_KEYS = ("inputs", "env", "jobs", "steps", "with", "strategy", "concurrency")


def check_file(path):
    bad = []
    try:
        doc = yaml.safe_load(open(path, encoding="utf-8"))
    except yaml.YAMLError as exc:
        return ["YAML не парсится: %s" % str(exc).splitlines()[0][:120]]
    if not isinstance(doc, dict):
        return ["корень не mapping"]
    on = doc.get("on", doc.get(True))
    if on is None:
        bad.append("нет блока `on` (workflow не триггерится)")
    jobs = doc.get("jobs")
    if not isinstance(jobs, dict) or not jobs:
        bad.append("нет `jobs` или он пуст")
    else:
        for name, body in jobs.items():
            if not isinstance(body, dict):
                bad.append("job `%s` — не mapping" % name)
                continue
            if not (body.get("runs-on") or body.get("uses")):
                bad.append("job `%s` без `runs-on`/`uses`" % name)
            if body.get("steps") is not None and not body.get("steps"):
                bad.append("job `%s`: пустой `steps`" % name)
            if body.get("steps") is None and not body.get("uses"):
                bad.append("job `%s` без `steps`" % name)
    # пустые под-блоки (inputs:/env:/...) на ЛЮБОМ уровне — именно этот случай ломал deploy.yml
    def walk(node, trail):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in EMPTY_KEYS and v is None:
                    bad.append("пустой блок `%s:` (значение None) по пути %s" % (k, trail or "корень"))
                walk(v, "%s.%s" % (trail, k) if trail else str(k))
        elif isinstance(node, list):
            for i, v in enumerate(node):
                walk(v, "%s[%d]" % (trail, i))

    walk(doc, "")
    if isinstance(on, dict) and "workflow_dispatch" in on and on["workflow_dispatch"] is None:
        pass      # `workflow_dispatch:` без входа — валидно
    return bad


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-wfshape")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base)

    # дефект: пустой inputs под workflow_dispatch (как было в deploy.yml)
    bad_path = os.path.join(base, "bad.yml")
    open(bad_path, "w", encoding="utf-8").write(
        "name: x\non:\n  workflow_dispatch:\n    inputs:\njobs:\n  a:\n    runs-on: ubuntu-latest\n"
        "    steps:\n      - run: echo ok\n")
    got = check_file(bad_path)
    if any("inputs" in g for g in got):
        ok += 1
    else:
        print("  [FAIL] пустой inputs не пойман: %s" % got)

    # норма
    good_path = os.path.join(base, "good.yml")
    open(good_path, "w", encoding="utf-8").write(
        "name: x\non:\n  push:\n    branches: [main]\n  workflow_dispatch:\njobs:\n  a:\n"
        "    runs-on: ubuntu-latest\n    steps:\n      - run: echo ok\n")
    got = check_file(good_path)
    if not got:
        ok += 1
    else:
        print("  [FAIL] норма помечена как дефект: %s" % got)

    shutil.rmtree(base, ignore_errors=True)
    print("check_workflow_shape --selftest: %d/2" % ok)
    return 0 if ok == 2 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    wf_dir = os.path.join(root, WF_DIR)
    if not os.path.isdir(wf_dir):
        print("::warning::нет %s" % WF_DIR)
        return 0
    total = bad_files = 0
    for name in sorted(os.listdir(wf_dir)):
        if not name.endswith((".yml", ".yaml")):
            continue
        total += 1
        problems = check_file(os.path.join(wf_dir, name))
        if problems:
            bad_files += 1
            for p in problems:
                print("::error::(workflow) %s: %s" % (name, p))
    print("check_workflow_shape: workflow %d, сломанных %d" % (total, bad_files))
    return 1 if bad_files else 0


if __name__ == "__main__":
    sys.exit(main())
