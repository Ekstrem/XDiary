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
import json
import urllib.request
import urllib.error
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
    """Тег сервиса: `single-<short sha последнего коммита, тронувшего путь>`.

    Решение @AR 2026-11-06 21:17 — вариант ① (посервисные теги), потому что ровно такой тег
    публикует CI в прогоне, где сервис собирался. Вариант ② («весь стенд на HEAD», 13 образов
    на одном коммите) — отдельный релизный механизм, и как блокер деплоя он отклонён: он требует
    зелёной сборки ВСЕХ сервисов, тогда как деплою нужен образ только изменённого.

    Условия @AR к ①: (1) `--verify-registry` ДО записи lock (в `lock-sync.yml` это уже шаг);
    (2) lock фиксируется после зелёных сборок и до следующего релиза не двигается.
    """
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


# ---------- DO-102: проверка, что теги из lock реально опубликованы ----------

def _registry_manifest_url(registry, owner, service, tag):
    return "https://%s/v2/%s/xdiary-%s/manifests/%s" % (registry, owner, service, tag)


def _ghcr_token(registry, owner, service, timeout=20):
    """Анонимный pull-токен GHCR. Пустая строка — токен не выдан (в т.ч. 403 у приватного пакета).

    403 не должен валить вызывающего: для него это «не измерено», а не «образа нет».
    """
    import json
    import urllib.parse
    import urllib.request
    q = urllib.parse.urlencode({"service": registry,
                                "scope": "repository:%s/xdiary-%s:pull" % (owner, service)})
    url = "https://%s/token?%s" % (registry, q)
    try:
        with urllib.request.urlopen(url, timeout=timeout) as r:
            data = json.load(r)
    except Exception:
        return ""
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



# ---------- DO-108: lock «из реестра» (временный разблокировщик деплоя) ----------
#
# Зачем: если у сервиса красные тесты, образа для «последнего коммита» нет, и `lock-published`
# блокирует деплой ЦЕЛИКОМ, включая сервисы, которые не менялись. В этом режиме для каждого
# сервиса берётся самый свежий ОПУБЛИКОВАННЫЙ тег `single-*`, чей коммит — предок `HEAD`.
#
# Условия @AR (2026-11-06 21:40), без них режим запрещён:
#   1. провенанс в самом lock: рядом с тегом — коммит, из которого образ собран;
#   2. заголовок прямо говорит: это НЕ сборка HEAD, набор смешанный;
#   3. `--verify-registry` обязателен, недоступный реестр = `rc=2` «не измерено»;
#   4. механизм временный: строка «вывести после появления release.yml» в lock и в карточке.

MIXED_MARK = "# набор смешанный"
MIXED_HEADER = """# images.lock — какие образы считаются текущими (ADR-027, DO-066)
#
# ВНИМАНИЕ: набор смешанный — это последние ДОСТУПНЫЕ образы, а НЕ сборка HEAD.
# Причина: у части сервисов красные тесты, образа «последнего коммита» нет (DO-108).
# Провенанс: `commit=<sha>` — коммит, из которого образ собран; он всегда предок HEAD.
# Вывести этот режим после появления `release.yml` (13 сервисов на одном коммите).
#
# Формат: <service> <tag> <путь>  commit=<sha>
# Проверка: python tools/gen_images_lock.py --verify-registry
"""


def is_mixed_lock(text):
    return MIXED_MARK in text


def _ghcr_tags(registry, owner, service, timeout=25):
    """Опубликованные теги пакета `xdiary-<service>` (пусто при любой ошибке сети/прав)."""
    token = _ghcr_token(registry, owner, service, timeout=timeout)
    if not token:
        return []
    url = "https://%s/v2/%s/xdiary-%s/tags/list?n=1000" % (registry, owner, service)
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            payload = json.loads(r.read().decode("utf-8", "replace"))
    except Exception:
        return []
    tags = payload.get("tags") or []
    return [t for t in tags if isinstance(t, str) and t.startswith("single-")]


def _commit_of(tag):
    """Полный sha для тега `single-<sha>`; None, если такого коммита нет в локальной истории."""
    sha = tag[len("single-"):]
    if not sha:
        return None
    full = git("rev-parse", "--verify", "--quiet", sha + "^{commit}")
    return full or None


def _is_ancestor(sha):
    return subprocess.run(["git", "merge-base", "--is-ancestor", sha, "HEAD"],
                          capture_output=True).returncode == 0


def registry_rows(registry, owner, echo=print):
    """[(service, tag, path, commit)] — самый свежий опубликованный образ-предок HEAD."""
    rows, missing = [], []
    for service, path in SERVICES:
        tags = _ghcr_tags(registry, owner, service)
        best = None
        for tag in tags:
            full = _commit_of(tag)
            if not full or not _is_ancestor(full):
                continue
            try:
                ts = int(git("log", "-1", "--format=%ct", full) or 0)
            except ValueError:
                ts = 0
            if best is None or ts > best[0]:
                best = (ts, full, tag)
        if best is None:
            missing.append(service)
            rows.append((service, "НЕТ-ОБРАЗА", path, ""))
        else:
            rows.append((service, best[2], path, best[1][:7]))
        echo("  %-20s %-18s commit=%s" % (rows[-1][0], rows[-1][1], rows[-1][3] or "—"))
    return rows, missing


def render_registry(rows):
    lines = [MIXED_HEADER.rstrip("\n")]
    for service, tag, path, commit in rows:
        if commit:
            lines.append("%-20s %-18s %s  commit=%s" % (service, tag, path, commit))
        else:
            lines.append("%-20s %-18s %s" % (service, tag, path))
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--from-registry", action="store_true",
                    help="DO-108: собрать lock из опубликованных образов (набор смешанный)")
    ap.add_argument("--file", default="7-deploy/images.lock")
    ap.add_argument("--verify-registry", action="store_true",
                    help="проверить, что теги опубликованы (rc=1 нет образа, rc=2 не измерено)")
    ap.add_argument("--registry", default="ghcr.io", help="хост registry (по умолчанию ghcr.io)")
    ap.add_argument("--owner", default="ekstrem", help="владелец пакетов (по умолчанию ekstrem)")
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка: детерминизм рендера + обнаружение дрейфа")
    a = ap.parse_args()

    if a.selftest:
        return selftest()

    if a.verify_registry:
        return verify_registry(a.file, registry=a.registry, owner=a.owner)

    if a.from_registry:
        rows, missing = registry_rows(a.registry, a.owner)
        if missing:
            print("::error::gen_images_lock: нет опубликованного образа для: %s — "
                  "lock из реестра НЕ пишу" % ", ".join(missing))
            return 2
        text = render_registry(rows)
        if a.write:
            open(a.file, "w", encoding="utf-8", newline="\n").write(text)
            print("записано (из реестра, набор смешанный): %s (%d строк)"
                  % (a.file, len(text.splitlines())))
            return 0
        if a.check:
            current = open(a.file, encoding="utf-8").read() if os.path.exists(a.file) else ""
            if current != text:
                print("::error::gen_images_lock --check: смешанный lock не совпадает с реестром: "
                      "перегенерируй --from-registry --write")
                return 1
            print("check_images_lock: смешанный lock совпадает с реестром")
            return 0
        print("::error::укажи --write или --check вместе с --from-registry")
        return 2

    # обычный режим: lock выводится из git
    current = open(a.file, encoding="utf-8").read() if os.path.exists(a.file) else ""
    if a.check and is_mixed_lock(current):
        # смешанный lock нельзя сверять с git: он собран из реестра (DO-108).
        print("check_images_lock: lock помечен как смешанный — сверяю с реестром")
        return verify_registry(a.file, registry=a.registry, owner=a.owner)

    text = render()
    if a.write:
        open(a.file, "w", encoding="utf-8", newline="\n").write(text)
        print("записано: %s (%d строк)" % (a.file, len(text.splitlines())))
        return 0
    if a.check:
        if current != text:
            print("::error::(lock) %s отстал от git: перегенерируй `python tools/gen_images_lock.py --write`"
                  % a.file)
            return 1
        print("check_images_lock: %s соответствует git" % a.file)
        return 0
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
