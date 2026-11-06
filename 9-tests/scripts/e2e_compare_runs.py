"""e2e_compare_runs.py — сравнить два браузерных прогона Front E2E по тестам, а не по числам.

Зачем: в отчётах я каждый раз писал «было 19 → стало 14», но проверить это мог только я — скрипты были
одноразовые. Здесь то же самое воспроизводимо: на вход два прогона (id GitHub Actions) или два
сохранённых лога, на выходе — кто позеленел, кто покраснел заново и кто остался красным.

Источник данных — аннотации `::error::{json}` в логе джобы (GitHub переписывает их в `##[error]{json}`),
в них есть `title` (имя теста) и `status` (`failed`/`timedOut`/…). Прогон без браузерной джобы
(браузер идёт только по `workflow_dispatch`/`schedule`) даёт `rc=2` — «не измерено», а не «зелено».

Использование::

    python 9-tests/scripts/e2e_compare_runs.py --runs 42143558389 42146688060
    python 9-tests/scripts/e2e_compare_runs.py --from-logs run765.txt run775.txt
    python 9-tests/scripts/e2e_compare_runs.py --selftest

Коды: `0` — регрессий нет; `1` — есть тесты, которые падали раньше и падают сейчас ИЛИ падают только в
новом прогоне (регресс); `2` — сравнить нечего (нет данных).
"""

import argparse
import json
import os
import pathlib
import re
import sys
import urllib.error
import urllib.request
import zipfile

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

ANNOT = re.compile(r"(?:::error::|##\[error\])(\{.*\})\s*$")
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
TEST_PREFIX = "E2E:"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def _token():
    for env in ("GITHUB_TOKEN", "GH_TOKEN", "PROJECT_PAT"):
        if os.environ.get(env):
            return os.environ[env]
    return ""


def _api(path, token):
    req = urllib.request.Request(
        "https://api.github.com" + path,
        headers={"User-Agent": "e2e-compare", "Accept": "application/vnd.github+json",
                 "Authorization": "Bearer " + token},
    )
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def _download(url, token):
    opener = urllib.request.build_opener(NoRedirect)
    try:
        opener.open(urllib.request.Request(
            url, headers={"User-Agent": "e2e-compare", "Authorization": "Bearer " + token}), timeout=60)
        raise RuntimeError("ожидали 302 на подписанный URL, а его нет")
    except urllib.error.HTTPError as e:
        loc = e.headers.get("Location")
        if not loc:
            raise
        with urllib.request.urlopen(urllib.request.Request(loc, headers={"User-Agent": "e2e-compare"}),
                                    timeout=300) as r:
            return r.read()


def log_from_run(run_id, token, repo="Ekstrem/XDiary"):
    blob = _download(f"https://api.github.com/repos/{repo}/actions/runs/{run_id}/logs", token)
    z = zipfile.ZipFile(__import__("io").BytesIO(blob))
    return "\n".join(z.read(n).decode("utf-8", "replace") for n in z.namelist())


def parse_failing(log_text):
    """{имя теста: статус} по аннотациям лога."""
    out = {}
    for line in log_text.splitlines():
        m = ANNOT.search(line.strip())
        if not m:
            continue
        try:
            d = json.loads(m.group(1))
        except ValueError:
            continue
        title = str(d.get("title") or "").strip()
        if not title.startswith(TEST_PREFIX):
            continue
        out[title] = str(d.get("status") or "failed")
    return out


def compare(old, new):
    """(fixed, regressed, still, ok) — сортированные списки имён."""
    fixed = sorted(set(old) - set(new))
    regressed = sorted(set(new) - set(old))
    still = sorted(set(old) & set(new))
    return fixed, regressed, still, regressed


def render(old_name, new_name, old, new):
    fixed, regressed, still, _ = compare(old, new)
    lines = [f"   Прогон A: {old_name} — упавших тестов {len(old)}",
             f"   Прогон B: {new_name} — упавших тестов {len(new)}"]
    lines.append(f"   позеленело: {len(fixed)} | покраснело заново: {len(regressed)} | осталось красным: {len(still)}")
    if fixed:
        lines.append("   --- позеленели:")
        lines += [f"      + {t}" for t in fixed]
    if regressed:
        lines.append("   --- РЕГРЕСС (падали только в B):")
        lines += [f"      - {t}" for t in regressed]
    if still:
        lines.append("   --- остались красными:")
        lines += [f"      = {t}" for t in still]
    return lines, fixed, regressed, still


def receipt_line(old_name, new_name, old, new):
    fixed, regressed, still, _ = compare(old, new)
    return (f"квитанция: A={old_name} упало {len(old)} | B={new_name} упало {len(new)} | "
            f"позеленело {len(fixed)} | регресс {len(regressed)} | осталось {len(still)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", nargs=2, metavar=("A", "B"))
    ap.add_argument("--from-logs", nargs=2, metavar=("A.txt", "B.txt"))
    ap.add_argument("--repo", default="Ekstrem/XDiary")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)

    if a.selftest:
        return selftest()

    if a.from_logs:
        paths = [pathlib.Path(p) for p in a.from_logs]
        if not all(p.is_file() for p in paths):
            print("::error::e2e_compare_runs: нет файла лога — не измерено (rc=2)")
            return 2
        texts = [p.read_text(encoding="utf-8", errors="replace") for p in paths]
        names = [p.name for p in paths]
    elif a.runs:
        token = _token()
        if not token:
            print("::error::e2e_compare_runs: нет токена (GITHUB_TOKEN/GH_TOKEN/PROJECT_PAT) — не измерено (rc=2)")
            return 2
        try:
            texts = [log_from_run(a.runs[0], token, a.repo), log_from_run(a.runs[1], token, a.repo)]
        except (urllib.error.HTTPError, zipfile.BadZipFile, RuntimeError) as e:
            print(f"::error::e2e_compare_runs: не скачал лог ({e}) — не измерено (rc=2)")
            return 2
        names = [f"run {a.runs[0]}", f"run {a.runs[1]}"]
    else:
        ap.error("нужно --runs A B или --from-logs A.txt B.txt или --selftest")

    old, new = parse_failing(texts[0]), parse_failing(texts[1])
    if not old and not new:
        print("::error::e2e_compare_runs: ни в одном логе нет аннотаций E2E — не измерено (rc=2)")
        return 2
    lines, fixed, regressed, still = render(names[0], names[1], old, new)
    print("\n".join(lines))
    print(receipt_line(names[0], names[1], old, new))
    worse = bool(still) or bool(regressed)
    return 1 if worse else 0


def _annot(title, status="failed"):
    return "2026-11-06T02:00:00Z ##[error]" + json.dumps({"title": title, "status": status}, ensure_ascii=False)


def selftest():
    ok = total = 0

    def case(name, log_a, log_b, want_rc=None, must=()):
        nonlocal ok, total
        total += 1
        old, new = parse_failing(log_a), parse_failing(log_b)
        lines, fixed, regressed, still = render("A", "B", old, new)
        text = "\n".join(lines) + "\n" + receipt_line("A", "B", old, new)
        rc = 1 if (still or regressed) else 0
        miss = [m for m in must if m not in text]
        rc_ok = True if want_rc is None else (rc == want_rc)
        if rc_ok and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    a = "\n".join([_annot("E2E: один"), _annot("E2E: два"), _annot("E2E: три", "timedOut")])
    b = "\n".join([_annot("E2E: два"), _annot("E2E: четыре")])
    case("позеленел+регресс+осталось", a, b, 1,
         ["позеленело: 2", "покраснело заново: 1", "осталось красным: 1", "+ E2E: один", "- E2E: четыре"])

    case("всё позеленело", a, "", 0, ["позеленело: 3", "регресс 0"])

    case("аннотация в формате ::error:: тоже читается",
         "", "2026-11-06T02:00:00Z ::error::" + json.dumps({"title": "E2E: пять", "status": "failed"}),
         1, ["покраснело заново: 1"])

    total += 1
    if parse_failing("##[error]" + json.dumps({"title": "browser http error"})) == {}:
        ok += 1
    else:
        print("  [FAIL] не-тестовые аннотации не должны попадать в выборку")

    print(f"e2e_compare_runs --selftest: {ok}/{total} (A/B по тестам: регресс = красный только в B)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
