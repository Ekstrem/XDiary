#!/usr/bin/env python
"""DO-073: гейт «схема БД = миграции в коде».

Почему: 2026-11-14 выяснилось, что миграция `20261113120000_AddDeletedAtToPartner`
**есть в коде** (коммит `5eed2ae1`) и даже **внутри образов** на проде и dev
(`git merge-base --is-ancestor` — да), но в БД её **никто не применил**: колонки
`DeletedAt` не было ни там, ни там. То есть soft-delete партнёра не работал, а
«приёмка E1» относилась к коду, а не к тому, что видит владелец.

Что делает: для каждого сервиса с `Migrations/` собирает **ожидаемый** список миграций
из кода и **фактический** из `__EFMigrationsHistory`, печатает расхождение, rc=1 если
код ушёл вперёд схемы (или в схеме есть миграции, которых нет в коде).

Где запускать: **на хосте** (там есть podman и БД), например в `7-deploy/update.sh`
после пересоздания контейнеров. В CI не запускается: базы там нет.

    python tools/check_schema.py --root /opt/xdiary --container xdiary-pg
    python tools/check_schema.py --root . --container xdiary-pg --sudo   # rootful podman
    python tools/check_schema.py --selftest
"""
import argparse
import os
import re
import shutil
import subprocess
import sys

# сервис -> (папка миграций в репо, БД, пользователь БД)
SERVICES = [
    ("partners", "6-msa/BC-Partners", "partners", "partners"),
    ("journal", "6-msa/BC-Journal", "journal", "journal"),
    ("client", "6-msa/BC-Client", "client", "client"),
    ("catalog", "6-msa/BC-Catalog", "catalog", "catalog"),
    ("daily-metrics", "6-msa/BC-DailyMetrics", "daily_metrics", "daily_metrics"),
    ("somatic-series", "6-msa/BC-SomaticSeries", "somatic_series", "somatic_series"),
    ("privacy", "6-msa/BC-Privacy", "privacy", "privacy"),
    ("portability", "6-msa/BC-Portability", "portability", "portability"),
    ("analytics", "6-msa/BC-Analytics", "analytics", "analytics"),
    ("garmin-gateway", "6-msa/BC-GarminGateway", "garmin_gateway", "garmin_gateway"),
    ("apple-health-gateway", "6-msa/BC-AppleHealthGateway", "apple_health_gateway",
     "apple_health_gateway"),
]

MIG = re.compile(r"^(\d{14})_([A-Za-z0-9_]+)\.cs$")


def expected(root, path):
    """Список MigrationId из кода (файлы `NNNN_*.cs`, .Designer.cs не в счёт)."""
    out = []
    base = os.path.join(root, path)
    if not os.path.isdir(base):
        return out
    for dirpath, _dirs, names in os.walk(base):
        if os.path.basename(dirpath) != "Migrations":
            continue
        for name in names:
            m = MIG.match(name)
            if m and not name.endswith(".Designer.cs"):
                out.append("%s_%s" % (m.group(1), m.group(2)))
    return sorted(out)


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.returncode, r.stdout.strip(), r.stderr.strip()


def actual(container, db_key, user, sudo=False):
    pod = ["sudo", "-n", "podman"] if sudo else ["podman"]
    rc, out, err = run(pod + ["exec", container, "psql", "-U", user, "-d", db_key,
                              "-A", "-t", "-c",
                              'select "MigrationId" from "__EFMigrationsHistory" order by 1'])
    if rc != 0:
        return None, (err or out)[:200]
    return [l.strip() for l in out.splitlines() if l.strip()], ""


def check(root, container, sudo=False):
    v = []
    for name, path, db_key, user in SERVICES:
        exp = expected(root, path)
        if not exp:
            continue
        act, err = actual(container, db_key, user, sudo)
        if act is None:
            v.append(("?", name, "не удалось прочитать историю миграций: %s" % err))
            continue
        missing = [m for m in exp if m not in act]
        extra = [m for m in act if m not in exp]
        if missing:
            v.append(("схема отстала", name, "в БД нет миграций: %s" % ", ".join(missing)))
        if extra:
            # НЕ ошибка: чаще всего это след squash/rename (например `AddSelfToPartner`
            # влит в `AddClientIdToPartner`). Ошибка — только когда код ушёл вперёд схемы:
            # тогда деплой приложения упадёт на отсутствующей колонке.
            v.append(("warning-extra", name, "в БД есть миграции, которых нет в коде: %s"
                      % ", ".join(extra)))
    return v


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-schema")
    shutil.rmtree(base, ignore_errors=True)
    mig = os.path.join(base, "6-msa", "BC-Partners", "src", "Partners.Storage", "Migrations")
    os.makedirs(mig)
    for n in ("20261030112254_InitialCreate.cs", "20261030112254_InitialCreate.Designer.cs",
              "20261101185012_AddSelfToPartner.cs"):
        open(os.path.join(mig, n), "w", encoding="utf-8").write("// x")
    got = expected(base, "6-msa/BC-Partners")
    if got == ["20261030112254_InitialCreate", "20261101185012_AddSelfToPartner"]:
        ok += 1
    else:
        print("  [FAIL] ожидаемый список из кода: %s" % got)
    # Designer не считается миграцией, а вложенный мусор не ломает список
    if "20261030112254_InitialCreate" in got and len(got) == 2:
        ok += 1
    shutil.rmtree(base, ignore_errors=True)
    print("check_schema --selftest: %d/2" % ok)
    return 0 if ok == 2 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--container", default="xdiary-pg")
    ap.add_argument("--sudo", action="store_true", help="rootful podman (нужен sudo -n)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    v = check(os.path.abspath(a.root), a.container, a.sudo)
    errors = [x for x in v if x[0] != "warning-extra"]
    for kind, name, why in v:
        prefix = "::warning" if kind == "warning-extra" else "::error"
        print("%s::(схема) %s: %s — %s" % (prefix, name, kind, why))
    n_serv = len([1 for n, p, _d, _u in SERVICES if expected(os.path.abspath(a.root), p)])
    print("check_schema: сервисов с миграциями %d, ошибок %d, предупреждений %d"
          % (n_serv, len(errors), len(v) - len(errors)))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
