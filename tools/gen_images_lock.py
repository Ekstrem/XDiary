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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--file", default="7-deploy/images.lock")
    a = ap.parse_args()
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
