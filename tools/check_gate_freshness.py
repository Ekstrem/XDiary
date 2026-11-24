#!/usr/bin/env python
"""check_gate_freshness.py — PR не должен делать гейты устаревшими (владелец 2026-11-21, вариант «а»).

Правило (владелец): удалил/переименовал файлы — обнови гейт, который ищет их паттерн, В ТОМ ЖЕ PR.

Живой случай (2026-11-21): BE переименовал `*CommandRepository.cs` → `*EventSourcingRepository.cs`
(`bb2b4b275`), а гейт `check_e2e_command_read_path.py` продолжил искать старый паттерн → находит
ноль файлов → rc=2 «не измерено» → `spec-gates` красил ЧУЖИЕ PR (например #3191, канвас архитектуры).
Невинный автор платил за рефактор, который прошёл раньше.

Логика гейта:
  1. Сканирует гейты в `tools/` и `9-tests/scripts/` (HEAD) и извлекает ЛИТЕРАЛЬНЫЕ паттерны
     файлов: glob-строки вида `6-msa/**/*FooRepository.cs` и точные пути вида
     `6-msa/BC-X/src/Y/ZRepository.cs`.
  2. Сравнивает совпадение паттернов с файлами репозитория на БАЗЕ (`--base`, по умолчанию
     `origin/main`) и в HEAD (оба — через `git ls-tree`, то есть по tracked-файлам).
  3. Совпадал на базе, а в HEAD — ни с одним файлом → ЭТОТ PR сделал гейт устаревшим → rc=1
     с точной строкой «гейт X, паттерн Y — обнови гейт в этом PR».
  4. Не совпадал уже и на базе → сталость внесена раньше → ::warning (не блок этого PR:
     вина не автора, а прежнего рефактора; сигнал владельцу зоны).

Ограничение честно: ловит только ЛИТЕРАЛЬНЫЕ паттерны в исходниках гейтов. Паттерны, собранные
динамически (f-строки, переменные), не извлекаются — такой случай покрывается правилом «(а)»
как процессом, а не гейтом.

Коды: 0 — новой сталости нет (предупреждения о прежней допустимы); 1 — этот PR делает гейт
устаревшим; 2 — не измерено (нет git, базового рефа или checkout без истории).

    python tools/check_gate_freshness.py [--base origin/main]
    python tools/check_gate_freshness.py --selftest
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

ROOT = pathlib.Path(__file__).resolve().parents[1]
GATE_DIRS = ("tools", "9-tests/scripts")
SELF_NAME = pathlib.Path(__file__).name

# расширения, по которым строку считаем паттерном файла
EXTS = (".cs", ".py", ".js", ".md", ".yaml", ".yml", ".sh", ".json", ".lock", ".txt", ".spec.js")

_STR = re.compile(r'["\']([^"\']{3,200})["\']')


def extract_patterns(text):
    """Литеральные паттерны файлов из исходника гейта.

    glob: строка содержит `*` и заканчивается известным расширением (или содержит его с `*`).
    literal: строка похожа на repo-путь (`a/b/c.ext`), без пробелов и подстановок.
    """
    out = []
    for m in _STR.finditer(text):
        s = m.group(1)
        if "://" in s or s.startswith("/") or " " in s:
            continue
        if any(ch in s for ch in "{}$%`"):
            continue  # динамика (f-строки, подстановки) — не извлекаем, честно
        if "\\" in s:
            continue
        has_glob = "*" in s
        if has_glob:
            if any(s.rstrip("*").endswith(e) or (e + "*") in s or (e + "?") in s for e in EXTS):
                out.append(s)
        elif "/" in s and any(s.endswith(e) for e in EXTS):
            out.append(s)
    return out


def glob_to_re(pat):
    """glob с `**` и `*` → regex (по tracked-путям, разделитель `/`)."""
    out, i = [], 0
    while i < len(pat):
        c = pat[i]
        if c == "*":
            if i + 1 < len(pat) and pat[i + 1] == "*":
                out.append(".*")
                i += 2
            else:
                out.append("[^/]*")
                i += 1
        elif c == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(c))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def matches(pat, files):
    """Совпадает ли паттерн хотя бы с одним tracked-файлом."""
    if "*" in pat or "?" in pat:
        rx = glob_to_re(pat)
        return any(rx.match(f) for f in files)
    return pat in files


def analyze(patterns, base_files, head_files):
    """(newly_stale, pre_stale): списки (гейт, паттерн)."""
    newly, pre = [], []
    for gate, pat in patterns:
        was, now = matches(pat, base_files), matches(pat, head_files)
        if was and not now:
            newly.append((gate, pat))
        elif not was and not now:
            pre.append((gate, pat))
    return newly, pre


def scan_gates(root):
    """(гейт_путь, паттерн) по всем гейтам в GATE_DIRS (рабочее дерево = HEAD)."""
    pats = []
    for d in GATE_DIRS:
        base = root / d
        if not base.is_dir():
            continue
        for f in sorted(base.rglob("*.py")) + sorted(base.rglob("*.js")):
            if f.name == SELF_NAME:
                continue
            try:
                text = f.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            rel = str(f.relative_to(root)).replace("\\", "/")
            for pat in extract_patterns(text):
                pats.append((rel, pat))
    return pats


def _ls_tree(ref, cwd):
    r = subprocess.run(["git", "-C", str(cwd), "ls-tree", "-r", "--name-only", ref],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        return None
    return [l.strip() for l in r.stdout.splitlines() if l.strip()]


def receipt(newly, pre, total):
    return (f"квитанция: паттернов в гейтах {total} | устарели ЭТИМ PR: {len(newly)} | "
            f"устарели раньше (предупреждение): {len(pre)}")


def selftest():
    ok = total = 0

    def case(name, cond):
        nonlocal ok, total
        total += 1
        if cond:
            ok += 1
        else:
            print(f"  [FAIL] {name}")

    pats = extract_patterns(
        'tracked_or_walk(root, "6-msa/**/*FooRepository.cs")\n'
        'KNOWN = {"6-msa/BC-X/src/Y/JournalFooRepository.cs": "описание"}\n'
        'url = "https://example.com/x.py"\n'
        'dyn = f"6-msa/**/{name}.cs"\n'
        'cmd = "podman run -d --replace"\n')
    case("extract: glob + literal, пропуск url/dyn/cmd",
         "6-msa/**/*FooRepository.cs" in pats
         and "6-msa/BC-X/src/Y/JournalFooRepository.cs" in pats
         and all("example.com" not in p and "{name}" not in p and "podman" not in p for p in pats))

    rx = glob_to_re("6-msa/**/*FooRepository.cs")
    case("glob: `**` совпадает с вложенным путём",
         bool(rx.match("6-msa/BC-X/src/Y/JournalFooRepository.cs")))
    case("glob: `*` не пересекает `/`",
         not glob_to_re("6-msa/*FooRepository.cs").match("6-msa/BC-X/src/JournalFooRepository.cs"))
    case("glob: чужой суффикс не совпадает",
         not rx.match("6-msa/BC-X/src/Y/JournalBarRepository.cs"))

    base_files = ["6-msa/BC-X/src/Y/JournalFooRepository.cs", "tools/a.py"]
    head_files = ["6-msa/BC-X/src/Y/JournalFooSourcing.cs", "tools/a.py"]
    gate = [("9-tests/scripts/g.py", "6-msa/**/*FooRepository.cs"),
            ("tools/t.py", "tools/a.py")]
    newly, pre = analyze(gate, base_files, head_files)
    case("новая сталость: glob совпадал на базе и не совпадает в HEAD",
         newly == [("9-tests/scripts/g.py", "6-msa/**/*FooRepository.cs")] and not pre)
    case("literal-файл на месте — не сталость",
         ("tools/t.py", "tools/a.py") not in newly)

    newly2, pre2 = analyze(gate, [], head_files)
    case("прежняя сталость (на базе уже 0) — предупреждение, не блок",
         not newly2 and pre2 == [("9-tests/scripts/g.py", "6-msa/**/*FooRepository.cs")])

    line = receipt([("g", "p")], [("g2", "p2")], 5)
    case("квитанция содержит оба числа", "устарели ЭТИМ PR: 1" in line and "устарели раньше" in line)

    print(f"check_gate_freshness --selftest: {ok}/{total}")
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="origin/main",
                    help="базовый реф для сравнения (в CI — merge-base или origin/main)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    head_files = _ls_tree("HEAD", ROOT)
    base_files = _ls_tree(a.base, ROOT)
    if head_files is None or base_files is None:
        print(f"::error::check_gate_freshness: не измерено — git ls-tree не сработал "
              f"(HEAD={'ок' if head_files is not None else 'нет'}, base={a.base}="
              f"{'ок' if base_files is not None else 'нет'}). Нужен checkout с историей "
              f"(fetch-depth: 0) и реф {a.base}. (rc=2)")
        return 2

    patterns = scan_gates(ROOT)
    newly, pre = analyze(patterns, base_files, head_files)
    for gate, pat in newly:
        print(f"::error::check_gate_freshness: {gate}: паттерн «{pat}» совпадал с файлами на "
              f"{a.base}, а в этом PR — ни с одним. Ты переименовал/удалил файлы, которые гейт "
              f"ищет, — обнови гейт В ЭТОМ ЖЕ PR (вариант «а», владелец 2026-11-21).")
    for gate, pat in pre:
        print(f"::warning::check_gate_freshness: {gate}: паттерн «{pat}» уже не совпадает ни с "
              f"одним файлом (сталость внесена раньше, не этим PR) — скажи владельцу зоны, "
              f"не блок этого PR.")
    print(receipt(newly, pre, len(patterns)))
    return 1 if newly else 0


if __name__ == "__main__":
    sys.exit(main())
