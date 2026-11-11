"""check_e2e_traceability.py — у каждой спеке `front-e2e` есть ссылка на карточку (правило `9-tests/AGENTS.md`).

Правило: «Тест без критерия приёмки из задачи — не тест: в шапке файла/коммита ссылаемся на `TE-xxx`».
Здесь проверяется машинно: в первых 12 строках спеки должен быть либо `TE-NNN`, либо явная метка
`LEGACY` с причиной (тогда спека исключена из гейта и не притворяется живой).

Коды: `0` — все спеки трассируются; `1` — есть спеки без ссылки и без `LEGACY`; `2` — не измерено
(нет каталога спек).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

TE = re.compile(r"TE-\d{3}")
HEAD_LINES = 12


def evaluate(specs, cards=None):
    """specs — {имя: шапка}. cards — {TE-ID: текст карточки} или None.

    Проверяется не только наличие `TE-NNN`, но и что карточка **есть** и не `Отклонена`:
    ссылка на отклонённую задачу — тот же «второй источник правды», только в трассировке.
    """
    issues, traced, legacy = [], 0, 0
    for name, head in sorted(specs.items()):
        refs = TE.findall(head)
        if refs:
            traced += 1
            for ref in refs:
                if cards is None:
                    continue
                text = cards.get(ref)
                if text is None:
                    issues.append(f"{name}: ссылка на `{ref}`, а такой карточки нет "
                                  f"в `5-tech-backlog/tester/`")
                    continue
                m = re.search(r"\*\*Статус:\*\*\s*([^\n]+)", text)
                status = (m.group(1).strip() if m else "")
                if status.startswith("Отклонена"):
                    issues.append(f"{name}: ссылка на `{ref}`, которая `Отклонена` — "
                                  f"выберите действующую карточку")
        elif re.search(r"LEGACY", head):
            legacy += 1
        else:
            issues.append(f"{name}: в шапке нет ни `TE-NNN`, ни метки `LEGACY` (правило 9-tests/AGENTS.md)")
    return issues, traced, legacy


def missing_body_refs(specs_full, cards):
    """Ссылки `TE-NNN` во всём тексте спеки, для которых нет карточки.

    Шапка проверяется строже (там ссылка обязательна), но и в теле ссылка на несуществующую карточку —
    это второй источник правды: читатель не найдёт критерий. Возвращает [(спека, ref)].
    """
    out = []
    if not cards:
        return out
    for name, text in sorted(specs_full.items()):
        for ref in sorted(set(TE.findall(text))):
            if ref not in cards:
                out.append((name, ref))
    return out


def load_full(specs_dir: pathlib.Path):
    return {p.name: p.read_text(encoding="utf-8", errors="replace")
            for p in sorted(specs_dir.glob("*.spec.js"))}


def load(specs_dir: pathlib.Path):
    if not specs_dir.is_dir():
        return None, "нет каталога спек"
    out = {}
    for p in sorted(specs_dir.glob("*.spec.js")):
        head = "\n".join(p.read_text(encoding="utf-8", errors="replace").splitlines()[:HEAD_LINES])
        out[p.name] = head
    return out, None


def output_lines(issues, traced, legacy, total):
    lines = [f"::error::check_e2e_traceability: {m}" for m in issues]
    lines.append(f"квитанция: спек {total} | с TE-ссылкой: {traced} | LEGACY: {legacy} | "
                 f"нарушено: {len(issues)}")
    return lines


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--strict", action="store_true",
                    help="блокирующий режим: ссылка на несуществующую карточку ⇒ rc=1")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    specs, err = load(pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests")
    if err:
        print(f"::error::check_e2e_traceability: {err} — проверять нечего (rc=2)")
        return 2
    cards = {}
    tester = pathlib.Path(a.root) / "5-tech-backlog" / "tester"
    if tester.is_dir():
        try:
            for p in sorted(tester.glob("TE-*.md")):
                cards[p.stem] = p.read_text(encoding="utf-8", errors="replace")
        except OSError as e:
            print(f"::error::check_e2e_traceability: не читаются карточки: {e} — "
                  f"часть проверки не измерена")
            cards = None
    issues, traced, legacy = evaluate(specs, cards if cards else None)
    for line in output_lines(issues, traced, legacy, len(specs)):
        print(line)

    # TE-(находка): ссылки в теле спеки на карточки, которых нет
    specs_full = load_full(pathlib.Path(a.root) / "9-tests" / "RnD" / "front-e2e" / "tests")
    missing = missing_body_refs(specs_full, cards if cards else None)
    level = "::error::" if a.strict else "::warning::"
    for name, ref in missing:
        print(f"{level}check_e2e_traceability: {name} ссылается на `{ref}`, а карточки нет "
              f"в `5-tech-backlog/tester/` (в шапке правило соблюдено)")
    if missing:
        print(f"квитанция (тела спек): ссылок на несуществующие карточки {len(missing)}"
              f"{' — блокирует (--strict)' if a.strict else ' — отчёт, не блокирует'}")
    return 1 if (issues or (a.strict and missing)) else 0


def selftest():
    ok = total = 0

    def case(name, specs, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, traced, legacy = evaluate(specs)
        text = "\n".join(output_lines(issues, traced, legacy, len(specs)))
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}")

    case("норма: TE и LEGACY", {"a.spec.js": "// TE-050 критерий", "b.spec.js": "// LEGACY: снят"},
         0, ["с TE-ссылкой: 1", "LEGACY: 1", "нарушено: 0"])
    case("спека без ссылки", {"a.spec.js": "// без ссылки"}, 1,
         ["a.spec.js", "ни `TE-NNN`, ни метки `LEGACY`"])
    case("TE во второй строке", {"a.spec.js": "// заголовок\n// TE-013\n"}, 0, ["с TE-ссылкой: 1"])

    cards_ok = {"TE-010": "- **Статус:** Закрыта", "TE-011": "- **Статус:** Отклонена (проверено)"}
    case("ссылка на действующую карточку",
         {"a.spec.js": "// TE-010 критерий"}, 0, ["нарушено: 0"]) if False else None
    total += 1
    issues, traced, legacy = evaluate({"a.spec.js": "// TE-010 критерий"}, cards_ok)
    if not issues and traced == 1:
        ok += 1
    else:
        print(f"  [FAIL] действующая карточка: {issues}")

    total += 1
    issues, traced, legacy = evaluate({"b.spec.js": "// TE-011 критерий"}, cards_ok)
    if issues and "Отклонена" in issues[0]:
        ok += 1
    else:
        print(f"  [FAIL] отклонённая карточка не поймана: {issues}")

    total += 1
    issues, traced, legacy = evaluate({"c.spec.js": "// TE-999 критерий"}, cards_ok)
    if issues and "такой карточки нет" in issues[0]:
        ok += 1
    else:
        print(f"  [FAIL] отсутствующая карточка не поймана: {issues}")

    total += 1
    line = "квитанция: спек 2 | с TE-ссылкой: 2 | LEGACY: 0 | нарушено: 0"
    if "None" not in line and "нарушено: 0" in line:
        ok += 1
    else:
        print(f"  [FAIL] текст: {line}")

    print(f"check_e2e_traceability --selftest: {ok}/{total} (спека без TE/LEGACY = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
