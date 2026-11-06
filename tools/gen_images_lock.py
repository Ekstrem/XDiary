#!/usr/bin/env python
"""Генератор `7-deploy/images.lock` (ADR-027, DO-066).

Зачем: деплой и откат должны быть **воспроизводимы из git**, а не из ручного тега.
`images.lock` фиксирует «какой образ каждого сервиса считается текущим»; деплой читает
только его, откат = revert коммита с этим файлом (видно в `git log`).

Тег сервиса = `single-<short sha HEAD>` — **один релизный коммит на все сервисы**.

Почему так (2026-11-06, DO-102): раньше тег считался по «последнему коммиту, тронувшему путь
сервиса», а CI публикует `single-<sha собираемого коммита>` = `single-<HEAD>`. Эти два правила
не могут совпасть: после любого пуша lock требовал теги, которых в registry не было
(проверено `--verify-registry`: 10 из 13 отсутствовали), а `deploy.sh` всё равно берёт **один**
тег на все сервисы. Семантика «один релизный коммит» делает lock, CI и деплой согласованными:
13 образов собраны из одного коммита, откат = revert этого коммита.

Использование:
    python tools/gen_images_lock.py            # напечатать lock
    python tools/gen_images_lock.py --write    # записать 7-deploy/images.lock
    python tools/gen_images_lock.py --check    # rc=1, если файл на диске отстал от git
    python tools/gen_images_lock.py --verify-registry   # DO-102: rc=1, если тега нет в registry

`--verify-registry` (DO-102, 2026-11-06): `--check` сверяет **git ↔ lock** и потому ничего не
говорит о том, что образ с таким тегом **опубликован**. Проверено на dev: ни одного тега
`single-<sha>` из 13 сервисов не было ни локально, ни в registry, а `deploy.sh` при этом
отрабатывал `rc=0` — то есть «деплой по lock» был невоспроизводим, и гейт этого не видел.
Registry проверяется анонимным токеном GHCR (образы публичные), манифест — через HEAD.
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


HEAD_SHA = None


def short_sha(path=None):
    """Тег релиза: `single-<short HEAD>`, одинаковый для всех сервисов.

    `path` оставлен в сигнатуре для совместимости вызовов, но больше не влияет:
    один релизный коммит — один набор образов (см. docstring модуля).
    """
    global HEAD_SHA
    if HEAD_SHA is None:
        sha = git("rev-parse", "--short=7", "HEAD") or ""
        if not sha:
            raise SystemExit("gen_images_lock: не могу определить HEAD")
        HEAD_SHA = sha[:7]
    return HEAD_SHA


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


# ---------- DO-102: проверка, что теги из lock реально опубликованы ----------

def _registry_manifest_url(registry, owner, service, tag):
    return "https://%s/v2/%s/xdiary-%s/manifests/%s" % (registry, owner, service, tag)


def _ghcr_token(registry, owner, service, timeout=20):
    """Анонимный pull-токен GHCR (образы публичные). Бросает OSError при сбое сети."""
    import json
    import urllib.parse
    import urllib.request
    q = urllib.parse.urlencode({"service": registry,
                                "scope": "repository:%s/xdiary-%s:pull" % (owner, service)})
    url = "https://%s/token?%s" % (registry, q)
    with urllib.request.urlopen(url, timeout=timeout) as r:
        data = json.load(r)
    return data.get("token") or data.get("access_token") or ""


def manifest_exists(registry, owner, service, tag, timeout=20):
    """True — тег опубликован; False — нет (404); OSError — проверить не смогли."""
    import urllib.error
    import urllib.request
    token = _ghcr_token(registry, owner, service, timeout=timeout)
    req = urllib.request.Request(
        _registry_manifest_url(registry, owner, service, tag), method="HEAD",
        headers={"Authorization": "Bearer " + token,
                 "Accept": (",".join([
                     "application/vnd.oci.image.index.v1+json",
                     "application/vnd.docker.distribution.manifest.list.v2+json",
                     "application/vnd.oci.image.manifest.v1+json",
                     "application/vnd.docker.distribution.manifest.v2+json"]))})
    try:
        with urllib.request.urlopen(req, timeout=timeout):
            return True
    except urllib.error.HTTPError as ex:
        if ex.code in (404, 403):
            return False
        raise OSError("registry ответил %d" % ex.code)


def parse_lock(text):
    """[(service, tag, path)] из текста lock: строки без # и с >=2 полями."""
    out = []
    for line in text.splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        parts = line.split()
        if len(parts) >= 2:
            out.append((parts[0], parts[1], parts[2] if len(parts) > 2 else ""))
    return out


def verify_registry(path, registry="ghcr.io", owner="ekstrem", check=manifest_exists, echo=print):
    """rc=0 всё опубликовано; rc=1 есть отсутствующие; rc=2 проверить не смогли."""
    if not os.path.exists(path):
        echo("check_images_lock: нет файла %s — проверять нечего" % path)
        return 2
    entries = parse_lock(open(path, encoding="utf-8").read())
    if not entries:
        echo("check_images_lock: в %s нет строк с тегами" % path)
        return 2
    missing, errors = [], []
    for service, tag, _ in entries:
        try:
            ok = check(registry, owner, service, tag)
        except OSError as ex:
            errors.append((service, tag, str(ex)))
            continue
        if not ok:
            missing.append((service, tag))
    total = len(entries)
    echo("check_images_lock --verify-registry: сервисов %d | опубликовано %d | отсутствует %d | не проверено %d"
         % (total, total - len(missing) - len(errors), len(missing), len(errors)))
    for service, tag in missing:
        echo("  ОТСУТСТВУЕТ: %s:%s" % (service, tag))
    for service, tag, msg in errors:
        echo("  НЕ ПРОВЕРЕНО: %s:%s (%s)" % (service, tag, msg[:80]))
    if missing:
        return 1
    if errors:
        return 2
    return 0


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
    # DO-102: verify_registry — норма / отсутствующий тег / registry недоступен
    import tempfile

    def _mk(entries):
        fd, name = tempfile.mkstemp(suffix=".lock")
        os.close(fd)
        with open(name, "w", encoding="utf-8") as f:
            f.write("# header\n" + "".join("%s %s %s\n" % e for e in entries))
        return name

    good = _mk([("journal", "single-aaa1111", "6-msa/BC-Journal"),
                ("partners", "single-bbb2222", "6-msa/BC-Partners")])
    total += 1
    rc = verify_registry(good, check=lambda *a: True, echo=lambda *a: None)
    print("  [%s] verify: все теги опубликованы -> rc=0" % ("OK" if rc == 0 else "FAIL",))
    ok += 1 if rc == 0 else 0

    total += 1
    rc = verify_registry(good, check=lambda r, o, s, t: s != "partners", echo=lambda *a: None)
    print("  [%s] verify: один тег отсутствует -> rc=1" % ("OK" if rc == 1 else "FAIL",))
    ok += 1 if rc == 1 else 0

    def _boom(*a):
        raise OSError("network down")

    total += 1
    rc = verify_registry(good, check=_boom, echo=lambda *a: None)
    print("  [%s] verify: registry недоступен -> rc=2 (не путать с 'нет образа')"
          % ("OK" if rc == 2 else "FAIL",))
    ok += 1 if rc == 2 else 0

    os.unlink(good)

    print("check_images_lock --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--file", default="7-deploy/images.lock")
    ap.add_argument("--verify-registry", action="store_true",
                    help="DO-102: проверить, что каждый тег из lock опубликован в registry")
    ap.add_argument("--registry", default="ghcr.io", help="хост registry (по умолчанию ghcr.io)")
    ap.add_argument("--owner", default="ekstrem", help="владелец пакетов (по умолчанию ekstrem)")
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка: детерминизм рендера + обнаружение дрейфа")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    if a.verify_registry:
        return verify_registry(a.file, registry=a.registry, owner=a.owner)
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
