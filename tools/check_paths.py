#!/usr/bin/env python3
"""Проверка «роль пишет только в свои папки».

Роль определяется по префиксу коммита:
    be:            fe:            do:            te:            ar:            tl:            po:
    chat(te):      docs(do):      fix(9-tests):  feat(be):      ...

Коммиты без узнаваемого префикса (руками, из IDE, «README: ...») не проверяются —
они считаются человеческими. Это компромисс: забор ловит агентские коммиты, ваши
не блокирует.

Использование:
    python tools/check_paths.py                     # последний коммит
    python tools/check_paths.py --range HEAD~20..HEAD
    python tools/check_paths.py --range A..B --report-only   # только отчёт, код 0
    python tools/check_paths.py --zones             # показать карту зон

Выход 1 — найдены вылеты за зону (в CI это красный job).
"""
import argparse
import fnmatch
import re
import subprocess
import sys

# ЯДРО — «своя» папка роли. Ровно то, что задал владелец продукта.
CORE_ZONES = {
    "po": ["01-user-stories/*", "02-cjm/*"],
    "ar": ["03-ddd/*", "04-architecture/*"],
    "be": ["6-msa/*"],
    "fe": ["8-front/*"],
    "do": ["7-deploy/*"],
    "te": ["9-tests/*"],
}

# СЛУЖЕБНОЕ — без этого цикл не работает: своя папка задач, свой outbox, зоны
# инструментов у DevOps и заметки о пробелах фреймворка у BackEnd.
SERVICE_ZONES = {
    "po": ["5-tech-backlog/chat/po.md"],
    "ar": ["5-tech-backlog/arch/*", "5-tech-backlog/chat/ar.md"],
    "be": ["0-df-issue/*", "5-tech-backlog/back-end/*", "5-tech-backlog/chat/be.md"],
    "fe": ["5-tech-backlog/front-end/*", "5-tech-backlog/chat/fe.md"],
    "do": ["tools/*", ".github/*", "5-tech-backlog/devops/*", "5-tech-backlog/chat/do.md"],
    "te": ["5-tech-backlog/tester/*", "5-tech-backlog/chat/te.md"],
}

# Зона TechLead: он раздаёт задачи, поэтому видит все папки задач, но не outbox'ы чужих.
TL_ZONE = ["5-tech-backlog/README.md",
           "5-tech-backlog/arch/*", "5-tech-backlog/back-end/*",
           "5-tech-backlog/front-end/*", "5-tech-backlog/devops/*",
           "5-tech-backlog/tester/*",
           "5-tech-backlog/chat/tl.md", "5-tech-backlog/chat/README.md",
           "tools/*"]

# зоны, доступные всем ролям (кроме самих outbox'ов чата)
SHARED = ["README.md", "CHANGELOG.md", ".gitignore", ".gitattributes"]


def zones_for(role, strict=False):
    """Разрешённые пути роли. strict=True — только ядро (как задал владелец)."""
    if role == "tl":
        return TL_ZONE + SHARED
    if strict:
        return CORE_ZONES[role] + SHARED
    return CORE_ZONES[role] + SERVICE_ZONES[role] + SHARED

# псевдонимы скоупа/типа коммита -> роль
ALIASES = {
    "be": "be", "backend": "be", "back-end": "be", "6-msa": "be",
    "fe": "fe", "frontend": "fe", "front-end": "fe", "8-front": "fe",
    "do": "do", "devops": "do", "7-deploy": "do",
    "te": "te", "tester": "te", "test": "te", "tests": "te", "9-tests": "te",
    "ar": "ar", "arch": "ar", "architect": "ar", "03-ddd": "ar", "04-architecture": "ar",
    "tl": "tl", "techlead": "tl", "5-tech-backlog": "tl",
    "po": "po", "01-user-stories": "po", "02-cjm": "po",
}

ROLE_HUMAN = {"po": "PO", "ar": "Architect", "tl": "TechLead", "be": "BackEnd",
              "fe": "FrontEnd", "do": "DevOps", "te": "TestEngineer"}

# feat(be): ...   chat(te): ...   fix(9-tests): ...   be: ...
CONV = re.compile(r"^([a-zA-Z][\w-]*)(?:\(([^)]+)\))?!?:\s")
PLAIN = re.compile(r"^(be|fe|do|te|ar|tl|po)[!:]", re.IGNORECASE)


def detect_role(subject):
    m = CONV.match(subject)
    if m:
        typ, scope = m.group(1).lower(), (m.group(2) or "").lower()
        if scope and scope in ALIASES:
            return ALIASES[scope]
        if typ in ALIASES:
            return ALIASES[typ]
        return None
    m = PLAIN.match(subject)
    if m:
        return ALIASES.get(m.group(1).lower())
    return None


def matches(path, patterns):
    """`dir/*` — поддерево, иначе точное имя файла. AGENTS.md разрешён везде."""
    if path.rsplit("/", 1)[-1] == "AGENTS.md":
        return True
    for p in patterns:
        if p.endswith("/*"):
            base = p[:-2]
            if path == base or path.startswith(base + "/"):
                return True
        elif path == p:
            return True
    return False


def git(*args):
    out = subprocess.run(["git", *args], capture_output=True)
    if out.returncode != 0:
        sys.exit("git %s: %s" % (" ".join(args), out.stderr.decode("utf-8", "ignore").strip()))
    return out.stdout.decode("utf-8", "ignore")


def read_commits(rng):
    """-> [(sha, subject, [paths])]"""
    raw = git("log", "--no-renames", "--name-only", "--format=%x00%H%x1f%s", rng)
    commits, cur = [], None
    for line in raw.split("\n"):
        if line.startswith("\x00"):
            sha, _, subject = line[1:].partition("\x1f")
            cur = (sha[:7], subject, [])
            commits.append(cur)
        elif line.strip() and cur is not None:
            cur[2].append(line.strip().replace("\\", "/"))
    return commits


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--range", default="HEAD~1..HEAD", help="диапазон git log (по умолчанию HEAD~1..HEAD)")
    ap.add_argument("--report-only", action="store_true", help="не падать, только отчёт")
    ap.add_argument("--zones", action="store_true", help="показать карту зон и выйти")
    ap.add_argument("--strict", action="store_true",
                    help="только ядро (1/2, 3/4, 6, 8, 7, 9) — без папок задач и чата")
    ap.add_argument("--quiet", action="store_true")
    a = ap.parse_args()

    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    if a.zones:
        mode = "СТРОГО: только ядро" if a.strict else "ядро + служебные файлы"
        print("режим: %s\n" % mode)
        for role in ("po", "ar", "be", "fe", "do", "te"):
            print("%-12s %s" % (ROLE_HUMAN[role], ", ".join(zones_for(role, a.strict))))
        print("%-12s %s" % ("TechLead", ", ".join(zones_for("tl"))))
        print("%-12s %s" % ("все", ", ".join(SHARED)))
        return 0

    commits = read_commits(a.range)
    if not commits:
        print("guard: в диапазоне %s нет коммитов" % a.range)
        return 0

    bad = skipped = checked = 0
    lines = []
    for sha, subject, paths in commits:
        role = detect_role(subject)
        if not role:
            skipped += 1
            continue
        checked += 1
        allowed = zones_for(role, a.strict)
        outside = [p for p in paths if not matches(p, allowed)]
        if outside:
            bad += 1
            lines.append("")
            lines.append("✗ %s  [%s]  %s" % (sha, ROLE_HUMAN[role], subject))
            for p in outside:
                lines.append("    вне зоны: %s" % p)

    if lines and not a.quiet:
        print("\n".join(lines))
        print("")
    print("guard: проверено коммитов %d, пропущено без роли %d, нарушений %d"
          % (checked, skipped, bad))

    if bad and not a.report_only:
        print("\nРоль вышла за свою папку. Что делать:")
        print(" 1) перенести правку в зону владельца (или оформить задачу через 5-tech-backlog/chat);")
        print(" 2) если выход согласован — тот, кому принадлежит зона, коммитит её сам")
        print("    с префиксом своей роли;")
        print(" 3) карта зон: python tools/check_paths.py --zones")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
