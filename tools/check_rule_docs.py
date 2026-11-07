# КОПИЯ из LoopMaker/check_rule_docs.py (источник правды там; правка — в обоих местах).
"""Линтер документации правил: чтобы в публичный репозиторий не утекли пути и токены.

Проверяет `*.md` (кроме чата и архива):
1. абсолютные домашние пути (`C:\\Users\\<имя>`, `/home/<имя>`, `/Users/<имя>`) —
   в публичном репозитории это раскрытие структуры машины и подсказка, где искать секреты;
2. похожее на токены (`ghp_`, `github_pat_`, `gho_`, `ghs_`) — даже фрагмент нельзя;
3. `push --force` в документированных командах — история не переписывается (§5.3).

    python tools/check_rule_docs.py            # из корня проекта
    python tools/check_rule_docs.py --report   # только показать
"""
import argparse
import glob
import os
import re
import subprocess
import sys

SKIP_DIRS = {"chat", "archive", ".git", "bin", "obj", "node_modules", ".obsidian"}

HOME_PATH = re.compile(r"[A-Za-z]:\\\\?Users\\\\?[A-Za-z0-9._-]+|/home/(?!(?:deploy|ubuntu|runner)\b)[A-Za-z0-9._-]+|/Users/[A-Za-z0-9._-]+")
TOKEN = re.compile(r"\b(ghp_|gho_|ghs_|ghu_|github_pat_)[A-Za-z0-9_]{6,}")
FORCE = re.compile(r"push[^\n]{0,40}--force(?!-with-lease)")


def md_files(root):
    """Файлы для проверки: ТОЛЬКО отслеживаемые git (2026-11-05).

    Раньше был обход `glob("**/*.md")`, и в скан попадали **неотслеживаемые** копии:
    в дереве появился вложенный worktree `origin/main` (545 md-файлов), из-за чего
    гейт показывал «файлов 1062, ошибок 33» — про чужую копию, а не про репозиторий.
    Гейт должен проверять то, что лежит **в репозитории**; распакованные архивы,
    временные worktree и выгрузки — не его предмет.

    Fallback на обход — если git недоступен (selftest во временном каталоге).
    """
    tracked = None
    try:
        out = subprocess.run(["git", "-c", "core.hooksPath=", "ls-files", "*.md"],
                             cwd=root, capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
        if out.returncode == 0 and out.stdout.strip():
            tracked = [os.path.join(root, rel.strip()) for rel in out.stdout.splitlines()
                       if rel.strip()]
    except Exception:  # noqa: BLE001
        tracked = None

    if tracked is not None:
        for path in tracked:
            parts = set(os.path.relpath(path, root).replace("\\", "/").split("/")[:-1])
            if parts & SKIP_DIRS:
                continue
            yield path
        return

    for path in glob.glob(os.path.join(root, "**", "*.md"), recursive=True):
        parts = set(os.path.relpath(path, root).replace("\\", "/").split("/")[:-1])
        if parts & SKIP_DIRS:
            continue
        if any(p in path.replace("\\", "/") for p in ("/bin/", "/obj/", "/node_modules/")):
            continue
        yield path


WARN_LIMIT = 30


def selftest():
    """DO-095: главный кейс — ошибка, стоящая в findings ПОСЛЕ лимита, обязана быть напечатана."""
    ok = total = 0

    def simulate(findings, show_all):
        errors = [f for f in findings if f[3] == "error"]
        warns = [f for f in findings if f[3] == "warn"]
        to_show = findings if show_all else errors + warns[:WARN_LIMIT]
        printed_errors = [f for f in to_show if f[3] == "error"]
        return len(errors), len(printed_errors), to_show

    many_warns = [("f%d.md" % i, i, "w", "warn") for i in range(40)]
    late_error = ("late.md", 1, "e", "error")
    findings = many_warns + [late_error]

    total += 1
    found, printed, _ = simulate(findings, show_all=False)
    if found == 1 and printed == 1:
        ok += 1
    else:
        print("  [FAIL] ошибка за лимитом не напечатана: найдено %d, напечатано %d"
              % (found, printed))

    total += 1
    found, printed, shown = simulate(findings, show_all=True)
    if found == printed == 1 and len(shown) == 41:
        ok += 1
    else:
        print("  [FAIL] --all: показано %d из %d, ошибок напечатано %d"
              % (len(shown), len(findings), printed))

    total += 1
    found, printed, shown = simulate(many_warns, show_all=False)
    if found == 0 and printed == 0 and len(shown) == WARN_LIMIT:
        ok += 1
    else:
        print("  [FAIL] лимит предупреждений: показано %d, ждали %d" % (len(shown), WARN_LIMIT))

    total += 1
    found, printed, _ = simulate([], show_all=False)
    if found == printed == 0:
        ok += 1
    else:
        print("  [FAIL] пустой набор дал непустой счётчик")

    print("check_rule_docs --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Линтер доков правил (пути и токены)")
    ap.add_argument("--root", default=None)
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--all", action="store_true",
                    help="показать все предупреждения (ошибки печатаются всегда)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()

    root = args.root or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    findings = []
    files = 0
    for path in md_files(root):
        files += 1
        rel = os.path.relpath(path, root).replace("\\", "/")
        with open(path, encoding="utf-8-sig") as f:
            for number, line in enumerate(f, 1):
                level = "warn" if rel.startswith("5-tech-backlog/") else "error"

                def add(what):
                    findings.append((rel, number, what, level))

                if HOME_PATH.search(line):
                    add("абсолютный домашний путь")
                if TOKEN.search(line):
                    add("похоже на токен (значение не печатаю)")
                banned = ("Запрещ", "запрещ", "нельзя", "не делаем", "не пушим")
                if (FORCE.search(line) and "--force-with-lease" not in line
                        and not any(w in line for w in banned)):
                    add("push --force")

    errors = [f for f in findings if f[3] == "error"]
    warns = [f for f in findings if f[3] == "warn"]

    # DO-095 (@TL 17:25): раньше печаталось `findings[:30]` — при 10 ошибках и 32
    # предупреждениях часть ошибок просто не попадала в вывод, а счётчик о них сообщал.
    # Красный гейт, не указывающий что чинить, — дефект гейта. Теперь:
    # ошибки печатаются ВСЕГДА и полностью, предупреждения — до лимита с явной пометкой.
    to_show = findings
    if not args.all:
        to_show = errors + warns[:WARN_LIMIT]
        shown_warns = min(len(warns), WARN_LIMIT)
    else:
        shown_warns = len(warns)

    print("check_rule_docs: файлов %d, ошибок %d, предупреждений %d"
          % (files, len(errors), len(warns)))
    for rel, number, what, level in to_show:
        print("  [%s] %s:%d — %s" % (level, rel, number, what))
    if not args.all and len(warns) > WARN_LIMIT:
        print("  … ещё %d предупреждений не показано (--all, чтобы увидеть все)"
              % (len(warns) - WARN_LIMIT))

    # инвариант: каждая ошибка обязана быть напечатана (иначе счётчик врёт)
    printed_errors = [f for f in to_show if f[3] == "error"]
    if len(printed_errors) != len(errors):
        print("::error::check_rule_docs: напечатано ошибок %d, а найдено %d — вывод неполный"
              % (len(printed_errors), len(errors)))
        return 1

    if errors and not args.report:
        print("")
        print("Правило: путь к хранилищу токена обезличиваем («личное хранилище роли, вне репозитория»),")
        print("значения секретов не цитируем, force-push запрещён (§5.3).")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
