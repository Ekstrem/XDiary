"""check_e2e_mvp_trace.py — каждая MVP-история связана со спекой (и это видно машинно).

Зачем: `check_e2e_traceability.py` проверяет обратную сторону — у спеки есть TE-карточка. Но связь
«история пользователя → спека» не проверялась ничем, и замер это подтвердил: в бэклоге **12** MVP-историй
(`US-01…US-12`), а в спеках не было **ни одной** ссылки на них. То есть нельзя было ответить на вопрос
«какая спека закрывает US-05?» иначе как глазами по карточкам.

Правило: каждая история из MVP-инкрементов (`01-user-stories/backlog.md`, разделы с `(MVP)`) обязана
упоминаться хотя бы в одной спеке в строке `MVP-истории:`. Частичное покрытие разрешено, но **явно**:
история помечается словом `ЧАСТИЧНО` в комментарии спеки и попадает в список «частично» в квитанции —
гейт не считает частичное покрытием «всё хорошо», но и не молчит.

Коды: `0` — все MVP-истории связаны; `1` — есть несвязанные; `2` — не измерено.
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

US = re.compile(r"\b(US-\d+)\b")
LINK_LINE = re.compile(r"MVP-истории:\s*(.+)")
PARTIAL = "ЧАСТИЧНО"


def mvp_stories(backlog_text):
    """Истории из разделов `## … (MVP)`."""
    out, cur = [], False
    for line in backlog_text.splitlines():
        if line.startswith("## "):
            cur = "(MVP)" in line
            continue
        if cur:
            out.extend(US.findall(line))
    return sorted(set(out), key=lambda x: int(x.split("-")[1]))


def spec_links(specs):
    """{US-id: [имена спек]}, плюс отдельно частичные."""
    full, partial = {}, {}
    for name, text in specs.items():
        for line in text.splitlines():
            m = LINK_LINE.search(line)
            if not m:
                continue
            for us in US.findall(line):
                target = partial if PARTIAL in line else full
                target.setdefault(us, []).append(name)
    return full, partial


def evaluate(backlog_text, specs):
    """(issues, stats)."""
    stories = mvp_stories(backlog_text)
    full, partial = spec_links(specs)
    issues, missing, only_partial = [], [], []
    for us in stories:
        if us in full:
            continue
        if us in partial:
            only_partial.append(us)
            continue
        missing.append(us)
        issues.append(f"MVP-история `{us}` не связана ни с одной спекой — нельзя сказать, "
                      f"чем она проверяется (добавьте строку `MVP-истории: {us} …` в шапку спеки)")
    stats = {"stories": len(stories), "linked": len(stories) - len(missing) - len(only_partial),
             "partial": len(only_partial), "missing": len(missing),
             "partial_list": only_partial, "missing_list": missing}
    return issues, stats


def receipt_line(stats, issues):
    partial = (" [" + ", ".join(stats["partial_list"]) + "]") if stats["partial_list"] else ""
    return (f"квитанция: MVP-историй {stats['stories']} | связаны полностью {stats['linked']} | "
            f"частично {stats['partial']}{partial} | без связи {stats['missing']} | нарушено: {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = pathlib.Path(a.root)
    backlog = root / "01-user-stories" / "backlog.md"
    tests_dir = root / "9-tests" / "RnD" / "front-e2e" / "tests"
    if not backlog.exists() or not tests_dir.is_dir():
        print("::error::check_e2e_mvp_trace: нет бэклога или спек — не измерено (rc=2)")
        return 2
    try:
        backlog_text = backlog.read_text(encoding="utf-8", errors="replace")
        specs = {p.name: p.read_text(encoding="utf-8", errors="replace")
                 for p in sorted(tests_dir.glob("*.spec.js"))}
    except OSError as e:
        print(f"::error::check_e2e_mvp_trace: не читается вход: {e} — не измерено (rc=2)")
        return 2
    stories = mvp_stories(backlog_text)
    if not stories:
        print("::error::check_e2e_mvp_trace: в бэклоге нет разделов `(MVP)` — не измерено (rc=2)")
        return 2
    issues, stats = evaluate(backlog_text, specs)
    for m in issues:
        print(f"::error::check_e2e_mvp_trace: {m}")
    print(receipt_line(stats, issues))
    return 1 if issues else 0


def selftest():
    ok = total = 0
    BACKLOG = (
        "## Инкремент 1 — Журнал (MVP)\n"
        "- **US-01** создать запись\n"
        "- **US-02** указать партнёра\n"
        "## Инкремент 3 — Соматика (TOBE)\n"
        "- **G-01** подключить источник\n"
    )

    def case(name, backlog, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats = evaluate(backlog, specs)
        text = "\n".join(issues) + "\n" + receipt_line(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}, text={text[:170]}")

    case("обе истории связаны", BACKLOG, {
        "a.spec.js": "// MVP-истории: US-01 (создание)\n",
        "b.spec.js": "// MVP-истории: US-02 (партнёр)\n",
    }, 0, ["связаны полностью 2", "нарушено: 0"])

    case("история без связи", BACKLOG, {"a.spec.js": "// MVP-истории: US-01 (создание)\n"}, 1,
         ["US-02", "не связана ни с одной спекой"])

    case("история TOBE не требуется", BACKLOG, {
        "a.spec.js": "// MVP-истории: US-01, US-02\n",
    }, 0, ["MVP-историй 2", "нарушено: 0"])

    case("частичное покрытие помечено", BACKLOG, {
        "a.spec.js": "// MVP-истории: US-01, US-02 — ЧАСТИЧНО: только офлайн-чтение\n",
    }, 0, ["частично 2", "без связи 0"])

    total += 1
    line = receipt_line({"stories": 3, "linked": 2, "partial": 1, "missing": 0,
                         "partial_list": ["US-10"], "missing_list": []}, [])
    if "MVP-историй 3" in line and "частично 1 [US-10]" in line and "None" not in line:
        ok += 1
    else:
        print(f"  [FAIL] текст квитанции: {line}")

    print(f"check_e2e_mvp_trace --selftest: {ok}/{total} (MVP-история без спеки = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
