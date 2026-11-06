"""e2e_receipt.py — квитанция прогона `front-e2e` из вывода Playwright (list или json).

Зачем: прогон набора живёт в CI, и его результат сейчас разбирается **скриптом внутри workflow**
(`front-e2e-repeat.yml`, шаг «Итог по спекам»). Дублировать эту логику и читать вывод глазами — два
источника правды. Этот скрипт даёт одну форму квитанции для обоих случаев и может вызываться локально:

    npx playwright test --reporter=list  > /tmp/e2e.txt
    python 9-tests/scripts/e2e_receipt.py --input /tmp/e2e.txt --ref <sha>

Квитанция: `итого N: passed P, failed F, skipped S | спек M | упавшие: … | REF=…`.
Коды: `0` — все passed (пропуски допустимы и попадают в квитанцию числом); `1` — есть failed;
`2` — не измерено (нет вывода / вывод не разобран).
"""

import argparse
import collections
import json
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# маркеры list-репортёра Playwright: ✓ passed, ✘/✗/× failed, - skipped
MARK = {"\u2713": "passed", "\u2717": "failed", "\u2718": "failed", "\u00d7": "failed",
        "-": "skipped", "\u203a": "skipped", "\u00bb": "skipped"}
# строка list-репортёра: маркер, номер, [проект] › путь:строка › название (время)
# проект может отсутствовать, а у ретраев в названии есть «(retry #N)»
LINE = re.compile(r"^\s*([\u2713\u2717\u2718\u00d7\-\u203a\u00bb])\s+\d+\s+"
                  # TE-088: `executed-guard.js` пишет `✓  1 › path:line › title` — БЕЗ `[project]`,
                  # но с `›` перед путём. Прежний шаблон ждал `[project] ›` и такие строки не разбирал,
                  # из-за чего квитанция из артефакта (#775) и текущий формат стража давали `rc=2`
                  # «вывод не разобран»: квитанцию писал страж, а прочитать её было нечем.
                  r"(?:\[[^\]]+\]\s*\u203a\s*|\u203a\s*)?"
                  # путь в реальном выводе приходит БЕЗ префикса `tests/`
                  # (`onboarding.spec.js:9:1`), поэтому префикс необязателен.
                  r"((?:tests/)?[\w./\-\[\]() ]+?\.spec\.js)")
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")


def parse_list(text):
    # В CI/терминале Playwright красит маркеры ANSI-кодами (`\x1b[32m\u2713\x1b[39m`),
    # из-за чего `^\s*(\u2713…)` не совпадал и весь разбор уходил в «не измерено».
    text = ANSI.sub("", text)
    per_spec = collections.OrderedDict()
    totals = collections.Counter()
    for line in text.splitlines():
        m = LINE.search(line)
        if not m:
            continue
        state = MARK.get(m.group(1))
        if not state:
            continue
        spec = m.group(2)
        c = per_spec.setdefault(spec, collections.Counter())
        c[state] += 1
        totals[state] += 1
        if "retry #" in line:
            c["retries"] += 1
            totals["retries"] += 1
    # «флейки»: у спеки были падения, но итог — passed (Playwright тоже печатает «N flaky»)
    for c in per_spec.values():
        if c.get("failed") and c.get("passed") and c.get("retries"):
            c["flaky"] = 1
            totals["flaky"] += 1
            c["failed"] = 0
            totals["failed"] -= 1
    return per_spec, totals


def parse_json(text):
    """Поддержка `--reporter=json` (одна JSON-структура на весь прогон)."""
    try:
        doc = json.loads(text)
    except ValueError:
        return None, None
    per_spec = collections.OrderedDict()
    totals = collections.Counter()
    for suite in doc.get("suites", []):
        for spec in suite.get("specs", []):
            state = "passed"
            for t in spec.get("tests", []):
                res = t.get("results", [])
                statuses = {r.get("status") for r in res}
                if "failed" in statuses or "timedOut" in statuses:
                    state = "failed"
                elif statuses and statuses <= {"skipped"}:
                    state = "skipped"
            name = spec.get("file") or spec.get("title") or "?"
            per_spec.setdefault(name, collections.Counter())
            per_spec[name][state] += 1
            totals[state] += 1
    return per_spec, totals


def receipt(per_spec, totals, ref):
    def n(k):
        return totals.get(k, 0)

    failed_specs = [s for s, c in per_spec.items() if c.get("failed")]
    flaky_specs = [s for s, c in per_spec.items() if c.get("flaky")]
    total = sum(totals.values()) - n("retries")
    parts = [f"итого {total}: passed {n('passed')}, failed {n('failed')}, skipped {n('skipped')}"]
    if n("flaky"):
        # флейк — это пройденный тест с падениями в ретраях: зелёный, но нестабильный,
        # и в квитанции он обязан быть виден, иначе «зелено» скрывает нестабильность
        parts.append(f"flaky {n('flaky')} (пройден ретраем)")
    parts.append(f"спек {len(per_spec)}")
    if failed_specs:
        parts.append("упавшие: " + ", ".join(sorted(failed_specs)[:8])
                     + (" …" if len(failed_specs) > 8 else ""))
    if flaky_specs:
        parts.append("нестабильные: " + ", ".join(sorted(flaky_specs)[:8])
                     + (" …" if len(flaky_specs) > 8 else ""))
    if n("retries"):
        parts.append(f"ретраев {n('retries')}")
    parts.append(f"REF={ref or 'НЕ УКАЗАН'}")
    return "квитанция front-e2e: " + " | ".join(parts), failed_specs, total


def table_report(per_spec, totals, ref, repeat=None, workers=None):
    """Markdown-таблица «спек | passed | failed | skipped | итого» для шага CI.

    Нужна вместо встроенного парсера в `front-e2e-repeat.yml`: тот требовал префикс `tests/`
    и не снимал ANSI, поэтому на реальном выводе не разбирал НИ ОДНОЙ строки и валил шаг
    `Итог по спекам` в `::error::` (измерение флейкости не могло дать чисел).
    """
    lines = ["| спек | passed | failed | skipped | flaky | итого |", "|---|---|---|---|---|---|"]
    tp = tf = tsk = tfl = 0
    for spec, c in per_spec.items():
        passed, failed, skipped = c.get("passed", 0), c.get("failed", 0), c.get("skipped", 0)
        flaky = c.get("flaky", 0)
        tp += passed
        tf += failed
        tsk += skipped
        tfl += flaky
        lines.append(f"| `{spec}` | {passed} | {failed} | {skipped} | {flaky} | {passed + failed + skipped} |")
    lines.append(f"| **итого** | **{tp}** | **{tf}** | **{tsk}** | **{tfl}** | **{tp + tf + tsk}** |")
    tail = []
    if repeat:
        tail.append(f"repeat-each={repeat}")
    if workers:
        tail.append(f"workers={workers}")
    if ref:
        tail.append(f"REF={ref}")
    if tail:
        lines.append("")
        lines.append(" · ".join(tail))
    return "\n".join(lines), tp, tf, tsk


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", help="файл с выводом Playwright (list или json)")
    ap.add_argument("--text", help="вывод Playwright строкой (для проверки)")
    ap.add_argument("--ref", help="REF, на котором шёл прогон")
    ap.add_argument("--table", action="store_true",
                    help="markdown-таблица по спекам (для шага CI вместо встроенного парсера)")
    ap.add_argument("--repeat", help="значение --repeat-each (попадёт в таблицу)")
    ap.add_argument("--workers", help="значение --workers (попадёт в таблицу)")
    ap.add_argument("--summary-file", help="дописать таблицу в этот файл (GITHUB_STEP_SUMMARY)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    if a.text is not None:
        text = a.text
    elif a.input:
        try:
            text = open(a.input, encoding="utf-8", errors="replace").read()
        except OSError as e:
            print(f"::error::e2e_receipt: не читается {a.input}: {e} — не измерено (rc=2)")
            return 2
    else:
        print("::error::e2e_receipt: не передан --input/--text — не измерено (rc=2)")
        return 2

    per_spec, totals = parse_list(text)
    if not per_spec:
        per_spec, totals = parse_json(text)
    if not per_spec:
        print("::error::e2e_receipt: вывод не разобран (нет ни list-строк, ни JSON) — "
              "не измерено, а не «зелено» (rc=2)")
        return 2

    if a.table:
        table, tp, tf, tsk = table_report(per_spec, totals, a.ref, a.repeat, a.workers)
        print(table)
        if a.summary_file:
            try:
                with open(a.summary_file, "a", encoding="utf-8") as fh:
                    fh.write("### front-e2e: частоты падений\n\n" + table + "\n\n")
            except OSError as e:  # noqa: BLE001
                print(f"::warning::e2e_receipt: не удалось дописать {a.summary_file}: {e}")
        if tf:
            print(f"::error::e2e_receipt: падений {tf} из {tp + tf}")
            return 1
        if tp + tf == 0:
            print(f"::error::e2e_receipt: ни один тест не исполнялся "
                  f"(passed 0, failed 0, skipped {tsk}) — не измерено (rc=2)")
            return 2
        print(f"::notice::e2e_receipt: падений нет — passed {tp} из {tp + tsk}")
        return 0

    line, failed, total = receipt(per_spec, totals, a.ref)
    print(line)
    for s in failed:
        print(f"::error::e2e_receipt: упавшая спека: {s}")
    if failed:
        return 1

    # Класс «прогон без шагов — не прогон» (AGENTS §5): если ВСЕ тесты пропущены условиями
    # стенда, набор «зелёный», но ничего не проверил. Это НЕ измерено (rc=2), а не успех.
    executed = totals.get('passed', 0) + totals.get('failed', 0)
    if executed == 0:
        print(f"::error::e2e_receipt: ни один тест не исполнялся "
              f"(passed 0, failed 0, skipped {totals.get('skipped', 0)}) \u2014 "
              f"\u00abпрогон без шагов \u2014 не прогон\u00bb: не измерено (rc=2)")
        return 2
    return 0 if total > 0 else 2


def selftest():
    ok = total = 0

    def case(name, text, want_rc, must=()):
        nonlocal ok, total
        total += 1
        per_spec, totals = parse_list(text)
        if not per_spec:
            per_spec, totals = parse_json(text)
        if not per_spec:
            rc, line = 2, "не разобран"
        else:
            line, failed, n = receipt(per_spec, totals, "abc123")
            executed = totals.get("passed", 0) + totals.get("failed", 0)
            rc = 1 if failed else (2 if (not n or executed == 0) else 0)
        miss = [m for m in must if m not in line]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, line={line[:120]}")

    list_ok = (
        "  \u2713  1 tests/a.spec.js:1:1 › первый (120ms)\n"
        "  \u2713  2 tests/a.spec.js:20:1 › второй (130ms)\n"
        "  -  3 tests/b.spec.js:5:1 › legacy (skipped)\n"
    )
    case("list: всё зелено + skip", list_ok, 0,
         ["passed 2", "failed 0", "skipped 1", "спек 2", "REF=abc123"])

    # Реалистичный вывод Playwright: ANSI-раскраска, CRLF, путь БЕЗ префикса `tests/`.
    # Раньше такой файл давал rc=2 «не измерено» — первый же CI-прогон не был бы прочитан.
    real_ansi = (
        "Running 4 tests using 1 worker",
        "\x1b[32m\u2713\x1b[39m  1 [chromium] \u203a onboarding.spec.js:9:1 \u203a E2E: onboarding (2.1s)",
        "\x1b[32m\u2713\x1b[39m  2 [chromium] \u203a onboarding.spec.js:60:1 \u203a E2E: profile (1.4s)",
        "\x1b[31m\u2717\x1b[39m  3 [chromium] \u203a partner-edit.spec.js:12:1 \u203a E2E: partner (30.0s)",
        "\x1b[33m-\x1b[39m  4 [chromium] \u203a change-pin.spec.js:8:1 \u203a E2E: pin",
    )
    case("real list: ANSI + CRLF + path without tests/", "\r\n".join(real_ansi), 1,
         ["passed 2", "failed 1", "skipped 1", "partner-edit.spec.js"])

    real_flaky = (
        "\u2718  1 [chromium] \u203a csv.spec.js:10:1 \u203a E2E: import (retry #1) (30.0s)",
        "\u2713  1 [chromium] \u203a csv.spec.js:10:1 \u203a E2E: import (retry #1) (2.0s)",
        "\u2713  2 [chromium] \u203a settings.spec.js:8:1 \u203a E2E: settings (1.1s)",
    )
    case("real list: retry -> flaky, not a failure", "\n".join(real_flaky), 0,
         ["failed 0", "flaky 1", "csv.spec.js"])

    real_skips = (
        "-  1 [chromium] \u203a change-pin.spec.js:8:1 \u203a E2E: pin",
        "-  2 [chromium] \u203a no-pin.spec.js:14:1 \u203a legacy",
    )
    # «Всё пропущено» — НЕ зелёный прогон: набор ничего не проверил, значит «не измерено».
    # TE-088: третий формат — то, что пишет `executed-guard.js` (`маркер  индекс › путь:строка › название`),
    # БЕЗ `[project]`. Раньше он не разбирался, и квитанция из артефакта давала rc=2 «не измерено».
    guard_ok = (
        "\u2713  1 \u203a /repo/9-tests/RnD/front-e2e/tests/a.spec.js:10 \u203a E2E: a",
        "\u2713  2 \u203a /repo/9-tests/RnD/front-e2e/tests/b.spec.js:42 \u203a E2E: b",
        "-  3 \u203a /repo/9-tests/RnD/front-e2e/tests/c.spec.js:7 \u203a E2E: c",
    )
    case("guard: без [project], путь абсолютный", "\n".join(guard_ok), 0,
         ["passed 2", "failed 0", "skipped 1", "спек 3"])

    guard_fail = "\n".join(list(guard_ok) + [
        "\u2717  4 \u203a /repo/9-tests/RnD/front-e2e/tests/d.spec.js:3 \u203a E2E: d",
    ])
    case("guard: одна упавшая видна", guard_fail, 1, ["failed 1", "d.spec.js"])

    case("real list: all skipped", "\n".join(real_skips), 2, ["skipped 2", "passed 0"])

    list_bad = list_ok + "  \u2718  4 tests/c.spec.js:7:1 › упал (5s)\n"
    case("list: одна упавшая", list_bad, 1, ["failed 1", "упавшие: tests/c.spec.js"])

    json_ok = json.dumps({"suites": [{"specs": [
        {"file": "tests/a.spec.js", "tests": [{"results": [{"status": "passed"}]}]},
        {"file": "tests/b.spec.js", "tests": [{"results": [{"status": "skipped"}]}]},
    ]}]})
    case("json: passed + skipped", json_ok, 0, ["passed 1", "skipped 1"])

    json_bad = json.dumps({"suites": [{"specs": [
        {"file": "tests/d.spec.js", "tests": [{"results": [{"status": "failed"}]}]},
    ]}]})
    case("json: failed", json_bad, 1, ["failed 1", "tests/d.spec.js"])

    flaky_out = (
        "  \u2713  1 [chromium] \u203a tests/a.spec.js:1:1 \u203a первый (1.2s)\n"
        "  \u2718  2 [chromium] \u203a tests/b.spec.js:5:1 \u203a второй (2.0s)\n"
        "  \u2713  2 [chromium] \u203a tests/b.spec.js:5:1 \u203a второй (retry #1) (1.1s)\n"
    )
    case("флейк: падение + успешный ретрай", flaky_out, 0,
         ["flaky 1", "нестабильные: tests/b.spec.js", "ретраев 1", "failed 0"])

    case("пустой вывод", "", 2, ["не разобран"])

    # Таблица для шага CI: суммы и наличие спеков
    total += 1
    import collections as _c
    per_spec = _c.OrderedDict([
        ("tests/a.spec.js", _c.Counter({"passed": 2})),
        ("tests/b.spec.js", _c.Counter({"failed": 1, "skipped": 1})),
    ])
    table, tp, tf, tsk = table_report(per_spec, _c.Counter(), "abc1234", "5", "1")
    if tp == 2 and tf == 1 and tsk == 1 and "| **итого** | **2** | **1** | **1** |" in table \
            and "repeat-each=5" in table and "REF=abc1234" in table:
        ok += 1
    else:
        print(f"  [FAIL] таблица: tp={tp} tf={tf} tsk={tsk}, table={table[:160]}")

    print(f"e2e_receipt --selftest: {ok}/{total} (упавшая спека = rc=1, неразобранный вывод = rc=2)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
