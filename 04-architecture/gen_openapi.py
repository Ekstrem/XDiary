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
                op["x-internal"] = True
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


def main(argv):
    write = "--write" in argv          # bootstrap: только создать отсутствующие файлы
    check = "--check" in argv or not write
    global REV
    REV = index_rev()
    _d, groups, date = load_index()
    specs, meta = build(groups, date)

    diffs = []
    warns = []
    for sl, spec in sorted(specs.items()):
        f = os.path.join(OUT, sl + ".json")
        if write:
            if os.path.exists(f):
                continue               # существующий авторский файл не перезаписываем
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
