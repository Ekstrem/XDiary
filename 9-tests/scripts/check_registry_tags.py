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
import subprocess
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


# Сервисы, которых в GHCR не будет ПО АРХИТЕКТУРЕ: `front` публикуется в Pages, а не образом.
# Список совпадает с `gen_images_lock.py` (@DO `2026-11-11 16:17`), чтобы два инструмента не спорили.
NO_REGISTRY_SERVICES = {"front"}


def registry_rc(missing_services):
    """`rc` для режима `--available`: 0, если без образа только ожидаемые сервисы."""
    unexpected = [s for s in missing_services if s not in NO_REGISTRY_SERVICES]
    return 1 if unexpected else 0


def available_rc(errors, missing_services, allow_fallback=False):
    """`rc` для `--available`: отказ реестра — это «не измерено» (`2`), а не «всё хорошо».

    errors — список ошибок доступа (по одной на сервис). Если они есть, «доступное» считается по
    git-истории, а не по реестру; выдавать это за измерение нельзя: `deploy.yml` проверяет именно реестр
    (`lock-published`), и расхождение уже случалось (#1970 красный при локальном `rc=0`).
    `allow_fallback=True` — осознанное исключение для отладки: тогда считаем как раньше, но гейт об этом
    печатает предупреждение.
    """
    # `front` в GHCR не публикуется (Pages) — его 403 не «не измерено», это ожидаемое архитектурное
    # исключение (@DO 2026-11-11 16:17). Ошибки остальных сервисов = измерение не состоялось.
    real_errors = [e for e in errors if e.split(":", 1)[0].strip() not in NO_REGISTRY_SERVICES]
    if real_errors and not allow_fallback:
        return 2
    return registry_rc(missing_services)


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


def git_commit_dates(root=".", ref=None):
    """{short7: ts} по коммитам, достижимым из ref. Имя удалённой ветки различается между клонами
    (`origin/main` vs `gh/main`), поэтому пробуем по порядку, а не падаем на первом."""
    refs = [ref] if ref else ["origin/main", "gh/main", "HEAD"]
    for r in refs:
        if not r:
            continue
        out = subprocess.run(["git", "-C", root, "log", r, "--format=%h %ct"],
                             capture_output=True, text=True, encoding="utf-8", errors="replace")
        rows = {}
        for line in (out.stdout or "").splitlines():
            parts = line.split()
            if len(parts) == 2 and parts[1].isdigit():
                # git отдаёт `%h` переменной длины (в этом репозитории — 8 символов из-за коллизий),
                # а тег в реестре несёт 7-символьный префикс: кладём оба ключа, иначе «доступно 0».
                rows[parts[0]] = int(parts[1])
                rows[parts[0][:7]] = int(parts[1])
        if rows:
            return rows, r
    return {}, None


def available_by_service(rows, tags_by_service, dates):
    """Для каждого сервиса — самый свежий ОПУБЛИКОВАННЫЙ тег, чей коммит есть в истории origin/main."""
    result = {}
    for svc, _tag in rows:
        tags = tags_by_service.get(svc) or []
        cands = []
        for t in tags:
            m = re.match(r"single-([0-9a-f]{7,40})", t)
            if not m:
                continue
            key = m.group(1)[:7]
            if key in dates:
                cands.append((dates[key], t))
        cands.sort(reverse=True)
        result[svc] = cands[0][1] if cands else None
    return result


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--lock", default=LOCK)
    ap.add_argument("--available", action="store_true",
                    help="показать, что реестр реально может дать: самый свежий опубликованный тег "
                         "с коммитом из origin/main (то, что запишет gen_images_lock.py --from-registry)")
    ap.add_argument("--repo-root", default=".")
    ap.add_argument("--ref", help="ветка для истории (по умолчанию origin/main, затем gh/main, HEAD)")
    ap.add_argument("--allow-git-fallback", action="store_true",
                    help="разрешить считать по git-истории, когда реестр недоступен "
                         "(отладка; по умолчанию это НЕ ИЗМЕРЕНО, rc=2)")
    ap.add_argument("--simulate-registry-down", action="store_true",
                    help="проверка дефект-плеча: реестр «недоступен» целиком (ждём rc=2)")
    ap.add_argument("--simulate-registry-fail", action="append", default=[],
                    metavar="SERVICE",
                    help="проверка дефект-плеча: реестр «недоступен» для сервиса (можно "
                         "несколько раз)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.simulate_registry_down or a.simulate_registry_fail:
        real_fetch = registry_tags
        down_all = a.simulate_registry_down
        failing = set(a.simulate_registry_fail)

        def simulated_fetch(service, timeout=25):
            if down_all or service in failing:
                raise OSError("simulated: registry unreachable for %s" % service)
            return real_fetch(service, timeout)

        globals()["registry_tags"] = simulated_fetch
        print("::notice::check_registry_tags: СИМУЛЯЦИЯ отказа реестра (%s) — это проверка дефект-плеча"
              % ("все сервисы" if down_all else ", ".join(sorted(failing))))

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

    if a.available:
        real_errors = [e for e in errors if e.split(":", 1)[0].strip() not in NO_REGISTRY_SERVICES]
        if real_errors and not a.allow_git_fallback:
            print("::error::check_registry_tags: НЕ ИЗМЕРЕНО: реестр недоступен для %d сервис(ов): %s"
                  % (len(real_errors), "; ".join(real_errors[:3])))
            print("квитанция (НЕ ИЗМЕРЕНО): реестр недоступен | сервисов %d | ошибок %d"
                  % (len(rows), len(real_errors)))
            return 2
        if real_errors:
            print("::warning::check_registry_tags: реестр недоступен для %d сервис(ов) — считаю по "
                  "git-истории по флагу --allow-git-fallback (это НЕ измерение реестра)" % len(errors))
        dates, used_ref = git_commit_dates(a.repo_root, a.ref)
        if not dates:
            print("::error::check_registry_tags: не удалось прочитать историю git — не измерено (rc=2)")
            return 2
        print(f"::notice::check_registry_tags: история из {used_ref}")
        avail = available_by_service(rows, tags_by_service, dates)
        have = [s for s, t in avail.items() if t]
        for svc, tag in avail.items():
            lock_tag = dict(rows).get(svc)
            mark = "совпадает с lock" if tag == lock_tag else ("—" if tag else "НЕТ опубликованных предков")
            print(f"   {svc:22} последний доступный: {tag or '—':16} (в lock {lock_tag}) {mark}")
        missing_svcs = [svc for svc, tag in avail.items() if not tag]
        note = ""
        if missing_svcs == ["front"]:
            # @AR 23:22: 12/13 не должно читаться как недоделка — у фронта архитектурное отличие.
            note = " | 1 без образа: front — пакет 403 на анонимный pull-token, фронт идёт через Pages, а не GHCR"
        elif missing_svcs:
            note = f" | без образа: {', '.join(missing_svcs)}"
        print(f"квитанция (from-registry): сервисов {len(avail)} | доступно {len(have)} | "
              f"без образа {len(avail) - len(have)}{note}")
        return available_rc(errors, [svc for svc, tag in avail.items() if not tag],
                            allow_fallback=a.allow_git_fallback)

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

    # @DO `2026-11-11 16:17`: `front` в GHCR не публикуется по архитектуре (Pages). Режим `--available`
    # не должен из-за него возвращать 1, но остальные сервисы без образа остаются красными.
    total += 1
    if registry_rc(["front"]) == 0 and registry_rc(["journal"]) == 1 and registry_rc([]) == 0:
        ok += 1
    else:
        print("  [FAIL] registry_rc: front должен давать 0, journal — 1")

    # TE-106: три исхода `--available` — норма 0 / тега нет 1 / реестр недоступен 2
    total += 1
    if available_rc([], []) == 0 and available_rc([], ["journal"]) == 1 \
            and available_rc(["journal: HTTPError"], []) == 2 \
            and available_rc(["front: HTTPError 403"], []) == 0 \
            and available_rc(["journal: HTTPError"], [], allow_fallback=True) == 0:
        ok += 1
    else:
        print("  [FAIL] available_rc: ждали 0/1/2 и 0 при --allow-git-fallback, получили %s/%s/%s"
              % (available_rc([], []), available_rc([], ["journal"]), available_rc(["x"], [])))

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
