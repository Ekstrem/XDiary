"""check_e2e_filesource.py — мета-гейт: обход дерева в гейте должен иметь источник `git ls-files`.

Норма (@AR `2026-11-06 23:30`): **гейт, который считает файлы, берёт список из `git ls-files`**; обход деревом
(`rglob`/`glob.glob`/`iterdir`) — только фолбэк, и в квитанции печатается **источник** (`git ls-files (N)` /
`walk (N)`). Иначе вердикт зависит от того, что лежит в рабочем каталоге: вложенный worktree, скопированная
«на минутку» копия, мусор — и у автора зелено, у соседа красно.

За день этот класс поймали пять раз: `check_rule_docs`, `check_docs_size`, `check_canon`, и два моих гейта
(`check_e2e_command_read_path`, `check_e2e_eventsourcing_contract`). Мета-гейт нужен, чтобы не появился шестой.

Что проверяется (для каждого `9-tests/scripts/check_e2e_*.py`):

- есть ли обход дерева (`rglob(`, `glob.glob(`, `glob.iglob(`, `iterdir(`);
- если есть — в файле должен быть **источник**: импорт `filelist_source` / вызов `tracked_or_walk(` /
  `tracked_only(` / `git ls-files`, либо `_skip_nested_repos(` как явный фолбэк;
- всё это видно в квитанции: `файлов с обходом N | с источником M | без источника 0`.

Коды: `0` — все обходы имеют источник; `1` — есть файл с обходом без источника; `2` — нет каталога скриптов
(не измерено).
"""

import argparse
import pathlib
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

WALK = re.compile(r"\b(rglob\(|glob\.glob\(|glob\.iglob\(|iterdir\(|os\.walk\()")
SOURCES = (
    "filelist_source",
    "tracked_or_walk(",
    "tracked_only(",
    "git ls-files",
    "_skip_nested_repos(",
)


def scan(scripts_dir):
    """[(файл, [строки с обходом])] для гейтов, где обход есть."""
    out = []
    me = pathlib.Path(__file__).name
    for p in sorted(pathlib.Path(scripts_dir).glob("check_e2e_*.py")):
        if p.name == me:
            continue  # в этом файле обходы встречаются в шаблонах и selftest-строках — это не гейт-обход
        text = p.read_text(encoding="utf-8", errors="replace")
        lines = text.splitlines()
        walks = [(i + 1, l.strip()) for i, l in enumerate(lines) if WALK.search(l)]
        if walks:
            out.append((p, text, walks))
    return out


def evaluate(scripts_dir):
    """(issues, stats) — по всем гейтам с обходом дерева."""
    found = scan(scripts_dir)
    issues = []
    with_source = 0
    for p, text, walks in found:
        has_source = any(s in text for s in SOURCES)
        if has_source:
            with_source += 1
        else:
            for lineno, line in walks:
                issues.append(f"{p.name}:{lineno} — обход дерева без источника "
                              f"(нет `git ls-files`/`tracked_or_walk`/`_skip_nested_repos`): `{line[:60]}`")
    stats = {"gates": len(found), "with_source": with_source, "walks": sum(len(w) for _p, _t, w in found)}
    return issues, stats, found


def receipt(stats, issues):
    return (f"квитанция: файлов с обходом {stats['gates']} | с источником {stats['with_source']} | "
            f"обходов деревом {stats['walks']} | без источника {len(issues)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--scripts", default=str(pathlib.Path(__file__).resolve().parent))
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    d = pathlib.Path(a.scripts)
    if not d.is_dir():
        print(f"::error::check_e2e_filesource: нет каталога {d} — не измерено (rc=2)")
        return 2
    issues, stats, found = evaluate(d)
    for m in issues:
        print(f"::error::check_e2e_filesource: {m}")
    for p, _text, walks in found:
        print(f"::notice::check_e2e_filesource: {p.name} — обходов {len(walks)} "
              f"(источник {'есть' if any(s in _text for s in SOURCES) else 'НЕТ'})")
    print(receipt(stats, issues))
    return 1 if issues else 0


def selftest():
    import os
    import shutil
    import tempfile

    ok = total = 0
    root = pathlib.Path(tempfile.gettempdir()) / ("filesrc-" + str(os.getpid()))
    shutil.rmtree(root, ignore_errors=True)
    root.mkdir(parents=True)

    def write(name, body):
        (root / name).write_text(body, encoding="utf-8")

    def case(name, want_rc, must=()):
        nonlocal ok, total
        total += 1
        issues, stats, _found = evaluate(root)
        text = "\n".join(issues) + "\n" + receipt(stats, issues)
        rc = 1 if issues else 0
        miss = [m for m in must if m not in text]
        if rc == want_rc and not miss:
            ok += 1
        else:
            print(f"  [FAIL] {name}: rc={rc} (ждали {want_rc}), нет {miss}; text={text[:200]}")

    write("check_e2e_bad.py", "for p in base.rglob('*.cs'):\n    pass\n")
    case("обход без источника — красный", 1, ["без источника", "check_e2e_bad.py:1"])

    write("check_e2e_bad.py", "from filelist_source import tracked_or_walk\n"
                              "paths, src = tracked_or_walk(root, '6-msa/**/*.cs')\n")
    case("источник через filelist_source — зелёный", 0, ["без источника 0"])

    write("check_e2e_skip.py", "for p in _skip_nested_repos(root.rglob('*.cs'), root):\n    pass\n")
    case("явный фолбэк `_skip_nested_repos` принимается", 0, ["с источником"])

    write("check_e2e_glob.py", "import glob\nfor p in glob.glob('6-msa/**/*.cs', recursive=True):\n    pass\n")
    case("glob.glob без источника — красный", 1, ["check_e2e_glob.py:2"])

    write("other.py", "for p in root.rglob('*.cs'):\n    pass\n")
    (root / "check_e2e_bad.py").unlink()
    (root / "check_e2e_skip.py").unlink()
    (root / "check_e2e_glob.py").unlink()
    case("не-гейт не проверяется", 0, ["файлов с обходом 0"])

    shutil.rmtree(root, ignore_errors=True)
    print(f"check_e2e_filesource --selftest: {ok}/{total} "
          f"(обход дерева без `git ls-files`/`tracked_or_walk` = rc=1)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    sys.exit(main())
