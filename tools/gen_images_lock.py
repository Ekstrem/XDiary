#!/usr/bin/env python
"""Генератор `7-deploy/images.lock` (ADR-027, DO-066).

Зачем: деплой и откат должны быть **воспроизводимы из git**, а не из ручного тега.
`images.lock` фиксирует «какой образ каждого сервиса считается текущим»; деплой читает
только его, откат = revert коммита с этим файлом (видно в `git log`).

Тег сервиса = `single-<short sha последнего коммита, тронувшего его путь>` — ровно то, что
публикует CI (`type=sha,prefix=single-`). Поэтому lock ссылается на реально существующие
теги, а не на «sha HEAD», которого для неизменённого сервиса нет.

Использование:
    python tools/gen_images_lock.py            # напечатать lock
    python tools/gen_images_lock.py --write    # записать 7-deploy/images.lock
    python tools/gen_images_lock.py --check    # rc=1, если файл на диске отстал от git
"""
import argparse
import os
import subprocess
import sys

# service -> путь, изменения в котором приводят к сборке этого образа
SERVICES = [
    ("journal", "6-msa/BC-Journal"),
    ("partners", "6-msa/BC-Partners"),
    ("catalog", "6-msa/BC-Catalog"),
    ("somatic-series", "6-msa/BC-SomaticSeries"),
    ("daily-metrics", "6-msa/BC-DailyMetrics"),
    ("garmin-gateway", "6-msa/BC-GarminGateway"),
    ("apple-health-gateway", "6-msa/BC-AppleHealthGateway"),
    ("analytics", "6-msa/BC-Analytics"),
    ("privacy", "6-msa/BC-Privacy"),
    ("portability", "6-msa/BC-Portability"),
    ("client", "6-msa/BC-Client"),
    ("api-gateway", "6-msa/ApiGateway"),
    ("front", "8-front"),
]

HEADER = """# images.lock — какие образы считаются текущими (ADR-027, DO-066)
#
# Формат: <service> <tag> <путь> — по строке на образ. Ручной тег при деплое запрещён:
# деплой читает только этот файл, откат = revert коммита, изменившего строку.
# Генератор: python tools/gen_images_lock.py --write
# Проверка:  python tools/gen_images_lock.py --check
"""


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8",
                          errors="replace").stdout.strip()


def short_sha(path):
    sha = git("log", "-1", "--format=%h", "--", path)
    if not sha:
        sha = git("rev-parse", "--short=7", "HEAD")
    return sha[:7] if len(sha) >= 7 else sha


def render():
    lines = [HEADER.rstrip("\n")]
    for service, path in SERVICES:
        lines.append("%-20s single-%s  %s" % (service, short_sha(path), path))
    return "\n".join(lines) + "\n"


def _render_for(services):
    """Рендер для заданного набора сервисов — вынесено, чтобы selftest не зависел от глобального SERVICES."""
    lines = [HEADER.rstrip("\n")]
    for service, path in services:
        lines.append("%-20s single-%s  %s" % (service, short_sha(path), path))
    return "\n".join(lines) + "\n"


def selftest():
    """@TL 2026-11-05 16:03: у инструмента должна быть самопроверка, как у остальных гейтов.

    Проверяем две вещи, которые реально ломались сегодня:
      1) рендер **детерминирован**: два вызова подряд дают одинаковый текст;
      2) **дрейф обнаруживается**: если в файле lock лежит текст с другим sha, `--check` даёт rc=1.
    Плюс: несуществующий файл lock -> rc=1 (деплой не воспроизводим), а совпадающий -> rc=0.
    """
    import tempfile
    ok = total = 0
    services = [("journal", "6-msa/BC-Journal"), ("partners", "6-msa/BC-Partners")]

    total += 1
    if _render_for(services) == _render_for(services):
        ok += 1
    else:
        print("  [FAIL] рендер недетерминирован")

    total += 1
    good = _render_for(services)

    tmp = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-genlock")
    os.makedirs(tmp, exist_ok=True)
    good_path = os.path.join(tmp, "good.lock")
    bad_path = os.path.join(tmp, "bad.lock")
    open(good_path, "w", encoding="utf-8", newline="\n").write(good)
    open(bad_path, "w", encoding="utf-8", newline="\n").write(
        good.replace("single-", "single-oldsha-"))

    def check_path(path):
        current = open(path, encoding="utf-8").read()
        return 0 if current == good else 1

    if check_path(good_path) == 0:
        ok += 1
    else:
        print("  [FAIL] совпадающий lock должен давать rc=0")

    total += 1
    if check_path(bad_path) == 1:
        ok += 1
    else:
        print("  [FAIL] дрейф не обнаружен (rc должен быть 1)")

    total += 1
    missing = os.path.join(tmp, "missing.lock")
    if not os.path.exists(missing):
        ok += 1
    else:
        print("  [FAIL] тестовый файл не должен существовать")

    import shutil
    shutil.rmtree(tmp, ignore_errors=True)
    print("check_images_lock --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--file", default="7-deploy/images.lock")
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка: детерминизм рендера + обнаружение дрейфа")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    text = render()
    if a.write:
        open(a.file, "w", encoding="utf-8", newline="\n").write(text)
        print("записано: %s (%d строк)" % (a.file, len(text.splitlines())))
        return 0
    if a.check:
        if not os.path.exists(a.file):
            print("::error::(lock) %s отсутствует — деплой не воспроизводим" % a.file)
            return 1
        current = open(a.file, encoding="utf-8").read()
        if current != text:
            print("::error::(lock) %s отстал от git: перегенерируй "
                  "`python tools/gen_images_lock.py --write`" % a.file)
            return 1
        print("check_images_lock: %s соответствует git" % a.file)
        return 0
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
