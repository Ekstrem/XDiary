#!/usr/bin/env python3
"""check_backup_freshness.py — гейт «бэкап вчерашний и полный» (@AR 2026-11-05 15:25).

Зачем: 2026-11-05 найдено, что бэкапы **молча обрезались** шесть дней:
`podman exec … pg_dumpall | gzip > file` терял вывод, а `rc` конвейера приходил от `gzip` = 0,
поэтому cron писал `done`. Неполный артефакт опаснее отсутствующего — он проходит проверку
и молча теряет данные. Этот гейт проверяет **последствие** (файл полный и свежий), а не факт
существования файла.

Коды возврата (по @AR):
  * файла нет                      → `rc=2` (НЕ ИЗМЕРЕНО: проверять нечего)
  * дамп обрезан (`CREATE DATABASE`
    меньше, чем баз в кластере)    → `rc=1` (нарушение, с числами)
  * файл старше `--max-age-hours`  → `rc=1` (бэкап «есть», но вчерашний)
  * всё в норме                    → `rc=0`

`--expected-dbs N` можно не передавать: тогда берётся число из самого дампа и печатается,
а сравнение с кластером делает вызывающая сторона (CI/ранбук) через `--expected-dbs`.
"""
import argparse
import glob
import gzip
import os
import shutil
import re
import subprocess
import sys
import tempfile
import time

PATTERN = "xdiary-all-*.sql.gz"
DB_RE = re.compile(r"^CREATE DATABASE (\S+)")


def newest(directory):
    files = sorted(glob.glob(os.path.join(directory, PATTERN)),
                   key=lambda p: os.path.getmtime(p), reverse=True)
    return files[0] if files else None


def databases_in_dump(path):
    """Число `CREATE DATABASE` в дампе; читаем gzip потоково, без распаковки на диск."""
    n = 0
    names = []
    try:
        with gzip.open(path, "rb") as fh:
            for raw in fh:
                line = raw.decode("utf-8", "replace")
                m = DB_RE.match(line)
                if m:
                    n += 1
                    names.append(m.group(1))
    except (OSError, EOFError) as exc:
        return None, None, "не читается как gzip: %s" % exc
    return n, names, None


def verdict(path, expected_dbs, max_age_hours, now=None):
    """(rc, сообщение). Отдельно от CLI, чтобы selftest проверял логику, а не файлы."""
    now = time.time() if now is None else now
    if not path or not os.path.exists(path):
        return 2, "НЕ ИЗМЕРЕНО: файла бэкапа нет (%s) — проверять нечего" % PATTERN

    age_h = (now - os.path.getmtime(path)) / 3600.0
    n, names, err = databases_in_dump(path)
    if err:
        return 1, "НАРУШЕНИЕ: дамп не читается (%s): %s" % (os.path.basename(path), err)
    if n == 0:
        return 1, "НАРУШЕНИЕ: в дампе 0 баз (%s) — обрезан" % os.path.basename(path)

    if expected_dbs is not None and n != expected_dbs:
        return 1, ("НАРУШЕНИЕ: дамп неполный — CREATE DATABASE %d, а баз в кластере %d (%s)"
                   % (n, expected_dbs, os.path.basename(path)))
    if age_h > max_age_hours:
        return 1, ("НАРУШЕНИЕ: бэкап устарел — %.1f ч (порог %g ч), файл %s"
                   % (age_h, max_age_hours, os.path.basename(path)))
    return 0, ("ОК: %s | баз: %d | возраст: %.1f ч | размер: %d байт"
               % (os.path.basename(path), n, age_h, os.path.getsize(path)))


def selftest():
    ok = total = 0
    # каталог внутри рабочего дерева: системный temp под sandbox бывает недоступен на reopen
    tmp = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-backup-gate")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp, exist_ok=True)

    def case(name, payload_lines, expected_dbs, max_age_hours, age_hours, want_rc, gz=True):
        nonlocal ok, total
        total += 1
        path = os.path.join(tmp, name)
        data = ("\n".join(payload_lines) + "\n").encode()
        if gz:
            with gzip.open(path, "wb") as fh:
                fh.write(data)
        else:
            with open(path, "wb") as fh:
                fh.write(data)
        mtime = time.time() - age_hours * 3600
        os.utime(path, (mtime, mtime))
        rc, msg = verdict(path, expected_dbs, max_age_hours)
        if rc == want_rc:
            ok += 1
        else:
            print("  [FAIL] %s: rc=%d, ждали %d | %s" % (name, rc, want_rc, msg))

    good = ["-- PostgreSQL database cluster dump --"] + \
           ["CREATE DATABASE %s;" % d for d in
            ("analytics", "catalog", "client", "journal", "partners", "privacy")]

    case("norm.sql.gz", good, 6, 24, 1.0, 0)
    case("truncated.sql.gz", good[:3], 6, 24, 1.0, 1)            # сегодняшний дефект: 2 из 6
    case("empty.sql.gz", ["-- dump"], 6, 24, 1.0, 1)             # 0 баз
    case("stale.sql.gz", good, 6, 24, 30.0, 1)                   # старше порога
    case("no-expected.sql.gz", good, None, 24, 1.0, 0)           # порог по числу не задан
    case("not-gzip.sql.gz", good, 6, 24, 1.0, 1, gz=False)       # битый контейнер

    total += 1
    rc, msg = verdict(os.path.join(tmp, "нет-такого.sql.gz"), 6, 24)
    if rc == 2:
        ok += 1
    else:
        print("  [FAIL] отсутствующий файл: rc=%d, ждали 2" % rc)

    # --- @TL 2026-11-05 15:38: гейт печатает ЧИСЛА, значит selftest обязан проверять ТЕКСТ.
    # Подкласс: rc верный, а текст врёт (у нас уже был «граница None» в check_adr).
    def text_case(label, mutate_check):
        nonlocal ok, total
        total += 1
        if mutate_check():
            ok += 1
        else:
            print("  [FAIL] текст: %s" % label)

    good_path = os.path.join(tmp, "text-good.sql.gz")
    with gzip.open(good_path, "wb") as fh:
        fh.write(("\n".join(good) + "\n").encode())
    part_path = os.path.join(tmp, "text-part.sql.gz")
    with gzip.open(part_path, "wb") as fh:
        fh.write(("\n".join(good[:3]) + "\n").encode())

    def t_numbers():
        rc, msg = verdict(part_path, 6, 24)
        need = ("CREATE DATABASE 2" in msg) and ("кластере 6" in msg)
        no_none = "None" not in msg
        if not need or not no_none:
            print("      текст обрезанного: %r" % msg)
        return rc == 1 and need and no_none

    def t_missing():
        rc, msg = verdict(os.path.join(tmp, "нет.sql.gz"), 6, 24)
        okay = ("НЕ ИЗМЕРЕНО" in msg) and ("None" not in msg)
        if not okay:
            print("      текст отсутствующего: %r" % msg)
        return rc == 2 and okay

    def t_stale():
        old_path = os.path.join(tmp, "text-old.sql.gz")
        with gzip.open(old_path, "wb") as fh:
            fh.write(("\n".join(good) + "\n").encode())
        mtime = time.time() - 30 * 3600
        os.utime(old_path, (mtime, mtime))
        rc, msg = verdict(old_path, 6, 24)
        okay = ("устарел" in msg) and ("30.0" in msg) and ("None" not in msg)
        if not okay:
            print("      текст устаревшего: %r" % msg)
        return rc == 1 and okay

    def t_norm_text():
        rc, msg = verdict(good_path, 6, 24)
        okay = ("баз: 6" in msg) and ("None" not in msg)
        if not okay:
            print("      текст нормы: %r" % msg)
        return rc == 0 and okay

    text_case("обрезанный дамп: числа есть, None нет", t_numbers)
    text_case("файла нет: НЕ ИЗМЕРЕНО без None", t_missing)
    text_case("устарел: часы и порог в тексте", t_stale)
    text_case("норма: число баз в тексте", t_norm_text)

    total += 1
    rc, msg = verdict(None, 6, 24)
    if rc == 2:
        ok += 1
    else:
        print("  [FAIL] путь None: rc=%d, ждали 2" % rc)

    shutil.rmtree(tmp, ignore_errors=True)
    print("check_backup_freshness --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="/home/deploy/backups",
                    help="каталог с xdiary-all-*.sql.gz")
    ap.add_argument("--expected-dbs", type=int, default=None,
                    help="сколько баз в кластере (если не задано — только печатаем число)")
    ap.add_argument("--max-age-hours", type=float, default=24.0)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()

    if a.selftest:
        return selftest()

    path = newest(a.dir)
    rc, msg = verdict(path, a.expected_dbs, a.max_age_hours)
    print("check_backup_freshness: %s" % msg)
    if rc:
        print("::error::check_backup_freshness: %s" % msg)
    try:
        total_files = len(glob.glob(os.path.join(a.dir, PATTERN)))
    except OSError:
        total_files = 0
    print("  файлов в каталоге: %d | порог возраста: %g ч | ожидаем баз: %s"
          % (total_files, a.max_age_hours,
             a.expected_dbs if a.expected_dbs is not None else "не задано"))
    return rc


if __name__ == "__main__":
    sys.exit(main())
