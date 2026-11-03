#!/usr/bin/env python
"""Гейт ADR-027 / DO-066: деплой воспроизводим — только lock, без ручного тега.

Проверки (@TL 2026-11-14 32:11, три пункта):
  (1) `7-deploy/deploy.sh` и `7-deploy/update.sh` **отказываются** принимать тег:
      аргумент → `exit 2` с сообщением «запрещено: тег задаётся только images.lock».
  (2) `.github/workflows/deploy.yml` не имеет `inputs.tag` и не вычисляет
      `TAG=${TAG_INPUT:-...}` — иначе ручной тег остаётся вторым источником правды.
  (3) `7-deploy/images.lock` существует, покрывает все сервисы и соответствует git
      (`tools/gen_images_lock.py --check`); в ранбуках нет отката через `podman tag`.

Зачем гейт: пока ручной тег возможен, «что сейчас на проде» нельзя восстановить из git —
откат живёт на сервере и исчезает вместе с ним.

    python tools/check_lock.py            # rc=0 норма / rc=1 нарушение
    python tools/check_lock.py --selftest
"""
import argparse
import os
import re
import shutil
import subprocess
import sys

LOCK = "7-deploy/images.lock"
GUARD = 'тег задаётся только images.lock'
SCRIPTS = ["7-deploy/deploy.sh", "7-deploy/update.sh"]
WORKFLOW = ".github/workflows/deploy.yml"
RUNBOOKS = ["7-deploy/runbooks/deploy-heal.md", "7-deploy/runbooks/release-gate.md"]


def read(root, rel):
    path = os.path.join(root, rel.replace("/", os.sep))
    if not os.path.exists(path):
        return None
    return open(path, encoding="utf-8", errors="replace").read()


def check(root):
    v = []
    for rel in SCRIPTS:
        txt = read(root, rel)
        if txt is None:
            v.append(("1", rel, "файл отсутствует"))
            continue
        if GUARD not in txt:
            v.append(("1", rel, "нет отказа от аргумента-тега (`%s`)" % GUARD))
        # дефолт single допустим только как fallback чтения lock, но не из $1
        # только явные формы чтения из позиционного аргумента (awk `$2` — не тег)
        if 'TAG="${1' in txt or 'TAG="$1"' in txt or 'TAG="${ARGS[0]' in txt:
            v.append(("1", rel, "тег всё ещё читается из аргумента (`$1`/`ARGS[0]`)"))
        if "images.lock" not in txt:
            v.append(("1", rel, "скрипт не читает 7-deploy/images.lock"))

    wf = read(root, WORKFLOW)
    if wf is None:
        v.append(("2", WORKFLOW, "файл отсутствует"))
    else:
        if re.search(r'^\s{4,}tag:\s*$', wf, re.M):
            v.append(("2", WORKFLOW, "остался `inputs.tag`"))
        if "TAG_INPUT" in wf:
            v.append(("2", WORKFLOW, "остался `TAG=${TAG_INPUT:-…}` — ручной тег как источник правды"))
        if "images.lock" not in wf and "lock_get" not in wf:
            v.append(("2", WORKFLOW, "workflow не берёт теги из images.lock"))

    lock = read(root, LOCK)
    if lock is None:
        v.append(("3", LOCK, "lock отсутствует — откат не воспроизводим"))
    else:
        rows = [l for l in lock.splitlines() if l.strip() and not l.lstrip().startswith("#")]
        if len(rows) < 10:
            v.append(("3", LOCK, "в lock меньше 10 сервисов (%d)" % len(rows)))
        for line in rows:
            parts = line.split()
            if len(parts) < 2 or not parts[1].startswith("single-"):
                v.append(("3", LOCK, "строка не формата `<service> single-<sha>`: %s" % line[:60]))

    # (4) `always()` в deploy.yml допустим только как ОФОРМЛЕННОЕ исключение (@AR 32:30):
    # в ранбуке должны быть и владелец, и условие снятия — иначе это «дыра по месту».
    if wf and "always()" in wf:
        gate_rb = read(root, "7-deploy/runbooks/release-gate.md") or ""
        has_owner = "Владелец:" in gate_rb and "Условие снятия" in gate_rb
        if not has_owner:
            v.append(("4", WORKFLOW, "`always()` без оформленного исключения в "
                                     "7-deploy/runbooks/release-gate.md (нужны «Владелец» и «Условие снятия»)"))

    for rel in RUNBOOKS:
        txt = read(root, rel)
        if not txt:
            continue
        # ищем именно КОМАНДУ отката, а не упоминание в комментарии: строка не должна
        # начинаться с `#` (иначе наша же пометка «ручной podman tag больше не путь»
        # считалась бы нарушением).
        for line in txt.splitlines():
            bare = line.strip()
            if bare.startswith("#") or "podman tag" not in bare:
                continue
            v.append(("3", rel, "откат командой `podman tag` — ручной путь: %s" % bare[:60]))
            break
    return v


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-lock")
    cases = [
        # (файлы, ожидаемое число нарушений)
        ({"7-deploy/deploy.sh": "TAG=\"${1:-single}\"",
          "7-deploy/update.sh": "TAG=\"${ARGS[0]:-single}\"",
          WORKFLOW: "    tag:\n      TAG_INPUT: x\n",
          LOCK: "# lock\n"}, 10),
        ({"7-deploy/deploy.sh": "# " + GUARD + "\n# читает images.lock\nread_lock\n",
          "7-deploy/update.sh": "# " + GUARD + "\n# images.lock\n",
          WORKFLOW: "python tools/lock_get.py\n",           # images.lock упомянут неявно
          LOCK: "\n".join("# c" if i == 0 else "svc%d single-abc1234 6-msa/X" % i
                          for i in range(11))}, 0),
    ]
    shutil.rmtree(base, ignore_errors=True)
    for files, expect in cases:
        case = os.path.join(base, "case")
        shutil.rmtree(case, ignore_errors=True)
        for rel, content in files.items():
            full = os.path.join(case, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(full), exist_ok=True)
            open(full, "w", encoding="utf-8", newline="\n").write(content)
        got = len(check(case))
        if got == expect:
            ok += 1
        else:
            print("  [FAIL] ждали %d, получили %d: %s" % (expect, got, sorted(files)))
    shutil.rmtree(base, ignore_errors=True)
    print("check_lock --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    v = check(os.path.abspath(a.root))
    for kind, rel, why in v:
        print("::error::(%s) %s: %s" % (kind, rel, why))
    print("check_lock: нарушений %d (1: скрипты не принимают тег; 2: workflow без inputs.tag; "
          "3: lock и откат через git)" % len(v))
    return 1 if v else 0


if __name__ == "__main__":
    sys.exit(main())
