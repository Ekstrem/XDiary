"""Гейт EDM: «долг закрыт» проверяется кодом и маркером, а не отчётом (DO-058).

Канон (согласован @AR 2026-11-04 16:26, `04-architecture/edm-debt-plan.md` §2.1):
  * файл `6-msa/BC-<Имя>/EDM-CLOSED`, key-value;
  * обязательные ключи: `migrated-by`, `reviewed-by`, `date` (YYYY-MM-DD), `checks`;
  * `checks` — фиксированный регистрозависимый набор из ШЕСТИ значений,
    проверяется присутствие ВСЕХ шести (а не «поле непустое»).

Почему так, а не «посчитать строки в отчёте»: отчёт пишет та же роль, что закрывает
долг — это самоотчёт. Маркер в репозитории проверяем: он либо есть с полным набором,
либо BC считается не переведённым.

Два режима строгости (DO-058 п.2 «внедрять поэтапно»):
  * `--strict-bc E1,E2` — перечисленные BC в строгом режиме (нарушение = ошибка);
  * остальные BC — предупреждение, пока не пройдут переделку E2.

    python tools/check_edm.py                       # все BC, строгие = E1 по умолчанию
    python tools/check_edm.py --range "<git diff range>"   # только изменённые BC (для CI)
    python tools/check_edm.py --selftest
"""
import argparse
import os
import re
import shutil
import subprocess
import sys

MSA = "6-msa"
MARKER = "EDM-CLOSED"
CANON_CHECKS = ("event-store", "event-bus", "read-store", "consumer", "mv-upsert", "replay")
REQUIRED_KEYS = ("migrated-by", "reviewed-by", "date", "checks")

# Запрещено в application-слое переведённого BC, обязательно — в его инфраструктуре.
FORBIDDEN = (
    ("SaveChangesAsync", "прямой SaveChangesAsync в application-пути"),
    ("IReadRepository", "чтение через IReadRepository вместо ReadStore"),
    ("IBusAdapter", "своя шина вместо AddEventBus"),
    ("ICommandDbContext", "свой DbContext вместо EventStore"),
)
REQUIRED = (
    ("AddEventStorePostgres", "нет AddEventStorePostgres"),
    ("AddEventBus", "нет AddEventBus"),
    ("AddReadStore", "нет AddReadStore"),
    ("IHostedService", "нет фонового consumer'а (IHostedService)"),
    ("UpsertAsync", "нет UpsertAsync в read-store (mv-upsert)"),
)
DEFAULT_STRICT = ()


def bc_dirs(root):
    base = os.path.join(root, MSA)
    if not os.path.isdir(base):
        return []
    return sorted(d for d in os.listdir(base)
                  if d.startswith("BC-") and os.path.isdir(os.path.join(base, d)))


def read_marker(bc_path):
    path = os.path.join(bc_path, MARKER)
    if not os.path.isfile(path):
        return None
    out = {}
    for line in open(path, encoding="utf-8", errors="replace"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if ":" not in line:
            return {"__parse__": "строка без «ключ: значение»: %s" % line[:40]}
        k, v = line.split(":", 1)
        out[k.strip()] = v.strip()
    return out


def check_marker(marker):
    """Проверка маркера по канону: ключи + ровно шесть значений из набора."""
    bad = []
    if "__parse__" in marker:
        return [marker["__parse__"]]
    for key in REQUIRED_KEYS:
        if key not in marker or not marker[key].strip():
            bad.append("нет ключа «%s»" % key)
    date = marker.get("date", "")
    if date and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", date):
        bad.append("«date» не в формате YYYY-MM-DD: %s" % date[:20])
    checks_raw = marker.get("checks", "")
    if checks_raw:
        got = [c.strip() for c in checks_raw.split(",") if c.strip()]
        missing = [c for c in CANON_CHECKS if c not in got]
        extra = [c for c in got if c not in CANON_CHECKS]
        if missing:
            bad.append("в «checks» нет: %s" % ", ".join(missing))
        if extra:
            bad.append("в «checks» лишнее/опечатка: %s" % ", ".join(extra))
    return bad


def scan_code(bc_path):
    """Ищет запрещённые/обязательные маркеры в исходниках BC."""
    hits_forbidden, hits_required = [], set()
    for dirpath, _dirs, files in os.walk(bc_path):
        if os.sep + "bin" in dirpath or os.sep + "obj" in dirpath:
            continue
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            rel = os.path.relpath(path, bc_path)
            for token, why in FORBIDDEN:
                if token in text and ("Application" in rel or "Handlers" in rel):
                    hits_forbidden.append("%s: %s (%s)" % (rel, token, why))
            for token, _why in REQUIRED:
                if token in text:
                    hits_required.add(token)
    missing_required = [why for token, why in REQUIRED if token not in hits_required]
    return hits_forbidden, missing_required


def changed_bcs(root, rng):
    """BC, затронутые диапазоном (для CI: только изменённые, иначе гейт вечно красный)."""
    out = subprocess.run(["git", "diff", "--name-only", rng], cwd=root,
                         capture_output=True, text=True, encoding="utf-8",
                         errors="replace")
    if out.returncode != 0:
        return None
    bcs = set()
    for line in out.stdout.splitlines():
        m = re.match(r"^%s/(BC-[A-Za-z]+)/" % MSA, line.strip())
        if m:
            bcs.add(m.group(1))
    return bcs


def check(root, strict_bcs, only=None):
    results = []
    dirs = bc_dirs(root)
    if only is not None:
        dirs = [d for d in dirs if d in only]
    for bc in dirs:
        bc_path = os.path.join(root, MSA, bc)
        marker = read_marker(bc_path)
        strict = bc in strict_bcs
        if marker is None:
            results.append((bc, strict, False, ["нет маркера %s — BC не переведён" % MARKER], []))
            continue
        marker_bad = check_marker(marker)
        forb, missing_req = scan_code(bc_path)
        problems = marker_bad + forb + missing_req
        results.append((bc, strict, True, problems, []))
    return results


def selftest():
    ok = 0
    total = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-edm")
    shutil.rmtree(base, ignore_errors=True)
    bc = os.path.join(base, MSA, "BC-Test")
    os.makedirs(os.path.join(bc, "src", "Test.Application"), exist_ok=True)
    good_marker = ("migrated-by: @BE\nreviewed-by: @AR\ndate: 2026-11-10\n"
                   "checks: event-store, event-bus, read-store, consumer, mv-upsert, replay\n")

    def run_case(title, marker_text, code_files, expect_problems):
        nonlocal ok, total
        total += 1
        for f in os.listdir(os.path.join(bc, "src", "Test.Application")):
            os.remove(os.path.join(bc, "src", "Test.Application", f))
        mp = os.path.join(bc, MARKER)
        if marker_text is None:
            if os.path.exists(mp):
                os.remove(mp)
        else:
            open(mp, "w", encoding="utf-8", newline="\n").write(marker_text)
        for name, content in code_files.items():
            open(os.path.join(bc, "src", "Test.Application", name), "w",
                 encoding="utf-8", newline="\n").write(content)
        res = check(base, {"BC-Test"})
        _bc, _st, _has, problems, _w = res[0]
        got = len(problems) > 0
        if got == expect_problems:
            ok += 1
            print("  [OK ] %s -> %s" % (title, "нарушения" if got else "чисто"))
        else:
            print("  [FAIL] %s -> %s: %s" % (title, "нарушения" if got else "чисто",
                                             "; ".join(problems)[:90]))

    good_code = {"Svc.cs": "AddEventStorePostgres(); AddEventBus(); AddReadStore(); "
                           "class C : IHostedService { } await repo.UpsertAsync(x);"}
    run_case("нет маркера", None, good_code, True)
    run_case("полный валидный маркер", good_marker, good_code, False)
    run_case("нет reviewed-by", good_marker.replace("reviewed-by: @AR\n", ""), good_code, True)
    run_case("в checks пять из шести",
             good_marker.replace(", replay", ""), good_code, True)
    run_case("опечатка в checks (mv-updsert)",
             good_marker.replace("mv-upsert", "mv-updsert"), good_code, True)
    run_case("SaveChangesAsync в application-слое", good_marker,
             {"Repo.cs": "AddEventStorePostgres(); AddEventBus(); AddReadStore(); "
                         "await db.SaveChangesAsync();"}, True)

    shutil.rmtree(base, ignore_errors=True)
    print("check_edm --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description="Гейт EDM: маркер + код (DO-058)")
    ap.add_argument("--root", default=".")
    ap.add_argument("--range", default=None,
                    help="git diff range: проверять только затронутые BC (для CI)")
    ap.add_argument("--strict-bc", default=",".join(DEFAULT_STRICT),
                    help="BC в строгом режиме через запятую; пусто = все предупреждения "
                         "(переделка не начата, иначе гейт красный на пустом месте)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    strict = {x.strip() for x in a.strict_bc.split(",") if x.strip()}
    only = changed_bcs(root, a.range) if a.range else None
    if a.range and only is not None:
        print("check_edm: --range %s затронул BC: %d" % (a.range, len(only)))

    results = check(root, strict, only)
    errors = warnings = migrated = 0
    for bc, is_strict, has_marker, problems, _w in results:
        if not has_marker:
            if is_strict:
                for p in problems:
                    print("::error::(edm/%s) %s" % (bc, p))
                errors += 1
            else:
                warnings += 1
            continue
        migrated += 1
        for p in problems:
            if is_strict:
                print("::error::(edm/%s) %s" % (bc, p))
                errors += 1
            else:
                print("::warning::(edm/%s) %s" % (bc, p))
                warnings += 1

    total = len(results)
    print("check_edm: BC %d, с маркером %d, не переведены %d (из них строгих с ошибкой %d), "
          "ошибок %d, предупреждений %d"
          % (total, migrated, total - migrated, errors, errors, warnings))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
