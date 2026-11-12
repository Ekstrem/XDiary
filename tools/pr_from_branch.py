#!/usr/bin/env python3
"""pr_from_branch.py — PR из ветки `role/<name>` в `main` (PDR-047 B1).

Зачем: по PDR-047 роли пушат в `role/<name>`, а в `main` — только через PR + merge queue.
Этот инструмент делает PR автоматически: заголовок `<role>: <последний коммит>`, тело —
список коммитов ветки относительно `main`. Повторный пуш **обновляет** существующий PR,
а не создаёт второй.

Почему отдельный файл, а не `run:` в YAML: логику (роль из имени ветки, заголовок, тело,
«нет коммитов — нет PR») можно проверить `--selftest` без GitHub — §5.4 требует обе стороны.

Коды: `0` — PR создан/обновлён либо создавать нечего; `1` — ошибка gh (не смогли);
`2` — не измерено (нет обязательных аргументов/нет gh).
"""
import argparse
import os
import re
import subprocess
import sys


def role_from_branch(branch):
    """`role/do` -> `do`; `role/fe-front` -> `fe-front`; не `role/*` -> ""."""
    m = re.match(r'^role/([\w.\-]+)$', (branch or "").strip())
    return m.group(1) if m else ""


def parse_commits(text):
    """Строки `git log --format=%h %s` -> [(sha, subject)]."""
    out = []
    for line in (text or "").splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split(None, 1)
        out.append((parts[0], parts[1] if len(parts) > 1 else ""))
    return out


def build_title(role, commits):
    """`<role>: <subject последнего коммита>`; пустой список -> "".

    Коммиты в репозитории начинаются с роли (`do: ...`), поэтому префикс снимается:
    иначе на PR было бы `do: do: ...` (заголовок читается как ошибка, а не как роль).
    """
    if not role or not commits:
        return ""
    subject = commits[0][1].strip() or "(без описания)"
    prefix = role + ":"
    while subject.lower().startswith(prefix.lower()):
        subject = subject[len(prefix):].strip()
    subject = subject[:100]
    return "%s: %s" % (role, subject)


def build_body(role, commits, base="main", branch=""):
    """Тело PR: список коммитов + пометка, что его создал бот."""
    if not commits:
        return ""
    lines = ["## Коммиты (%d)" % len(commits), ""]
    for sha, subject in commits:
        lines.append("- `%s` %s" % (sha, subject.strip()[:160]))
    lines += ["", "Ветка `%s` → `%s`." % (branch or "role/" + role, base), "",
              "_PR создан автоматически (`pr-create.yml`, PDR-047 B1). "
              "Повторный пуш в ветку обновляет заголовок и тело._"]
    return "\n".join(lines) + "\n"


def _ref_exists(ref, cwd="."):
    cp = subprocess.run(["git", "rev-parse", "--verify", "--quiet", ref],
                        cwd=cwd, capture_output=True, text=True)
    return cp.returncode == 0


def git_commits(branch, base, cwd="."):
    """[(sha, subject)] от base до branch (свежие сверху).

    В CI `actions/checkout` с `ref: <ветка>` не создаёт локальную `main`, поэтому
    `main..role/do` падает с `ambiguous argument` (прогон #1). База — `origin/main`,
    а если переданной ветки нет локально, берём `HEAD` (мы уже на нужном коммите).
    """
    base_ref = base
    if not _ref_exists(base_ref, cwd) and _ref_exists("origin/" + base, cwd):
        base_ref = "origin/" + base
    branch_ref = branch
    if not _ref_exists(branch_ref, cwd):
        branch_ref = "HEAD"
    cp = subprocess.run(["git", "log", "--format=%h %s", "%s..%s" % (base_ref, branch_ref)],
                        cwd=cwd, capture_output=True, text=True, encoding="utf-8",
                        errors="replace")
    if cp.returncode != 0:
        raise OSError("git log %s..%s: %s" % (base_ref, branch_ref,
                                             (cp.stderr or "").strip()[:160]))
    return parse_commits(cp.stdout)


def gh(args, cwd="."):
    """Вызов gh; возвращает (rc, stdout, stderr)."""
    cp = subprocess.run(["gh"] + args, cwd=cwd, capture_output=True, text=True,
                        encoding="utf-8", errors="replace")
    return cp.returncode, cp.stdout or "", cp.stderr or ""


def existing_pr(branch, base, cwd="."):
    rc, out, err = gh(["pr", "list", "--head", branch, "--base", base, "--state", "open",
                       "--json", "number", "--jq", ".[0].number"], cwd=cwd)
    if rc != 0:
        raise OSError((err or "").strip()[:200])
    out = out.strip()
    return int(out) if out.isdigit() else 0


def selftest():
    cases = []
    cases.append(("role из ветки: role/do -> do", role_from_branch("role/do") == "do"))
    cases.append(("role из ветки: role/fe-front -> fe-front",
                  role_from_branch("role/fe-front") == "fe-front"))
    cases.append(("не role-ветка -> пусто", role_from_branch("main") == ""))

    commits = parse_commits("abc1234 do: тест один\ndef5678 do: тест два\n")
    cases.append(("разбор git log", commits == [("abc1234", "do: тест один"),
                                                ("def5678", "do: тест два")]))
    cases.append(("заголовок = <role>: <последний коммит> (префикс роли не дублируется)",
                  build_title("do", [("abc1234", "do: тест один")]) == "do: тест один"))
    cases.append(("заголовок без префикса роли",
                  build_title("do", [("abc1234", "правка lock")]) == "do: правка lock"))
    body = build_body("do", commits, base="main", branch="role/do")
    cases.append(("тело перечисляет все коммиты",
                  "abc1234" in body and "def5678" in body and "## Коммиты (2)" in body))
    cases.append(("нет коммитов -> нет заголовка и тела",
                  build_title("do", []) == "" and build_body("do", []) == ""))
    ok = 0
    for name, passed in cases:
        print("  [%s] %s" % ("OK " if passed else "FAIL", name))
        ok += 1 if passed else 0
    if not cases:
        print("::error::pr_from_branch: 0 кейсов — самопроверка ничего не проверила")
        return 2
    print("pr_from_branch --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="PR из role/* в main (PDR-047 B1)")
    ap.add_argument("--branch", default=os.environ.get("GITHUB_REF_NAME", ""))
    ap.add_argument("--base", default="origin/main")
    ap.add_argument("--root", default=".")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)

    if a.selftest:
        return selftest()

    branch = (a.branch or "").strip()
    role = role_from_branch(branch)
    if not role:
        print("::error::pr_from_branch: ветка «%s» не вида role/<name> — PR не создаю" % branch)
        return 2

    try:
        commits = git_commits(branch, a.base, cwd=a.root)
    except OSError as ex:
        print("::error::pr_from_branch: не смог прочитать коммиты: %s" % ex)
        return 2

    title = build_title(role, commits)
    body = build_body(role, commits, base=a.base, branch=branch)
    print("заголовок: %s" % (title or "(нет)"))
    print("коммитов: %d" % len(commits))

    if not commits:
        print("::notice::pr_from_branch: в ветке нет коммитов относительно %s — PR не нужен" % a.base)
        return 0

    if a.dry_run:
        print("--- тело PR ---")
        print(body)
        return 0

    try:
        num = existing_pr(branch, a.base, cwd=a.root)
    except OSError as ex:
        print("::error::pr_from_branch: gh pr list не сработал: %s" % ex)
        return 1

    if num:
        rc, out, err = gh(["pr", "edit", str(num), "--title", title, "--body", body], cwd=a.root)
        if rc != 0:
            print("::error::pr_from_branch: не обновил PR #%d: %s" % (num, (err or "").strip()[:200]))
            return 1
        print("::notice::pr_from_branch: обновлён PR #%d (%s → %s)" % (num, branch, a.base))
        return 0

    rc, out, err = gh(["pr", "create", "--head", branch, "--base", a.base,
                       "--title", title, "--body", body], cwd=a.root)
    if rc != 0:
        print("::error::pr_from_branch: не создал PR: %s" % (err or "").strip()[:300])
        return 1
    print("::notice::pr_from_branch: создан PR %s" % (out or "").strip()[:200])
    return 0


if __name__ == "__main__":
    sys.exit(main())
