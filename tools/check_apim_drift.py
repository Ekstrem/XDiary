#!/usr/bin/env python
"""DO-056: drift-check «индекс/спеки ↔ APIM» (contract-first, решение владельца 2026-11-04).

Что проверяет: набор `operationId` из `04-architecture/api-index.yaml` (и `openapi/*.json`,
когда реестр готов) должен совпадать с тем, что реально опубликовано в Gravitee APIM.
Расхождение → `::error::` и rc=1.

Почему через `podman exec`, а не через порт: на проде Management API **не опубликован**
на хост (только gateway `18082`), а изнутри контейнера `:8083/management/v2/` отвечает
**200**. Публиковать лишний порт наружу ради гейта — не надо.

Токен: `GRAVITEE_TOKEN` (Management API требует Bearer). Без токена гейт **не** зелёный:
он возвращает rc=2 и пишет, чего не хватает — «не смог проверить» не равно «совпадает».

    python tools/check_apim_drift.py --root .              # прод (podman exec) 
    python tools/check_apim_drift.py --root . --api http://127.0.0.1:18083
    python tools/check_apim_drift.py --selftest
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys

INDEX = os.path.join("04-architecture", "api-index.yaml")
OPENAPI_DIR = "openapi"


def index_operations(root):
    """operationId ПУБЛИЧНЫХ операций индекса (`internal: true` не считается).

    2026-11-10 (@AR 15:36, `AR-019`): раньше собирали все `operationId` регуляркой,
    и `internal: true` попадали в счёт: 82 против 80 в публичных спеках → вечный `rc=1`
    на двух внутренних (`DailyMetrics.InternalSeries`, `Journal.ClientIds`).
    Правильный счёт публичных: **блок** операции (`- bc:` … до следующего `- bc:`) без
    `internal: true`. Regex по блокам, а не YAML-парсер: у инструмента нет PyYAML
    в деплой-пути (python из образа), тянуть зависимость ради одного поля — лишнее.
    """
    path = os.path.join(root, INDEX)
    if not os.path.exists(path):
        return set()
    txt = open(path, encoding="utf-8", errors="replace").read()
    # блоки операций начинаются с `- bc:` (см. api-index.yaml); заголовки/правила — без него
    blocks = re.split(r"(?m)^\s*-\s+bc:\s*", txt)[1:]
    out = set()
    for b in blocks:
        body = "- bc: " + b
        if re.search(r"(?m)^\s*internal:\s*true\s*$", body):
            continue
        m = re.search(r'operationId:\s*([A-Za-z0-9_.]+)', body)
        if m and not m.group(1).isdigit():
            out.add(m.group(1))
    return out


def spec_operations(root):
    """operationId ПУБЛИЧНЫХ операций из `openapi/*.json` (реестр @BE).

    2026-11-10 (@AR 15:36): сверять надо публичные с публичными. Внутренние
    (`/internal/**`) попадают в снимок не всегда: сейчас в `openapi/` их **две** из
    четырёх (`Journal.ListAll`, `Journal.IngestPartnerEvent`), а `DailyMetrics.InternalSeries`
    и `Journal.ClientIds` — нет. Поэтому путь `/internal/…` из спеки исключаем, иначе
    сравнение «публичные vs все» даёт вечное расхождение (78 против 80).
    """
    out = set()
    base = os.path.join(root, OPENAPI_DIR)
    if not os.path.isdir(base):
        return out
    for name in sorted(os.listdir(base)):
        if not name.endswith(".json") or name.startswith("_"):
            continue
        try:
            doc = json.load(open(os.path.join(base, name), encoding="utf-8"))
        except (OSError, ValueError):
            continue
        for path, item in (doc.get("paths") or {}).items():
            if str(path).startswith("/internal/"):
                continue
            for op in (item or {}).values():
                if isinstance(op, dict) and op.get("operationId"):
                    out.add(op["operationId"])
    return out


def apim_operations(api_base, token, exec_container=None, timeout=20):
    """operationId, реально опубликованные в APIM (по всем API окружения)."""
    def call(path):
        if exec_container:
            cmd = ["podman", "exec", exec_container, "sh", "-c",
                   "curl -s -m %d -H 'Authorization: Bearer %s' '%s%s'"
                   % (timeout, token, "http://127.0.0.1:8083", path)]
            r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                               errors="replace")
            return r.stdout
        import urllib.request
        req = urllib.request.Request(api_base + path,
                                     headers={"Authorization": "Bearer " + token})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.read().decode("utf-8", "replace")

    try:
        apis = json.loads(call("/management/v2/organizations/DEFAULT/environments/DEFAULT/apis?per_page=100") or "{}")
    except Exception as exc:                       # сеть, токен, формат
        return None, "APIM недоступен или токен не принят: %s" % str(exc)[:120]

    ids = set()
    for api in (apis.get("data") or []):
        api_id = api.get("id")
        if not api_id:
            continue
        try:
            page = json.loads(call("/management/v2/organizations/DEFAULT/environments/DEFAULT/apis/%s/pages"
                                   % api_id) or "{}")
        except Exception:
            continue
        # operationId берём из спек: у Gravitee это страница типа SWAGGER/OPENAPI
        for pg in (page.get("data") or []):
            content = (pg.get("content") or "")
            if "operationId" in content or pg.get("type", "").upper() in ("SWAGGER", "OPENAPI"):
                try:
                    doc = json.loads(content)
                except ValueError:
                    continue
                for item in (doc.get("paths") or {}).values():
                    for op in (item or {}).values():
                        if isinstance(op, dict) and op.get("operationId"):
                            ids.add(op["operationId"])
    return ids, ""


def receipt_header(container=None):
    """Шапка квитанции (условие @AR 2026-11-05 18:50): REF, образ, время.

    Без них «дрейф есть» не работает: непонятно, к какому состоянию он относится.
    Внутри CI REF берётся из `GITHUB_SHA`, локально — из `git rev-parse HEAD`.
    """
    ref = (os.environ.get("GITHUB_SHA") or "").strip()
    if not ref:
        try:
            ref = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True,
                                 encoding="utf-8", errors="replace").stdout.strip()
        except Exception:  # noqa: BLE001
            ref = "неизвестен"
    image = "—"
    if container:
        try:
            out = subprocess.run(
                ["podman", "inspect", container, "--format",
                 "{{.ImageName}}"], capture_output=True, text=True,
                encoding="utf-8", errors="replace")
            if out.returncode == 0 and out.stdout.strip():
                image = out.stdout.strip()
        except Exception:  # noqa: BLE001
            pass
    import datetime
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    # @AR 15:51 (`1091`/`1092`, канон §20): `ref` отвечает «что измеряли», `run`/`attempt` —
    # «какой именно прогон». Одного `ref` мало: на одном коммите `rc` расходится
    # (сегодня доказано серией канона: тот же код дал `rc=0`, потом `rc=2` с `unknown 7`).
    # @AR 15:52 (`1097`): плюс `tool=` — чтобы «каким инструментом» не восстанавливать по памяти.
    tool = "tools/check_apim_drift.py"
    try:
        import hashlib
        with open(os.path.abspath(__file__), "rb") as fh:
            tool = "%s@%s" % (tool, hashlib.sha256(fh.read()).hexdigest()[:10])
    except OSError:
        tool = "%s@нет-файла" % tool
    run_id = (os.environ.get("GITHUB_RUN_ID") or "").strip()
    attempt = (os.environ.get("GITHUB_RUN_ATTEMPT") or "").strip()
    run = ("%s attempt=%s" % (run_id, attempt or "1")) if run_id else "локально"
    print("  REF=%s | образ=%s | время=%s | run=%s | tool=%s"
          % (ref[:12] or "неизвестен", image, now, run, tool))


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-apim")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(os.path.join(base, "04-architecture"))
    os.makedirs(os.path.join(base, "openapi"))
    open(os.path.join(base, "04-architecture", "api-index.yaml"), "w", encoding="utf-8").write(
        # 2026-11-10: блок операции начинается с `- bc:` (так в реальном индексе), а
        # `internal: true` в публичный счёт не входит — это и проверяем.
        "operations:\n"
        "  - bc: Journal\n    method: GET\n    path: \"/api/v1/journal\"\n    operationId: Journal.List\n"
        "  - bc: Client\n    method: GET\n    path: \"/api/v1/clients/me\"\n    operationId: Client.Me\n"
        "  - bc: Journal\n    method: GET\n    path: \"/internal/journal/records\"\n"
        "    operationId: Journal.ListAll\n    internal: true\n")
    got = index_operations(base)
    if got == {"Journal.List", "Client.Me"}:
        ok += 1
    else:
        print("  [FAIL] индекс: %s" % got)
    open(os.path.join(base, "openapi", "journal.json"), "w", encoding="utf-8").write(json.dumps(
        {"paths": {"/journal": {"get": {"operationId": "Journal.List"}},
                   "/internal/journal/records": {"get": {"operationId": "Journal.ListAll"}}}}))
    got = spec_operations(base)
    if got == {"Journal.List"}:
        ok += 1
    else:
        print("  [FAIL] спеки: %s" % got)
    shutil.rmtree(base, ignore_errors=True)
    print("check_apim_drift --selftest: %d/2" % ok)
    return 0 if ok == 2 else 1


def specs_files(root):
    base = os.path.join(root, OPENAPI_DIR)
    if not os.path.isdir(base):
        return []
    return sorted(n[:-5] for n in os.listdir(base)
                  if n.endswith(".json") and n not in ("index.json", "_meta.json"))


def _norm(name):
    return re.sub(r'[^a-z0-9]', '', name.lower())


def compare_names(spec_slugs, apim_names):
    """Сопоставление «slug спеки» ↔ «имя API в APIM» по нормализованному виду."""
    apim_norm = {_norm(n): n for n in apim_names}
    spec_norm = {_norm(sl): sl for sl in spec_slugs}
    seen = 0
    missing, extra = set(), set()
    for norm, slug in spec_norm.items():
        hit = None
        for an, orig in apim_norm.items():
            if norm == an or norm in an or an in norm:
                hit = orig
                break
        if hit:
            seen += 1
            apim_norm.pop(_norm(hit), None)
        else:
            missing.add(slug)
    for orig in apim_norm.values():
        extra.add(orig)
    return seen, missing, extra


def apim_operations_basic(api_base, basic, exec_container=None, timeout=25):
    """Список API из APIM по Basic-авторизации (через podman exec или напрямую)."""
    base = ("http://127.0.0.1:8083" if exec_container else api_base)
    url = (base + "/management/v2/organizations/DEFAULT/environments/DEFAULT/"
                  "apis?page=%d&per_page=10")

    def get(path):
        if exec_container:
            cmd = ["podman", "exec", exec_container, "sh", "-c",
                   "curl -s -m %d -u '%s' '%s'" % (timeout, basic, path)]
            return subprocess.run(cmd, capture_output=True, text=True,
                                  encoding="utf-8", errors="replace").stdout
        import urllib.request
        import base64
        req = urllib.request.Request(
            path, headers={"Authorization": "Basic " + base64.b64encode(basic.encode()).decode()})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.read().decode("utf-8", "replace")

    names = set()
    for page in (1, 2, 3):
        try:
            data = json.loads(get(url % page) or "{}")
        except Exception as exc:
            return None, "APIM недоступен или Basic не принят: %s" % str(exc)[:100]
        batch = data.get("data") or []
        for a in batch:
            if a.get("name"):
                names.add(a["name"])
        if len(batch) < 10:
            break
    return names, ""

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--api", default="http://127.0.0.1:18083")
    ap.add_argument("--container", default="xdiary-apim-management")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--specs-only", action="store_true",
                    help="только посчитать спеки/индекс, без обращения к APIM")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    root = os.path.abspath(a.root)
    token = os.environ.get("GRAVITEE_TOKEN", "").strip()
    basic = os.environ.get("GRAVITEE_BASIC", "").strip()      # "user:password"
    want = index_operations(root)
    specs = spec_operations(root)
    receipt_header(None if getattr(a, "specs_only", False) else a.container)
    print("check_apim_drift: индекс %d operationId, спеки %d (openapi/*.json)"
          % (len(want), len(specs)))
    # @TL 2026-11-05 18:48: спеки не скопированы => это НЕ ИЗМЕРЕНО, а не «80 расхождений».
    # Раньше отсутствие openapi/*.json давало rc=1 с 80 строками «есть в реестре, нет в спеке»,
    # и такое красное читалось как дрейф. Красное по неверной причине хуже зелёного.
    if a.specs_only and not specs and want:
        print("::error::check_apim_drift: НЕ ИЗМЕРЕНО — в индексе %d операций, а спек 0 "
              "(каталог openapi/*.json не найден или не скопирован)" % len(want))
        return 2
    if a.specs_only:
        # DO-056: раньше режим ТОЛЬКО печатал числа и всегда возвращал 0 — то есть
        # «81 в индексе против 80 в спеках» проходило зелёным (проверено 2026-11-05:
        # фейковая операция в индексе → rc=0). Теперь сравниваются МНОЖЕСТВА,
        # а не количества: расхождение — ошибка.
        missing_in_specs = sorted(want - specs)
        extra_in_specs = sorted(specs - want)
        for op in missing_in_specs:
            print("::error::(specs) %s — есть в api-index.yaml, нет в openapi/*.json" % op)
        for op in extra_in_specs:
            print("::error::(specs) %s — есть в openapi/*.json, нет в api-index.yaml" % op)
        if missing_in_specs or extra_in_specs:
            print("check_apim_drift: --specs-only: индекс %d, спеки %d, расхождений %d "
                  "(APIM не опрашивался)"
                  % (len(want), len(specs), len(missing_in_specs) + len(extra_in_specs)))
            return 1
        print("check_apim_drift: --specs-only: индекс %d, спеки %d, расхождений 0 "
              "(APIM не опрашивался)" % (len(want), len(specs)))
        return 0
    if basic:
        got, err = apim_operations_basic(a.api, basic, exec_container=a.container)
        want_all = specs if specs else set()
        apis_apim = got or set()
        print("check_apim_drift: APIM API %d, openapi спек %d, операций в openapi %d"
              % (len(apis_apim), len(specs_files(a.root)), len(want_all)))
        if err:
            print("::error::(apim) %s" % err)
            return 2
        if not specs_files(a.root) and not apis_apim:
            print("check_apim_drift: спек нет и в APIM пусто — расхождений 0")
            return 0
        seen, missing, extra = compare_names(specs_files(a.root), apis_apim)
        for name in sorted(missing):
            print("::error::(apim) %s — спека есть, API в APIM нет" % name)
        for name in sorted(extra):
            print("::warning::(apim) %s — API в APIM есть, спеки нет" % name)
        print("check_apim_drift: совпало по именам %d из %d спек; расхождений %d"
              % (seen, len(specs_files(a.root)), len(missing) + len(extra)))
        return 1 if missing else 0

    if not token:
        print("::warning::GRAVITEE_TOKEN не задан — сравнение с APIM не выполнено (это НЕ «совпадает»)")
        return 2
    got, err = apim_operations(a.api, token, exec_container=a.container)
    if got is None:
        print("::error::(apim) %s" % err)
        return 2
    base = specs if specs else set()
    missing = sorted(base - got)
    extra = sorted(got - base) if base else []
    if not base and not got:
        print("check_apim_drift: спек нет и в APIM пусто — импортировать нечего, расхождений 0")
        return 0
    if want and not specs:
        print("check_apim_drift: индекс %d операций, спек 0 — drift считается по спекам "
              "(реестр ещё не выкачен), APIM %d" % (len(want), len(got)))
    for op in missing[:20]:
        print("::error::(apim) %s — есть в реестре (индекс/спеки), НЕТ в APIM" % op)
    for op in extra[:20]:
        print("::error::(apim) %s — есть в APIM, НЕТ в реестре (индекс/спеки)" % op)
    print("check_apim_drift: APIM %d операций, расхождений %d" % (len(got), len(missing) + len(extra)))
    return 1 if (missing or extra) else 0



if __name__ == "__main__":
    sys.exit(main())

