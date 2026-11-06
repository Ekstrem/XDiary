"""check_registry_tags.py — теги из `7-deploy/images.lock` должны существовать в реестре.

Зачем: `deploy-prod` блокируется шагом `lock-published`, который требует, чтобы **все** 13 образов из
`images.lock` были опубликованы. `images.lock` генерируется **из git** (`gen_images_lock.py`), поэтому он может
указывать на коммит, чьи образы никогда не публиковались (сборка красная → job `docker` не выполнился). Тогда
деплой стоит **целиком**, хотя 2 из 13 образов в реестре есть — и «почему прод не обновился» приходится выяснять
руками.

Эта проверка отвечает на вопрос числом: сколько из 13 тегов **реально существует** в GHCR, какие отсутствуют, и
какие недоступны анонимно (приватный пакет — это «не проверено», а не «нет»).

Коды: `0` — все теги найдены; `1` — есть отсутствующие; `2` — не измерено (реестр недоступен/нет lock).
"""

import argparse
import json
import pathlib
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

LOCK = "7-deploy/images.lock"
REGISTRY = "https://ghcr.io"
OWNER = "ekstrem"
LINE = re.compile(r"^([a-z0-9][a-z0-9-]*)\s+(single-[0-9a-f]{7})\s+\S+", re.I)


def parse_lock(text):
    """[(service, tag)] из строк вида `journal  single-2ba8f0c  6-msa/BC-Journal`."""
    rows = []
    for line in (text or "").splitlines():
        s = line.strip()
        if not s or s.startswith("#"):
            continue
        m = LINE.match(s)
        if m:
            rows.append((m.group(1), m.group(2)))
    return rows


def evaluate(rows, tags_by_service):
    """tags_by_service: {service: list[str] | None} (None = пакет недоступен анонимно)."""
    present, missing, unknown = [], [], []
    for svc, tag in rows:
        tags = tags_by_service.get(svc)
        if tags is None:
            unknown.append((svc, tag))
        elif tag in tags:
            present.append((svc, tag))
        else:
            missing.append((svc, tag))
    stats = {"total": len(rows), "present": len(present), "missing": len(missing),
             "unknown": len(unknown)}
    return present, missing, unknown, stats


def receipt(stats, missing_line=""):
    return (f"квитанция: образов в lock {stats['total']} | опубликовано {stats['present']} | "
            f"отсутствуют {stats['missing']} | не проверено (403) {stats['unknown']}"
            + (f" | нет: {missing_line}" if missing_line else ""))


def registry_tags(service, timeout=25):
    """Список тегов пакета; None — если реестр ответил 403/401 (приватный пакет, «не проверено»)."""
    scope = urllib.parse.quote(f"repository:{OWNER}/xdiary-{service}:pull")
    with urllib.request.urlopen(
        urllib.request.Request(f"{REGISTRY}/token?scope={scope}&service=ghcr.io",
                               headers={"User-Agent": "xdiary-registry"}), timeout=timeout) as r:
        token = json.loads(r.read())["token"]
    try:
        with urllib.request.urlopen(
            urllib.request.Request(f"{REGISTRY}/v2/{OWNER}/xdiary-{service}/tags/list?n=1000",
                                   headers={"Authorization": "Bearer " + token,
                                            "User-Agent": "xdiary-registry"}), timeout=timeout) as r:
            data = json.loads(r.read())
        return data.get("tags") or []
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            return None
        raise


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--lock", default=LOCK)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    p = pathlib.Path(a.lock)
    if not p.exists():
        print(f"::error::check_registry_tags: нет {a.lock} — не измерено (rc=2)")
        return 2
    rows = parse_lock(p.read_text(encoding="utf-8", errors="replace"))
    if not rows:
        print(f"::error::check_registry_tags: в {a.lock} не разобрано ни одной строки — не измерено (rc=2)")
        return 2

    tags_by_service, errors = {}, []
    for svc, _tag in rows:
        try:
            tags_by_service[svc] = registry_tags(svc)
        except Exception as e:  # noqa: BLE001
            errors.append(f"{svc}: {type(e).__name__}: {e}")
            tags_by_service[svc] = None
    if errors and all(v is None for v in tags_by_service.values()):
        print("::error::check_registry_tags: реестр недоступен целиком — не измерено (rc=2): "
              + "; ".join(errors[:3]))
        return 2

    present, missing, unknown, stats = evaluate(rows, tags_by_service)
    for svc, tag in missing:
        print(f"::warning::check_registry_tags: {svc} {tag} — тега нет в реестре "
              f"(образ не публиковался: сборка красная или `docker` = skipped)")
    for svc, tag in unknown:
        print(f"::notice::check_registry_tags: {svc} {tag} — пакет недоступен анонимно (403), это НЕ «нет»")
    print(receipt(stats, ", ".join(f"{s}:{t}" for s, t in missing)))
    return 1 if missing else 0


def selftest():
    ok = total = 0

    def case(name, rows, tags, want_rc, must=()):
        nonlocal ok, total
        total += 1
        present, missing, unknown, stats = evaluate(rows, tags)
        text = receipt(stats, ", ".join(f"{s}:{t}" for s, t in missing))
        rc = 1 if missing else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    lock = ("# коммент\n"
            "journal              single-2ba8f0c  6-msa/BC-Journal\n"
            "client               single-0dd2b57  6-msa/BC-Client\n"
            "front                single-b987513  8-front\n")
    rows = parse_lock(lock)
    total += 1
    if rows == [("journal", "single-2ba8f0c"), ("client", "single-0dd2b57"), ("front", "single-b987513")]:
        ok += 1
    else:
        print(f"  [FAIL] разбор lock: {rows}")

    case("все теги на месте", rows,
         {"journal": ["single-2ba8f0c"], "client": ["single-0dd2b57"], "front": ["single-b987513"]},
         0, ["опубликовано 3", "отсутствуют 0"])

    case("одного тега нет — красный", rows,
         {"journal": ["single-1111111"], "client": ["single-0dd2b57"], "front": ["single-b987513"]},
         1, ["отсутствуют 1", "journal:single-2ba8f0c"])

    case("403 на пакет — «не проверено», а не «нет»", rows,
         {"journal": ["single-2ba8f0c"], "client": ["single-0dd2b57"], "front": None},
         0, ["не проверено (403) 1"])

    total += 1
    line = receipt({"total": 13, "present": 2, "missing": 10, "unknown": 1})
    if "образов в lock 13" in line and "опубликовано 2" in line and "отсутствуют 10" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_registry_tags --selftest: {ok}/{total} "
          f"(тег из lock, которого нет в реестре = rc=1: деплой упирается в lock-published)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
