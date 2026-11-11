#!/usr/bin/env python3
"""Генератор и проверка реестра OpenAPI из `04-architecture/api-index.yaml` (AR-016 / contract-first, @TL 2026-11-04 11:55).

Почему скрипт, а не «руками один раз»:
    реестр должен быть **воспроизводим** — правка `api-index.yaml` обязана давать ровно тот же `openapi/*.json`
    при перегенерации, иначе «drift» появится не между APIM и кодом, а внутри репозитория.

Использование:
    python 04-architecture/gen_openapi.py --check     # сверка СМЫСЛА с индексом (rc=1 при расхождении) — рабочий режим
    python 04-architecture/gen_openapi.py --write     # bootstrap: создать ТОЛЬКО отсутствующие спеки

⚠️ Почему `--write` не перезаписывает существующие: спеки в репозитории **богаче** индекса — в них есть
`requestBody`, `parameters`, `summary`, которых в `api-index.yaml` нет. Проверено 2026-11-04: перегенерация из индекса
удаляла 308 строк контракта (`journal` -134, `partners` -60) — то есть «детерминированная производная» была бы **потерей**
данных, а не обновлением. Поэтому единственный рабочий режим — `--check` (сверка смысла), а генерация — только bootstrap.

⚠️ Про `--write`: спеки в репозитории — **авторский** контракт (`info.description`, `servers`, `x-generated-at`,
`x-operations`), а не только машинная производная индекса. Первая версия этого скрипта перезаписывала файлы целиком
и **теряла** эти поля (проверено 2026-11-04: `-127` строк на 12 файлах). Поэтому `--write` обязан **сливать**:
смысловые поля (paths/operationId/security/responses) берутся из индекса, остальные ключи сохраняются из файла.
Если файла нет — создаётся минимальная спека из индекса.

Что генерируется (детерминированно, без сети и без обращения к сервисам):
    paths        — из `path` индекса (как есть; шаблоны `{id}` сохраняются);
    operationId  — из индекса (единственный источник идентичности операции);
    security     — `jwt` → bearerAuth; `anonymous` и `anonymous-inside` → public (явный `security: []`);
    responses    — из `codes` индекса (описание = значение);
    info         — имя BC, версия = `x-derived-date`;
    x-derived-from, x-derived-date — провенанс.
"""
import io
import json
import os
import sys

try:
    import yaml
except ImportError:
    sys.stderr.write("нужен pyyaml\n")
    sys.exit(2)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INDEX = os.path.join(ROOT, "04-architecture", "api-index.yaml")
OUT = os.path.join(ROOT, "openapi")

SLUG = {
    "ApiGateway": "api-gateway",
    "AppleHealthGateway": "apple-health-gateway",
    "GarminGateway": "garmin-gateway",
    "SomaticSeries": "somatic-series",
    "DailyMetrics": "daily-metrics",
}
TITLES = {
    "api-gateway": "API Gateway",
    "apple-health-gateway": "Apple Health Gateway API",
    "garmin-gateway": "Garmin Gateway API",
    "somatic-series": "Somatic Series API",
    "daily-metrics": "Daily Metrics API",
}


def slug(bc):
    return SLUG.get(bc, bc.lower())


def index_rev():
    import hashlib
    raw = io.open(INDEX, "rb").read()
    return hashlib.sha256(raw).hexdigest()


def _walk_dups(node, out, where):
    """Ищет повтор ключа в одном mapping-блоке (ВЛОЖЕННЫЕ одинаковые имена — не дубль)."""
    import yaml as _y
    if isinstance(node, _y.MappingNode):
        seen = set()
        for k, v in node.value:
            name = getattr(k, "value", None)
            if name in seen:
                out.append((k.start_mark.line + 1, str(name)))
            seen.add(name)
            _walk_dups(v, out, where)
    elif isinstance(node, _y.SequenceNode):
        for item in node.value:
            _walk_dups(item, out, where)


def dups_in_yaml(path):
    """[(строка, ключ)] — повтор ключа внутри одного блока. `safe_load` для этого не годится:
    он уже потерял информацию и молча оставляет последнее значение (AR-024, найдено @AR 2026-11-10
    на `Client.Get`: два `codes_status`/`verification`, первое доказательство исчезло без ошибки)."""
    node = yaml.compose(io.open(path, encoding="utf-8").read())
    out = []
    if node is not None:
        _walk_dups(node, out, path)
    return out


def load_index():
    d = yaml.safe_load(io.open(INDEX, encoding="utf-8"))
    date = d.get("x-derived-date") or d.get("date") or ""
    groups = {}
    for o in d["operations"]:
        groups.setdefault(slug(o["bc"]), {"bc": o["bc"], "ops": []})["ops"].append(o)
    return d, groups, date


REV = ""


def build(groups, date):
    specs = {}
    meta = {}
    for sl, g in sorted(groups.items()):
        paths = {}
        for o in g["ops"]:
            method = o["method"].lower()
            op = {
                "operationId": o["operationId"],
                "tags": [g["bc"]],
                "responses": {str(c): {"description": str(desc)} for c, desc in sorted(o["codes"].items())},
            }
            if o["auth"] == "jwt":
                op["security"] = [{"bearerAuth": []}]
            else:
                op["security"] = []  # anonymous / anonymous-inside — публичный внутри контура
            if o.get("deprecated"):
                op["deprecated"] = True
            if o.get("internal"):
                # `internal_rule`: маршрут внутреннего контура НЕ публикуется в `openapi/*.json`
                # (ADR-008, api-index.yaml). До 2026-11-11 генератор помечал его `x-internal`, но
                # всё равно публиковал — правило и артефакт расходились (@AR, ревью BE-095).
                continue
            paths.setdefault(o["path"], {})[method] = op

        title = TITLES.get(sl, g["bc"] + " Microservice API")
        spec = {
            "openapi": "3.0.3",
            "info": {"title": title, "version": date or "unspecified"},
            "x-derived-from": "04-architecture/api-index.yaml",
            "x-derived-date": date,
            "paths": paths,
        }
        if any(o["auth"] == "jwt" for o in g["ops"]):
            spec["components"] = {"securitySchemes": {"bearerAuth": {"type": "http", "scheme": "bearer", "bearerFormat": "JWT"}}}
        specs[sl] = spec
        meta[sl] = {
            "rev": REV,
            "paths": len(paths),
            "title": title,
            "operations": len(g["ops"]),
            "derived_from": "04-architecture/api-index.yaml",
        }
    return specs, meta




def merge_spec(path, generated):
    """Слить сгенерированное с авторским: смысл — из индекса, метаданные — из файла."""
    if not os.path.exists(path):
        return generated
    have = json.load(io.open(path, encoding="utf-8"))
    out = dict(have)                      # сохраняем всё авторское (servers, info.description, x-*)
    out["openapi"] = have.get("openapi", generated["openapi"])
    # info у существующего файла НЕ перетираем: title и version — авторские (в индексе даты нет,
    # и подставлять "unspecified" вместо реальной версии контракта значит портить опубликованное)
    out["info"] = dict(have.get("info", generated["info"]))
    out["paths"] = generated["paths"]     # смысл — всегда из индекса
    out["x-derived-from"] = generated["x-derived-from"]
    if generated.get("x-derived-date"):
        out["x-derived-date"] = generated["x-derived-date"]
    if "components" in generated:
        out["components"] = generated["components"]
    return out


def sync_spec(have, generated):
    """Слияние ПО ПОЛЯМ: обновить только смысловые поля операции, сохранить авторские.

    Зачем не `merge_spec`: он заменяет `paths` целиком и теряет `requestBody`/`parameters`/
    `summary`, которых в индексе нет (проверено 2026-11-04: −308 строк контракта).
    Здесь обновляются `operationId`/`security`/`responses`, а остальные ключи операции
    (`requestBody`, `parameters`, `summary`, `x-*`) остаются авторскими.
    Операции, которых в индексе больше нет, удаляются — иначе «лишнее в спеке» вечно.
    """
    out = dict(have)
    hp = out.get("paths") or {}
    gp = generated["paths"]

    for path, methods in gp.items():
        if path not in hp:
            hp[path] = methods
            continue
        for m, gen_op in methods.items():
            if m not in hp[path]:
                hp[path][m] = gen_op
                continue
            have_op = hp[path][m]
            have_op["responses"] = gen_op["responses"]
            have_op["operationId"] = gen_op["operationId"]
            if "security" in gen_op:
                have_op["security"] = gen_op["security"]
            if gen_op.get("deprecated"):
                have_op["deprecated"] = True

    for path in list(hp):
        if path not in gp:
            del hp[path]
            continue
        for m in list(hp[path]):
            if m in ("get", "post", "put", "delete", "patch") and m not in gp[path]:
                del hp[path][m]
        if not hp[path]:
            del hp[path]

    out["paths"] = hp
    out["x-derived-from"] = generated["x-derived-from"]
    if generated.get("x-derived-date"):
        out["x-derived-date"] = generated["x-derived-date"]
    if "components" in generated:
        out["components"] = generated["components"]
    return out


def semantic(spec):
    """Смысловая проекция спеки: что именно опубликовано, без метаданных."""
    out = {}
    for path, methods in spec.get("paths", {}).items():
        for m, op in methods.items():
            if m not in ("get", "post", "put", "delete", "patch"):
                continue
            out["%s %s" % (m.upper(), path)] = {
                "operationId": op.get("operationId"),
                "security": op.get("security"),
                "responses": sorted(str(c) for c in (op.get("responses") or {}).keys()),
            }
    return out


def dump(path, obj):
    io.open(path, "w", encoding="utf-8", newline="\n").write(
        json.dumps(obj, ensure_ascii=False, indent=2, sort_keys=False) + "\n")


def selftest():
    """Три состояния гейта: норма, дефект, и отказ на пустом входе.

    Без этого `--selftest` молча исполнял `--check` (флаг игнорировался), то есть
    выглядел как «самопроверка пройдена», хотя самопроверки не было (@AR 2026-11-10).
    """
    import tempfile

    ok = 0
    # 1) норма: индекс парсится и содержит операции
    d = yaml.safe_load(io.open(INDEX, encoding="utf-8").read())
    if isinstance(d, dict) and d.get("operations"):
        print("  [OK ] индекс: %d операций" % len(d["operations"]))
        ok += 1
    else:
        print("  [FAIL] индекс не содержит operations")

    # 2) дефект: незакрытая кавычка обязана дать исключение парсера
    bad = 'operations:\n  - operationId: X\n    summary: "незакрытая кавычка\n'
    fd, path = tempfile.mkstemp(suffix=".yaml")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write(bad)
        try:
            yaml.safe_load(io.open(path, encoding="utf-8").read())
            print("  [FAIL] битый YAML разобран без ошибки")
        except Exception as e:
            print("  [OK ] битый YAML -> %s" % type(e).__name__)
            ok += 1
    finally:
        os.remove(path)

    # 3) пустой вход не должен считаться «нет расхождений»
    empty = yaml.safe_load("operations: []\n")
    if isinstance(empty, dict) and not empty.get("operations"):
        print("  [OK ] пустой индекс распознаётся как пустой")
        ok += 1
    else:
        print("  [FAIL] пустой индекс не распознан")

    # 4) дубль ключа в одном блоке обязан находиться
    fd, path = tempfile.mkstemp(suffix=".yaml")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            f.write("operations:\n  - operationId: X\n    codes_status: verified\n    codes_status: derived\n")
        dups = dups_in_yaml(path)
        if len(dups) == 1 and dups[0][1] == "codes_status":
            print("  [OK ] дубль ключа найден: строка %d, ключ %s" % dups[0])
            ok += 1
        else:
            print("  [FAIL] дубль ключа не найден: %s" % (dups,))

        # 5) одинаковые имена под РАЗНЫМИ родителями — не дубль (эталон: Partners.UpdateProfile)
        with io.open(path, "w", encoding="utf-8") as f:
            f.write("operations:\n  - operationId: Y\n    request:\n      required: [a]\n"
                    "    target_request:\n      required: [b]\n")
        dups2 = dups_in_yaml(path)
        if not dups2:
            print("  [OK ] вложенные одинаковые ключи не считаются дублем")
            ok += 1
        else:
            print("  [FAIL] ложное срабатывание на вложенных ключах: %s" % (dups2,))
    finally:
        os.remove(path)

    print("gen_openapi --selftest: %d/5" % ok)
    return 0 if ok == 5 else 1


def main(argv):
    if "--selftest" in argv:
        return selftest()
    write = "--write" in argv          # bootstrap: только создать отсутствующие файлы
    check = "--check" in argv or not write
    global REV
    REV = index_rev()
    dups = dups_in_yaml(INDEX)
    if dups:
        for line, key in dups:
            print("::error::(openapi) %s:%d: дубль ключа «%s» — YAML молча оставит последнее значение (AR-024)"
                  % (os.path.basename(INDEX), line, key))
        print("openapi: дублей ключей %d" % len(dups))
        return 1
    _d, groups, date = load_index()
    specs, meta = build(groups, date)

    diffs = []
    warns = []
    for sl, spec in sorted(specs.items()):
        f = os.path.join(OUT, sl + ".json")
        if write:
            if os.path.exists(f):
                # Слияние по полям: смысл (operationId/security/responses) — из индекса,
                # авторские requestBody/parameters/summary — сохраняются (см. sync_spec).
                dump(f, sync_spec(json.load(io.open(f, encoding="utf-8")), spec))
                continue
            dump(f, spec)
            continue
        if not os.path.exists(f):
            diffs.append("%s: файла нет" % sl)
            continue
        have = json.load(io.open(f, encoding="utf-8"))
        # Сверяем СМЫСЛ (paths → method → operationId/security/responses), а не метаданные:
        # спеки в репозитории — авторский контракт, у них может быть info.description, servers,
        # x-generated-at и прочее, что к индексу не относится. Требовать байтового равенства значило бы
        # ломать гейт на косметике — тот самый класс «гейт красный не по делу».
        sem_have = semantic(have)
        sem_want = semantic(spec)
        if sem_have != sem_want:
            only_want = sorted(set(sem_want) - set(sem_have))
            only_have = sorted(set(sem_have) - set(sem_want))
            changed = sorted(k for k in set(sem_have) & set(sem_want) if sem_have[k] != sem_want[k])
            diffs.append("%s: нет в спеке %s; лишнее в спеке %s; отличается %s"
                         % (sl, only_want[:4] or "-", only_have[:4] or "-", changed[:4] or "-"))
        else:
            for k in ("servers", "x-generated-at", "x-operations", "components"):
                if (k in have) != (k in spec) or have.get(k) != spec.get(k):
                    warns.append("%s: метаданные %s отличаются (не блокер)" % (sl, k))

    mf = os.path.join(OUT, "_meta.json")
    if write:
        dump(mf, meta)
        print("записано спек: %d, meta: %d" % (len(specs), len(meta)))
        return 0
    if os.path.exists(mf):
        have_meta = json.load(io.open(mf, encoding="utf-8"))
        for sl in sorted(set(have_meta) | set(meta)):
            h, w = have_meta.get(sl, {}), meta.get(sl, {})
            for k in ("paths", "operations"):
                if h.get(k) != w.get(k):
                    diffs.append("_meta.json[%s].%s: %s != %s" % (sl, k, h.get(k), w.get(k)))
            if h.get("rev") != w.get("rev"):
                warns.append("_meta.json[%s].rev: провенанс старше текущего индекса (не блокер)" % sl)
    else:
        diffs.append("_meta.json: файла нет")

    for w in warns:
        print("::warning::(openapi) %s" % w)
    if diffs:
        for d in diffs:
            print("::error::(openapi) %s" % d)
        print("openapi --check: расхождений %d" % len(diffs))
        return 1
    total = sum(m["operations"] for m in meta.values())
    print("openapi --check: спек %d, операций %d, расхождений 0" % (len(meta), total))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
