# КОПИЯ из LoopMaker/check_docs_size.py (источник правды там; правка — в обоих местах).
"""Гейт бюджета инструкций: сумма AGENTS.md ≤ 64 КиБ, каждый ≤ 16 КиБ."""
import argparse
import glob
import subprocess
import json
import os
import sys

CONFIG = "loop.config.json"
DEFAULT_TOTAL = 65536
DEFAULT_PER_FILE = 16384


def selftest():
    """Самопроверка (норма @TL: гейт печатает числа ⇒ кейс на текст с негативным контролем)."""
    import shutil
    import tempfile
    ok = total = 0
    # временный каталог — ВНУТРИ воркспейса: песочница не даёт писать в %TEMP%
    base = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        ".tmp-docs-size")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    try:
        def case(name, sizes, want_rc, must_text=None):
            nonlocal ok, total
            total += 1
            d = os.path.join(base, name)
            os.makedirs(d, exist_ok=True)
            for idx, size in enumerate(sizes):
                path = os.path.join(d, "AGENTS.md" if idx == 0 else "sub%d" % idx, "AGENTS.md")
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "wb") as fh:
                    fh.write(b"x" * size)
            out = subprocess.run([sys.executable, os.path.abspath(__file__),
                                  "--root", d, "--total", "1000", "--per-file", "600"],
                                 capture_output=True, text=True, encoding="utf-8",
                                 errors="replace")
            text = (out.stdout or "") + (out.stderr or "")
            good = out.returncode == want_rc and (must_text is None or must_text in text)
            if good:
                ok += 1
            else:
                print("  [FAIL] %s: rc=%d (ждали %d), текст: %r"
                      % (name, out.returncode, want_rc, text.strip()[:120]))

        case("under", [400], 0, "в пределах лимита")
        case("over_total", [800, 800], 1, "превышение")
        case("over_file", [700], 1)
    finally:
        shutil.rmtree(base, ignore_errors=True)

    print("check_docs_size --selftest: %d/%d" % (ok, total))
    return 0 if ok == total else 1


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Бюджет инструкций (AGENTS.md)")
    ap.add_argument("--root", default=None)
    ap.add_argument("--config", default=None)
    ap.add_argument("--total", type=int, default=None)
    ap.add_argument("--per-file", type=int, default=None)
    ap.add_argument("--selftest", action="store_true",
                    help="самопроверка: в пределах лимита -> 0, превышение -> 1, текст с числами")
    args = ap.parse_args()
    if args.selftest:
        return selftest()

    root = args.root or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    cfg_path = args.config or os.path.join(root, CONFIG)
    cfg = {}
    if os.path.exists(cfg_path):
        with open(cfg_path, encoding="utf-8-sig") as f:
            cfg = json.load(f)
    budget = cfg.get("docs_budget") or {}
    total_limit = args.total or budget.get("total_bytes") or DEFAULT_TOTAL
    per_limit = args.per_file or budget.get("per_file_bytes") or DEFAULT_PER_FILE

    # 2026-11-05 (@TE 19:01): обход `glob("**/AGENTS.md")` подхватывал НЕотслеживаемую копию —
    # вложенный worktree `origin/main` (545 md, 10,6 МБ), из-за чего бюджет раздувался до 76141.
    # Бюджет инструкций — про файлы репозитория, поэтому перечень берём из git (как в check_rule_docs).
    files = None
    try:
        out = subprocess.run(["git", "-c", "core.hooksPath=", "ls-files", "*AGENTS.md"],
                             cwd=root, capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
        if out.returncode == 0 and out.stdout.strip():
            files = sorted(os.path.join(root, rel.strip()) for rel in out.stdout.splitlines()
                           if rel.strip())
    except Exception:  # noqa: BLE001
        files = None
    if files is None:
        files = sorted(glob.glob(os.path.join(root, "**", "AGENTS.md"), recursive=True))
    rows, total = [], 0
    for path in files:
        size = os.path.getsize(path)
        total += size
        rows.append((os.path.relpath(path, root).replace("\\", "/"), size))

    print("бюджет инструкций: файлов %d, сумма %d из %d байт" % (len(rows), total, total_limit))
    for name, size in rows:
        mark = "!" if size > per_limit else " "
        print("  %s %-28s %6d байт" % (mark, name, size))

    bad = []
    if total > total_limit:
        bad.append("сумма %d > %d" % (total, total_limit))
    for name, size in rows:
        if size > per_limit:
            bad.append("%s: %d > %d" % (name, size, per_limit))

    if bad:
        print("")
        for line in bad:
            print("  превышение:", line)
        print("")
        print("Лечение: норма — в AGENTS.md (≤ %d КБ), истории и детали — в RULES.md/задачи."
              % (per_limit // 1024))
        return 1
    print("docs-budget: в пределах лимита")
    return 0


if __name__ == "__main__":
    sys.exit(main())
