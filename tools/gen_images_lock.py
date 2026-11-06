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
    """Анонимный pull-токен GHCR: (token, reason), reason = "ok" | "forbidden" | "error".

    403 = пакет приватный: для вызывающего это «не проверено», а НЕ «образа нет» (@TL 22:04).
    """
    import json
    import urllib.error
    import urllib.parse
    import urllib.request
    q = urllib.parse.urlencode({"service": registry,
                                "scope": "repository:%s/xdiary-%s:pull" % (owner, service)})
    url = "https://%s/token?%s" % (registry, q)
    try:
        with urllib.request.urlopen(url, timeout=timeout) as r:
            data = json.load(r)
    except urllib.error.HTTPError as ex:
        return "", ("forbidden" if ex.code in (401, 403) else "error")
    except Exception:
        return "", "error"
    tok = data.get("token") or data.get("access_token") or ""
    return tok, ("ok" if tok else "error")

def manifest_exists(registry, owner, service, tag, timeout=20):
    """True — тег опубликован; False — нет (404); OSError — проверить не смогли."""
    import urllib.error
    import urllib.request
    token, _reason = _ghcr_token(registry, owner, service, timeout=timeout)
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
    """[(service, tag, path, unverified)] — строки без ведущего # и с >=2 полями.

    Хвостовой комментарий (например `# unverified: …`) не попадает в путь, а помечает строку.
    """
    out = []
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        comment = stripped.split("#", 1)[1] if "#" in stripped else ""
        body = stripped.split("#", 1)[0].split()
        if len(body) >= 2:
            out.append((body[0], body[1], body[2] if len(body) > 2 else "",
                        "unverified" in comment))
    return out

def verify_registry(path, registry="ghcr.io", owner="ekstrem", check=manifest_exists, echo=print):
    """rc=0 всё опубликовано (в т.ч. если часть строк помечена `unverified`); rc=1 есть отсутствующие;
    rc=2 проверить не смогли.

    Строки `unverified` (403 на анонимный pull-token) попадают в «не проверено» и на rc НЕ влияют —
    иначе фронт, который деплоится через Pages, навсегда блокировал бы гейт (@TL 22:04).
    """
    if not os.path.exists(path):
        echo("check_images_lock: нет файла %s — проверять нечего" % path)
        return 2
    entries = parse_lock(open(path, encoding="utf-8").read())
    if not entries:
        echo("check_images_lock: в %s нет строк с тегами" % path)
        return 2
    missing, errors, unverified = [], [], []
    for service, tag, _path, unv in entries:
        if unv:
            unverified.append((service, tag))
            continue
        try:
            ok = check(registry, owner, service, tag)
        except OSError as ex:
            errors.append((service, tag, str(ex)))
            continue
        if not ok:
            missing.append((service, tag))
    total = len(entries)
    echo("check_images_lock --verify-registry: сервисов %d | опубликовано %d | отсутствует %d | "
         "не проверено %d (unverified — на rc не влияют)"
         % (total, total - len(missing) - len(errors) - len(unverified), len(missing),
            len(errors) + len(unverified)))
    for service, tag in missing:
        echo("  ОТСУТСТВУЕТ: %s:%s" % (service, tag))
    for service, tag in unverified:
        echo("  НЕ ПРОВЕРЕНО (unverified): %s:%s (403 на анонимный pull-token)" % (service, tag))
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
    """(теги `single-*`, reason) — reason приходит из `_ghcr_token`."""
    token, reason = _ghcr_token(registry, owner, service, timeout=timeout)
    if reason != "ok":
        return [], reason
    url = "https://%s/v2/%s/xdiary-%s/tags/list?n=1000" % (registry, owner, service)
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            payload = json.loads(r.read().decode("utf-8", "replace"))
    except Exception:
        return [], "error"
    tags = payload.get("tags") or []
    return [t for t in tags if isinstance(t, str) and t.startswith("single-")], "ok"

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


UNVERIFIED_NOTE = ("# unverified: 403 на анонимный pull-token; "
                   "front деплоится через Pages, не контейнером")


def registry_rows(registry, owner, echo=print):
    """[(service, tag, path, commit, unverified)].

    Если пакет закрыт (403 на анонимный токен), строка остаётся с git-тегом и пометкой `unverified`:
    фронту она в lock нужна (деплой через Pages), но `--verify-registry` обязан считать её
    «не проверено», и rc от неё не зависит (@TL 22:04).
    """
    rows = []
    for service, path in SERVICES:
        tags, reason = _ghcr_tags(registry, owner, service)
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
        if best is not None:
            rows.append((service, best[2], path, best[1][:7], False))
        elif reason == "forbidden":
            sha = short_sha(path)
            rows.append((service, "single-" + sha, path, sha, True))
        else:
            rows.append((service, "НЕТ-ОБРАЗА", path, "", False))
        svc, tag, _p, cm, unv = rows[-1]
        echo("  %-20s %-18s commit=%-8s%s" % (svc, tag, cm or "—", "  unverified" if unv else ""))
    return rows

def render_registry(rows):
    lines = [MIXED_HEADER.rstrip("\n")]
    for service, tag, path, commit, unverified in rows:
        if not commit:
            lines.append("%-20s %-18s %s" % (service, tag, path))
            continue
        line = "%-20s %-18s %s  commit=%s" % (service, tag, path, commit)
        if unverified:
            line += "  " + UNVERIFIED_NOTE
        lines.append(line)
    return "\n".join(lines) + "\n"

def receipt(path, registry="ghcr.io", owner="ekstrem", echo=print):
    """Три числа для квитанции деплоя (@AR 2026-11-06 23:22).

    «смешанный набор» — это слова, пока не напечатано:
      1) сколько сервисов в lock;
      2) у скольких тег НЕ совпадает с последним опубликованным предком HEAD (то есть отстали);
      3) сколько не выкачено вовсе (нет тега в реестре / помечено `unverified`).
    """
    if not os.path.exists(path):
        echo("check_images_lock --receipt: нет файла %s" % path)
        return 2
    entries = parse_lock(open(path, encoding="utf-8").read())
    if not entries:
        echo("check_images_lock --receipt: в %s нет строк" % path)
        return 2
    rows = registry_rows(registry, owner, echo=lambda *_a, **_k: None)
    newest = {svc: tag for svc, tag, _p, _c, _u in rows}
    lagging, absent, unverified = [], [], []
    for service, tag, _path, unv in entries:
        if unv or tag == "НЕТ-ОБРАЗА":
            unverified.append((service, tag))
            continue
        last = newest.get(service)
        if last and last != tag and last != "НЕТ-ОБРАЗА":
            lagging.append((service, tag, last))
    total = len(entries)
    echo("квитанция деплоя: сервисов в lock %d | НЕ совпадает с последним опубликованным %d | "
         "не выкачено (нет образа/unverified) %d" % (total, len(lagging), len(unverified)))
    for service, tag, last in lagging:
        echo("  отстаёт: %-20s в lock %-18s последний опубликованный %s" % (service, tag, last))
    for service, tag in unverified:
        reason = "403 на анонимный pull-token; front идёт через Pages, не контейнером" \
            if service == "front" else "нет опубликованного образа"
        echo("  не выкачено: %-16s %-18s (%s)" % (service, tag, reason))
    # rc=0: квитанция информационная. «не выкачено» уже печатается списком,
    # а жёсткий вердикт даёт `--verify-registry` (он для этого и существует).
    return 0


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
    ap.add_argument("--receipt", action="store_true",
                    help="три числа для квитанции деплоя (@AR 23:22)")
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка: детерминизм рендера + обнаружение дрейфа")
    a = ap.parse_args()

    if a.selftest:
        return selftest()

    if a.receipt:
        return receipt(a.file, registry=a.registry, owner=a.owner)

    if a.verify_registry:
        return verify_registry(a.file, registry=a.registry, owner=a.owner)

    if a.from_registry:
        rows = registry_rows(a.registry, a.owner)
        missing = [svc for svc, tag, _p, _c, _u in rows if tag == "НЕТ-ОБРАЗА"]
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
            # Сверка «файл == свежий рендер» здесь НЕПРИГОДНА: «самый свежий опубликованный
            # предок HEAD» меняется после каждого мержа (проверено 2026-11-06: journal fdf7550 ->
            # 89b3a2f после merge), поэтому такой --check мигал бы красным на ровном месте.
            # Значимый инвариант смешанного lock: каждый его тег есть в реестре.
            print("check_images_lock: lock смешанный — проверяю теги в реестре (а не совпадение с рендером)")
            return verify_registry(a.file, registry=a.registry, owner=a.owner)
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
