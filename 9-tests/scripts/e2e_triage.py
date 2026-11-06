"""e2e_triage.py — разбирает лог прогона front-e2e на КЛАССЫ падений, а не по одной строке.

Зачем: прогон дал `18 failed, 3 skipped, 8 passed` (@DO 2026-11-06 02:34), и разбирать 18 аннотаций
руками — это часы. При этом класс падения определяет лечение, и классов всего несколько:

    test-timeout   «Test timeout of 90000ms exceeded»      → бюджет теста/хуков, а не утверждение
    wait-timeout   «locator.waitFor: Timeout 30000ms»      → не дождались элемента (вход/навигация/стенд)
    session-lost   ошибки `http 401/404` на /clients/me|journal → клиента удалили (очистка стенда/др. прогон)
    stand          `[infra]`, `stale-image`, 502/503/504    → стенд, не продукт и не спека
    assert         несовпадение ожидаемого (toBe/toHave/Expected) → дефект продукта или неверная спека
    other          всё остальное

Вход — файл с логом джобы (или `test-results/receipt.txt`). Разбор ищет аннотации `::error::` с JSON
(`title/status/steps/error`), которые печатает `reporters/notice.js`, и текст `Test timeout …`.

Коды: `0` — лог разобран; `2` — не разобран (нет аннотаций/файла).
"""

import argparse
import collections
import json
import os
import shutil
import tempfile
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

# GitHub переписывает `::error::`-команды в `##[error]` (то же для warning/notice):
# в скачанном логе джобы аннотаций в исходном виде НЕТ, и триаж по реальному логу
# сначала возвращал rc=2 «нет аннотаций». Поддерживаем оба формата.
ANNOTATION = re.compile(r"(?:::error::|##\[error\])(\{.*?\})\s*$")
CLASSES = (
    ("test-timeout", re.compile(r"test timeout of \d+\s*ms exceeded", re.I)),
    ("session-lost", re.compile(r"http (401|404).*(clients/me|journal)|(clients/me|journal).*(401|404)", re.I)),
    ("stand", re.compile(r"\[infra\]|stale-image|stand-unhealthy|\b50[234]\b", re.I)),
    ("wait-timeout", re.compile(r"timeout \d+\s*ms exceeded|locator\.\w+.*timeout|waitfor", re.I)),
    ("assert", re.compile(r"expect|tobe|tohave|expected|received", re.I)),
)
ADVICE = {
    "browser-noise": "шум диагностики браузера/фикстуры, а не отдельное падение — смотреть, к какому тесту относится",
    "test-timeout": "лимит теста израсходован: смотри длительности шагов; сокращать время входа/навигации, "
                    "а не увеличивать ожидания",
    "wait-timeout": "элемент не появился: проверь вход (фазы в логе), экран и стенд; это не «продукт ответил не то»",
    "session-lost": "сессию убили на стенде (401/404): очистка стенда или параллельный прогон; "
                    "фикстура делает один повтор с новым телефоном",
    "stand": "дефект стенда: поднимать/перегрев образа, не править тесты",
    "assert": "ожидание не совпало: дефект продукта ИЛИ неверное ожидание в спеке — смотреть данные",
    "other": "не классифицировано: смотреть полный текст аннотации",
}


STEP_DUR = re.compile(r"([^>]+?)\s*\((\d+(?:\.\d+)?)s\)")


def slowest_step(steps_text):
    """Самый долгий шаг из аннотации: `login по PIN (12.0s) > открыть (30.5s) [FAIL]` → ('открыть', 30.5)."""
    if not steps_text:
        return None
    found = [(m.group(1).strip(), float(m.group(2))) for m in STEP_DUR.finditer(steps_text)]
    found = [f for f in found if f[1] > 0]
    if not found:
        return None
    return max(found, key=lambda x: x[1])


def classify(text):
    low = text or ""
    for name, rx in CLASSES:
        if rx.search(low):
            return name
    return "other"


def classify_item(title, status, blobby):
    """Класс падения с приоритетом того, что реально мешает разбору.

    `browser http error` / `browser console.error`   это шум диагностики фикстуры, а не отдельное
    падение: в живом логе такого шума 90 записей против 19 падений тестов, и без разделения таблица
    нечитаема. Сами падения классифицируем по статусу и тексту ошибки.
    """
    t = (title or "").strip()
    if t.startswith("browser ") or t.startswith("storage snapshot") or t.startswith("views "):
        return "browser-noise"
    if (status or "").lower() in ("timedout", "interrupted") or "test timeout of" in (blobby or "").lower():
        return "test-timeout"
    if re.search(r"(http|status)[^0-9]{0,6}(5\d\d)", blobby or "", re.I):
        return "stand"
    if re.search(r"http (401|404).*(clients/me|journal)|(clients/me|journal).*(401|404)", blobby or "", re.I):
        return "session-lost"
    if re.search(r"expect|tobe|tohave|expected|received", blobby or "", re.I):
        return "assert"
    if re.search(r"timeout \d+\s*ms exceeded|locator\.\w+.*timeout|waitfor", blobby or "", re.I):
        return "wait-timeout"
    return "other"


def parse(log_text):
    """(items, classes). items = [(spec, title, status, klass, error)]."""
    items = []
    for line in log_text.splitlines():
        m = ANNOTATION.search(line.strip())
        if not m:
            continue
        try:
            payload = json.loads(m.group(1))
        except ValueError:
            payload = {"title": line.strip()[:160], "error": ""}
        title = str(payload.get("title") or "")
        err = str(payload.get("error") or "")
        steps = str(payload.get("steps") or "")
        spec = title.split(" › ")[0].strip() if " › " in title else title
        blobby = " ".join((title, steps, err))
        items.append((spec, title, payload.get("status") or "?", classify_item(title, payload.get("status"), blobby), err[:300], slowest_step(steps)))
    return items


def report(items):
    by_class = collections.defaultdict(list)
    for item in items:
        spec, title, status, klass, err = item[:5]
        slow = item[5] if len(item) > 5 else None
        by_class[klass].append((spec or title, err.splitlines()[0][:160] if err else "", slow))
    lines = ["| класс | сколько | примеры (спека) | самый долгий шаг | лечение |",
             "|---|---|---|---|---|"]
    for klass, _ in (("test-timeout", None), ("wait-timeout", None), ("session-lost", None),
                     ("stand", None), ("assert", None), ("other", None), ("browser-noise", None)):
        rows = by_class.get(klass) or []
        if not rows:
            continue
        specs = sorted({r[0] for r in rows if r[0]})[:4]
        # самый долгий шаг среди всех падений этого класса — где именно ушло время
        slow = [r[2] for r in rows if r[2]]
        slow_txt = f"{slow[0][0]} ({slow[0][1]:.1f}s)" if slow else "—"
        lines.append(f"| `{klass}` | {len(rows)} | {', '.join(specs)} | {slow_txt} | {ADVICE[klass]} |")
    return "\n".join(lines), by_class


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", help="файл лога джобы, test-results/receipt.txt ИЛИ каталог артефакта")
    ap.add_argument("--text", help="лог строкой (для проверки)")
    ap.add_argument("--json", action="store_true", help="вывести классы как JSON")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()
    if a.text is not None:
        text = a.text
    elif a.input:
        path = pathlib.Path(a.input)
        # Удобство для CI/артефакта: можно передать каталог артефакта (`front-e2e-results`
        # или распакованный `test-results/`) — файл квитанции найдётся сам.
        if path.is_dir():
            for cand in (path / "test-results" / "receipt.txt", path / "receipt.txt"):
                if cand.exists():
                    path = cand
                    break
            else:
                print(f"::error::e2e_triage: в каталоге {a.input} нет test-results/receipt.txt — "
                      f"не измерено (rc=2)")
                return 2
            print(f"::notice::e2e_triage: читаю {path}")
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError as e:
            print(f"::error::e2e_triage: не читается {path}: {e} — не измерено (rc=2)")
            return 2
    else:
        print("::error::e2e_triage: не передан --input/--text — не измерено (rc=2)")
        return 2

    items = parse(text)
    if not items:
        print("::error::e2e_triage: в логе нет аннотаций `::error::` — не измерено (rc=2)")
        return 2
    table, by_class = report(items)
    if a.json:
        print(json.dumps({k: len(v) for k, v in by_class.items()}, ensure_ascii=False))
        return 0
    print(table)
    print()
    print(f"итого аннотаций: {len(items)} | классов: {len(by_class)} | "
          f"по классам: " + ", ".join(f"{k}={len(v)}" for k, v in sorted(by_class.items())))
    return 0


def selftest():
    ok = total = 0

    def ann(title, status, steps, err):
        return "::error::" + json.dumps({"title": title, "status": status, "steps": steps, "error": err},
                                        ensure_ascii=False)

    LOG = "\n".join([
        "Running 4 tests using 1 worker",
        ann("partner-edit.spec.js › E2E: партнёр", "timedOut",
            "Before Hooks (90.0s) › login по PIN (60.0s)", "Test timeout of 90000ms exceeded."),
        ann("csv.spec.js › E2E: импорт", "failed",
            "login по PIN (12.0s) › открыть импорт (30.5s) [FAIL]", "locator.waitFor: Timeout 30000ms exceeded"),
        ann("onboarding.spec.js › E2E: онбординг", "failed",
            "login по PIN (5.0s)", "http 401 /api/v1/clients/me"),
        ann("catalog.spec.js › E2E: каталог", "failed",
            "проверка позиции (2.0s)", "expect(received).toBe(expected) Expected: 'x' Received: 'y'"),
    ])

    items = parse(LOG)
    total += 1
    if len(items) == 4:
        ok += 1
    else:
        print(f"  [FAIL] разбор: найдено {len(items)} из 4 аннотаций")

    got = collections.Counter(k for *_rest, k, _e in [(i[0], i[1], i[2], i[3], i[4]) for i in items])
    expect = {"test-timeout": 1, "wait-timeout": 1, "session-lost": 1, "assert": 1}
    total += 1
    if dict(got) == expect:
        ok += 1
    else:
        print(f"  [FAIL] классификация: {dict(got)} != {expect}")

    total += 1
    table, by_class = report(items)
    if "| `test-timeout` | 1 |" in table and "| `session-lost` | 1 |" in table and "лечение" in table:
        ok += 1
    else:
        print(f"  [FAIL] таблица: {table[:200]}")

    total += 1
    if parse("просто текст без аннотаций") == []:
        ok += 1
    else:
        print("  [FAIL] лог без аннотаций должен давать пустой список")

    # Каталог артефакта: файл квитанции находится автоматически
    total += 1
    tmp = pathlib.Path(tempfile.gettempdir()) / f"triage-art-{os.getpid()}"
    shutil.rmtree(tmp, ignore_errors=True)
    (tmp / "test-results").mkdir(parents=True)
    (tmp / "test-results" / "receipt.txt").write_text(
        ann("a.spec.js \u203a x", "failed", "login (12.0s)", "locator.waitFor: Timeout 30000ms exceeded"),
        encoding="utf-8",
    )
    try:
        if main(["--input", str(tmp)]) == 0:
            ok += 1
        else:
            print("  [FAIL] каталог артефакта не разобран")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    print(f"e2e_triage --selftest: {ok}/{total} "
          f"(классы: test-timeout/wait-timeout/session-lost/stand/assert/other)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
