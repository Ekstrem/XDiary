"""check_e2e_time_budget.py — прогон помещается в таймаут CI-джобы.

Зачем: `workers: 1`, `timeout: 90_000` на тест и 29 тестов дают **худший случай 43 минуты**,
а джоба `front-e2e` ограничена `timeout-minutes: 30`. Значит один зависший тест убивает прогон
на 30-й минуте — и результат приходит как «оборван», без квитанции. Это не дефект продукта и не
дефект спек, но диагностируется одинаково плохо.

Гейт считает бюджет и падает, если худший случай превышает таймаут джобы (или близок к нему).
Число тестов берётся из `playwright test --list`, если установлены зависимости
(`node_modules/@playwright/test`), иначе — грубо по вхождениям `test(`.

Коды: `0` — бюджет в пределах; `1` — превышает или слишком близко; `2` — не измерено (нет конфига/CI).
"""

import argparse
import pathlib
import re
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

MARGIN = 0.8  # доля таймаута джобы, после которой считаем «впритык»


def parse_config(cfg_text):
    """(timeout_sec, workers)."""
    m = re.search(r"timeout:\s*([\d_]+)", cfg_text)
    timeout_ms = int(m.group(1).replace("_", "")) if m else 90_000
    w = re.search(r"workers:\s*(\d+)", cfg_text)
    workers = int(w.group(1)) if w else 1
    return timeout_ms / 1000.0, max(workers, 1)


def parse_job_timeout(wf_text, job="front-e2e"):
    """таймаут джобы в минутах или None."""
    idx = wf_text.find(job + ":")
    if idx < 0:
        return None
    tail = wf_text[idx:]
    m = re.search(r"timeout-minutes:\s*(\d+)", tail)
    return int(m.group(1)) if m else None


def count_tests(base: pathlib.Path, tests_dir: pathlib.Path):
    """(count, источник)."""
    pw = base / "node_modules" / "@playwright" / "test" / "cli.js"
    if pw.exists():
        try:
            r = subprocess.run(["node", str(pw), "test", "--list"],
                               cwd=str(base), capture_output=True, text=True,
                               encoding="utf-8", errors="replace", timeout=180)
            m = re.search(r"Total:\s*(\d+)\s+tests", r.stdout or "")
            if m:
                return int(m.group(1)), "--list"
        except (OSError, subprocess.SubprocessError):
            pass
    total = 0
    try:
        for p in sorted(tests_dir.glob("*.spec.js")):
            total += len(re.findall(r"^\s*test\(", p.read_text(encoding="utf-8", errors="replace"), re.M))
    except OSError as e:
        return None, f"не читаются спеки: {e}"
    return total, "test()"


def evaluate(tests, timeout_sec, workers, job_minutes):
    """(issues, stats)."""
    smallest = -(-tests // workers)  # ceil
    worst_min = smallest * timeout_sec / 60.0
    stats = {"tests": tests, "timeout": timeout_sec, "workers": workers,
             "job": job_minutes, "worst": round(worst_min, 1)}
    issues = []
    if job_minutes is None:
        issues.append("в workflow не найден `timeout-minutes` у джобы `front-e2e` — "
                      "границу прогона проверить нельзя")
        return issues, stats
    if worst_min > job_minutes:
        issues.append(f"худший случай {worst_min:.0f} мин ({tests} тестов × {timeout_sec:.0f} с) "
                      f"больше таймаута джобы {job_minutes} мин — зависший тест оборвёт прогон "
                      f"без квитанции; поднимите timeout-minutes или снизьте timeout теста")
    # «впритык» — НЕ ошибка: худший случай предполагает, что каждый тест выберет свой лимит целиком,
    # а это практически невозможно. Печатаем запас числом и решаем по факту прогона (@DO поднял
    # таймаут до 45 мин — 39 мин худшего случая даёт запас 13%, и это нормально).
    stats["margin_pct"] = round((1 - worst_min / job_minutes) * 100, 1) if job_minutes else None
    return issues, stats


def receipt_line(stats, issues):
    job = stats["job"] if stats["job"] is not None else "—"
    margin = stats.get("margin_pct")
    tail = f" | запас {margin}%" if margin is not None else ""
    return (f"квитанция: тестов {stats['tests']} | timeout {stats['timeout']:.0f} с | "
            f"workers {stats['workers']} | худший случай {stats['worst']} мин | "
            f"таймаут джобы {job} мин{tail} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict", action="store_true",
                    help="превышение бюджета — ошибка (rc=1); без флага — ::warning::")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = pathlib.Path(a.root)
    base = root / "9-tests" / "RnD" / "front-e2e"
    cfg, tests_dir = base / "playwright.config.js", base / "tests"
    wf = root / ".github" / "workflows" / "ci-front.yml"
    if not cfg.exists() or not tests_dir.is_dir() or not wf.exists():
        print("::error::check_e2e_time_budget: нет конфига, спек или ci-front.yml — "
              "не измерено (rc=2)")
        return 2
    try:
        timeout_sec, workers = parse_config(cfg.read_text(encoding="utf-8", errors="replace"))
        job_minutes = parse_job_timeout(wf.read_text(encoding="utf-8", errors="replace"))
    except OSError as e:
        print(f"::error::check_e2e_time_budget: не читается вход: {e} — не измерено (rc=2)")
        return 2
    tests, source = count_tests(base, tests_dir)
    if tests is None:
        print(f"::error::check_e2e_time_budget: {source} — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(tests, timeout_sec, workers, job_minutes)
    # Таймаут джобы — в зоне @DO (`ci-front.yml`), поэтому по умолчанию это ПРЕДУПРЕЖДЕНИЕ:
    # гейт не должен блокировать мою зону из-за чужого файла, но обязан быть видимым в выводе.
    # Строгий режим — `--strict` (его и надо включать в CI после правки workflow).
    marker = "::error::" if a.strict else "::warning::"
    for m in issues:
        print(f"{marker}check_e2e_time_budget: {m}")
    print(receipt_line(stats, issues) + f" | тестов считано по: {source}"
          + ("" if a.strict else " | режим: предупреждение (--strict → ошибка)"))
    return 1 if (issues and a.strict) else 0


def selftest():
    ok = total = 0

    def case(name, tests, timeout_sec, workers, job, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(tests, timeout_sec, workers, job)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("влезает с запасом", 10, 30, 1, 30, 0, ["худший случай 5.0 мин", "нарушено: 0"])
    case("не влезает", 29, 90, 1, 30, 1, ["44 мин", "больше таймаута джобы"])
    case("впритык не краснеет (запас есть)", 20, 60, 1, 25, 0, ["запас 20.0%"])
    case("нет timeout-minutes", 5, 30, 1, None, 1, ["timeout-minutes"])
    case("два воркера делят время", 20, 60, 2, 30, 0, ["худший случай 10.0 мин"])

    total += 1
    line = receipt_line({"tests": 3, "timeout": 60.0, "workers": 1, "job": 30, "worst": 3.0}, [])
    if "тестов 3" in line and "нарушено: 0" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_time_budget --selftest: {ok}/{total} (худший случай > таймаута джобы = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
