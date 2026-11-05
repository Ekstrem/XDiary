"""check_e2e_all.py — одна точка входа для всех машинных проверок `front-e2e`.

Зачем: гейтов про `front-e2e` стало девять (селекторы, покрытие, трассировка, паузы, гигиена данных,
контракт обвязки, пропуски, позиционные клики, квитанция), и каждый надо помнить по имени. Роль,
которая подключает CI, не должна собирать список руками — иначе «гейт, который не запускается,
не существует» (ровно этот класс зафиксирован в `DO-099`: часть тестовых гейтов не была в CI).

Скрипт запускает каждый гейт с `--selftest`, затем без флагов (прогон на репозитории), и печатает
одну сводку. Коды: `0` — все зелёные; `1` — есть красные (rc=1); `2` — есть «не измерено» (rc=2)
при отсутствии красных.

    python 9-tests/scripts/check_e2e_all.py            # все гейты
    python 9-tests/scripts/check_e2e_all.py --list     # только перечень и что каждый проверяет
"""

import argparse
import pathlib
import shutil
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

ROOT = pathlib.Path(__file__).resolve().parents[2]

# (файл, короткое назначение) — порядок от «структуры» к «исполнению»
GATES = (
    ("check_e2e_coverage.py", "экраны фронта покрыты спеками, маркер не врёт"),
    ("check_e2e_selectors.py", "id/классы/тексты спек существуют во фронте"),
    ("check_e2e_interactions.py", "у click/fill есть адресат: обработчик и поле ввода"),
    ("check_e2e_views.py", "экраны, которых ждут спеки, фронт умеет показывать"),
    ("check_e2e_mock_fields.py", "двойник API отдаёт поля, которые спеки утверждают"),
    ("check_e2e_traceability.py", "у каждой спеки TE-ссылка или LEGACY"),
    ("check_e2e_fixture_contract.py", "импорты спек соответствуют module.exports обвязки"),
    ("check_e2e_no_sleeps.py", "нет фиксированных пауз (waitForTimeout)"),
    ("check_e2e_positional.py", "нет кликов по номеру в динамических списках"),
    ("check_e2e_data_hygiene.py", "спеки убирают за собой и создают уникальные данные"),
    ("check_e2e_skips.py", "каждый skip объяснён, флаги видны"),
    ("check_e2e_assertions.py", "у каждой спеки есть поведенческое утверждение"),
    ("check_e2e_ui_coverage.py", "карта: какие элементы фронта не трогает ни одна спека"),
    ("check_e2e_error_paths.py", "негативные пути: элементы ошибок утверждаются спеками"),
    ("check_e2e_api_contract.py", "эндпоинты спек есть в api-index.yaml"),
    ("check_e2e_diagnostics.py", "каждая спека оставляет след для разбора упавшего прогона"),
    ("check_e2e_assertion_strength.py", "утверждения проверяют поведение, а не «что-то истинное»"),
    ("check_e2e_ci_filter.py", "в npm test нет фильтров, которые тихо убирают спеки"),
    ("check_e2e_time_budget.py", "худший случай прогона помещается в timeout-minutes джобы"),
    ("check_e2e_timeout_sums.py", "сумма объявленных ожиданий спеки помещается в лимит теста"),
)

# гейты, которые требуют аргумента и в сводку как «прогон» не входят
ARGS_REQUIRED = {"e2e_receipt.py": "квитанция прогона: нужен файл вывода Playwright"}

# JS-проверки: запускаются через node, если он есть (иначе — «не измерено»)
JS_GATES = (
    ("9-tests/RnD/front-e2e/e2e-dryrun.js",
     "исполняет тела спек на заглушках (без браузера): нет исключений в коде тестов"),
)


def run_gate(path: pathlib.Path, extra=()):
    return subprocess.run([sys.executable, str(path), *extra], cwd=str(ROOT),
                          capture_output=True, text=True, encoding="utf-8", errors="replace")


def summarize(results):
    """results — [(имя, selftest_rc, run_rc, last_line)]. Возвращает (lines, rc)."""
    lines, red, unmeasured = [], 0, 0
    for name, st_rc, run_rc, last in results:
        if st_rc != 0:
            state, red = "КРАСНЫЙ (selftest)", red + 1
        elif run_rc == 0:
            state = "зелёный"
        elif run_rc == 2:
            state, unmeasured = "не измерено", unmeasured + 1
        else:
            state, red = "КРАСНЫЙ", red + 1
        lines.append(f"  {name:<32} {state:<18} {last[:78]}")
    if red:
        rc = 1
    elif unmeasured:
        rc = 2
    else:
        rc = 0
    lines.append(f"сводка front-e2e: гейтов {len(results)} | зелёных {len(results) - red - unmeasured} | "
                 f"красных {red} | не измерено {unmeasured}")
    return lines, rc


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if a.list:
        for name, about in GATES:
            print(f"  {name:<32} {about}")
        for name, about in ARGS_REQUIRED.items():
            print(f"  {name:<32} {about} (вызывается с --input)")
        return 0

    results = []
    node = shutil.which("node")
    for rel, about in JS_GATES:
        path = ROOT / rel
        if not path.exists():
            results.append((pathlib.Path(rel).name, 2, 2, "файла нет — не измерено"))
            continue
        if not node:
            results.append((pathlib.Path(rel).name, 2, 2, "нет node — не измерено"))
            continue
        run = subprocess.run([node, str(path)], cwd=str(path.parent),
                             capture_output=True, text=True, encoding="utf-8", errors="replace")
        last = (run.stdout or "").strip().splitlines()
        results.append((pathlib.Path(rel).name, 0 if run.returncode in (0, 1) else 2,
                        run.returncode, last[-1] if last else about))
    for name, _about in GATES:
        path = ROOT / "9-tests" / "scripts" / name
        if not path.exists():
            results.append((name, 2, 2, "файла нет — не измерено"))
            continue
        st = run_gate(path, ("--selftest",))
        rn = run_gate(path)
        last = (rn.stdout or "").strip().splitlines()
        results.append((name, st.returncode, rn.returncode, last[-1] if last else "(нет вывода)"))
    lines, rc = summarize(results)
    for line in lines:
        print(line)
    return rc


def selftest():
    ok = total = 0

    def case(name, results, want_rc, must=()):
        nonlocal ok, total
        total += 1
        lines, rc = summarize(results)
        text = "\n".join(lines)
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}")

    case("все зелёные",
         [("a.py", 0, 0, "квитанция: ок"), ("b.py", 0, 0, "квитанция: ок")],
         0, ["зелёных 2", "красных 0", "не измерено 0"])

    case("один красный прогон",
         [("a.py", 0, 0, "ок"), ("b.py", 0, 1, "нарушено: 1")],
         1, ["КРАСНЫЙ", "красных 1"])

    case("красный selftest",
         [("a.py", 1, 0, "ок")],
         1, ["КРАСНЫЙ (selftest)"])

    case("не измерено",
         [("a.py", 0, 2, "нет входа"), ("b.py", 0, 0, "ок")],
         2, ["не измерено 1", "зелёных 1"])

    case("красный важнее не измеренного",
         [("a.py", 0, 2, "нет входа"), ("b.py", 0, 1, "нарушено")],
         1, ["красных 1", "не измерено 1"])

    total += 1
    line = "сводка front-e2e: гейтов 3 | зелёных 3 | красных 0 | не измерено 0"
    if "гейтов 3" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст сводки: {line}")

    print(f"check_e2e_all --selftest: {ok}/{total} (красный = rc=1, только «не измерено» = rc=2)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
