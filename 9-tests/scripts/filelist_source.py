"""filelist_source.py — источник списка файлов для гейтов: `git ls-files`, а `walk` только фолбэк.

Зачем (норма, @AR `2026-11-06 23:30`): гейт, который **считает файлы**, не должен зависеть от того, что лежит
в рабочем каталоге. Обход `rglob`/`glob.glob` видит **вложенный worktree**, скопированные «на минутку» копии и
любой мусор — и вердикт получается разный у автора и у соседа («у меня зелено, у тебя красно»). Сегодня этот
класс поймали пять раз: `check_rule_docs`, `check_docs_size`, `check_canon`, и два моих гейта.

Правило:

1. список берётся из **`git ls-files`** (то, что реально в репозитории);
2. `walk` — только фолбэк, когда `git` недоступен или каталог вне git-корня;
3. **источник печатается в квитанции** (`git ls-files (N)` или `walk (N)`) — чтобы вердикт нельзя было получить
   на другом дереве незаметно.

API:

    from filelist_source import tracked_or_walk
    paths, source = tracked_or_walk(root, "6-msa/**/*CommandRepository.cs")
    print(f"источник: {source}")

`source` — строка вида `git ls-files (12)` / `walk (12)`; её место — в квитанции гейта.
"""

import pathlib
import subprocess


def _git(root, *args):
    try:
        out = subprocess.run(["git", "-C", str(root), *args], capture_output=True,
                             text=True, encoding="utf-8", errors="replace")
        if out.returncode != 0:
            return None
        return out.stdout
    except (OSError, ValueError):
        return None


def tracked_or_walk(root, pathspec):
    """(список путей, 'git ls-files (N)' | 'walk (N)').

    `pathspec` — путь-шаблон для git (`6-msa/**/*.cs`) и одновременно шаблон для `rglob` в фолбэке.
    Порядок путей стабильный (сортировка), чтобы квитанции не «прыгали».
    """
    root = pathlib.Path(root)
    listed = _git(root, "ls-files", "-z", "--", pathspec)
    if listed is not None:
        paths = [p for p in listed.split("\0") if p.strip()]
        if paths:
            return sorted((root / p for p in paths), key=lambda p: str(p)), f"git ls-files ({len(paths)})"

    # фолбэк: walk по тому же шаблону (например, когда гейт запускают вне git-корня)
    pattern = pathspec.split("**/", 1)[-1] if "**/" in pathspec else pathspec
    base = pathspec.split("**/", 1)[0] if "**/" in pathspec else "."
    base_path = root / base
    found = sorted(base_path.rglob(pattern), key=lambda p: str(p)) if base_path.is_dir() else []
    return found, f"walk ({len(found)})"


def tracked_only(root, pathspec):
    """Строгая форма: только `git ls-files`. None, если git недоступен (гейт должен сказать «не измерено»)."""
    root = pathlib.Path(root)
    listed = _git(root, "ls-files", "-z", "--", pathspec)
    if listed is None:
        return None, "git ls-files недоступен"
    paths = [p for p in listed.split("\0") if p.strip()]
    return sorted((root / p for p in paths), key=lambda p: str(p)), f"git ls-files ({len(paths)})"


def selftest():
    """Проверка принципа: список из git ls-files, а не из дерева.

    Сценарий, ради которого всё делалось: в рабочем каталоге лежит **копия** файла, которой нет в git
    (то же самое, что вложенный worktree или «скопировал на минутку»). Гейт, читающий `git ls-files`,
    её не видит; гейт, обходящий дерево, — видит, и вердикт расходится с репозиторием.
    """
    import os
    import shutil
    import tempfile

    ok = total = 0
    root = pathlib.Path(tempfile.gettempdir()) / ("flsrc-" + str(os.getpid()))
    shutil.rmtree(root, ignore_errors=True)
    (root / "6-msa" / "BC-X").mkdir(parents=True)
    (root / "6-msa" / "BC-X" / "Tracked.cs").write_text("class A {}", encoding="utf-8")

    env_ok = True
    for args in (("init", "-q"), ("add", "-A")):
        r = subprocess.run(["git", "-C", str(root), *args], capture_output=True)
        if r.returncode != 0:
            env_ok = False

    total += 1
    if env_ok:
        (root / "6-msa" / "BC-X" / "Untracked.cs").write_text("class B {}", encoding="utf-8")
        paths, src = tracked_or_walk(root, "6-msa/**/*.cs")
        names = sorted(p.name for p in paths)
        if names == ["Tracked.cs"] and src.startswith("git ls-files"):
            ok += 1
        else:
            print(f"  [FAIL] неотслеживаемая копия попала в список: {names} | источник {src}")
    else:
        print("  [SKIP] git недоступен — проверка источника невозможна")
        ok += 1

    total += 1
    paths2, src2 = tracked_or_walk(root, "no-such-dir/**/*.cs")
    if not paths2 and src2.startswith("walk"):
        ok += 1
    else:
        print(f"  [FAIL] пустой путь: {paths2} | {src2}")

    total += 1
    paths3, src3 = tracked_only(root, "6-msa/**/*.cs")
    if paths3 and src3.startswith("git ls-files"):
        ok += 1
    else:
        print(f"  [FAIL] tracked_only: {paths3} | {src3}")

    shutil.rmtree(root, ignore_errors=True)
    print(f"filelist_source --selftest: {ok}/{total} "
          f"(список берётся из git ls-files: неотслеживаемая копия не меняет вердикт)")
    return 0 if ok == total else 1


if __name__ == "__main__":
    import sys

    sys.exit(selftest())
