#!/usr/bin/env python
"""Релиз по ADR-027 §3.3 (принято 2026-11-14, @AR): `lock@<sha>` + тег `release-YYYY.MM.DD.N`.

Что делает:
  1. обновляет `7-deploy/images.lock` (генератор `tools/gen_images_lock.py`);
  2. пишет раздел в `CHANGELOG.md` из `git log --oneline <prev-lock>..HEAD` — **в том же
     коммите**, что и lock (правило ADR: релиз — это коммит, а не «состояние сервера»);
  3. (с `--tag`) ставит git-тег `release-YYYY.MM.DD.N`, N — следующий по счётчику за день.

Без `--write` — только печатает, что будет сделано (dry-run по умолчанию).

    python tools/release.py                 # показать план
    python tools/release.py --write         # изменить lock + CHANGELOG
    python tools/release.py --write --tag   # плюс git-тег
"""
import argparse
import datetime
import os
import re
import subprocess
import sys

LOCK = "7-deploy/images.lock"
CHANGELOG = "CHANGELOG.md"
GEN = os.path.join("tools", "gen_images_lock.py")


def git(*args):
    r = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8",
                       errors="replace")
    return r.stdout.strip()


def prev_lock_commit():
    return git("log", "-1", "--format=%h", "--", LOCK)


def next_tag(date=None):
    day = (date or datetime.date.today()).strftime("%Y.%m.%d")
    tags = git("tag", "--list", "release-%s.*" % day).splitlines()
    nums = [int(t.rsplit(".", 1)[-1]) for t in tags if re.search(r"\.(\d+)$", t)]
    return "release-%s.%d" % (day, (max(nums) + 1) if nums else 1)


def changelog_entry(prev, head, tag):
    log = git("log", "--oneline", "--no-decorate", "%s..%s" % (prev, head)) if prev else ""
    lines = [l for l in log.splitlines() if l.strip()]
    body = "\n".join("- " + l.split(" ", 1)[-1] for l in lines[:40]) or "- (изменений в путях образов нет)"
    return ("\n## %s — `lock@%s`\n\nИзменения: `git log --oneline %s..%s`\n\n%s\n"
            % (tag, head[:7], prev or "(начало)", head[:7], body))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--tag", action="store_true")
    ap.add_argument("--head", default="HEAD")
    a = ap.parse_args()
    head = git("rev-parse", a.head)
    prev = prev_lock_commit()
    tag = next_tag()
    print("релиз: %s (lock@%s)" % (tag, head[:7]))
    print("предыдущий lock-коммит: %s" % (prev or "(нет)"))
    changed = git("log", "--oneline", "--no-decorate", "%s..%s" % (prev, head)) if prev else ""
    print("коммитов после предыдущего lock: %d" % len([l for l in changed.splitlines() if l.strip()]))
    if not a.write:
        print("\n--dry-run: ничего не изменено. Планируется:")
        print("  1) перегенерировать %s" % LOCK)
        print("  2) добавить в %s раздел '%s'" % (CHANGELOG, tag))
        if a.tag:
            print("  3) git tag %s" % tag)
        return 0

    r = subprocess.run([sys.executable, GEN, "--write"], capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    print(r.stdout.strip() or r.stderr.strip()[:200])
    entry = changelog_entry(prev, head, tag)
    old = open(CHANGELOG, encoding="utf-8").read() if os.path.exists(CHANGELOG) else "# CHANGELOG\n"
    head_line = old.splitlines()[0] if old.splitlines() else "# CHANGELOG"
    rest = "\n".join(old.splitlines()[1:])
    open(CHANGELOG, "w", encoding="utf-8", newline="\n").write(head_line + "\n" + entry + rest + "\n")
    print("CHANGELOG: добавлен раздел %s" % tag)
    if a.tag:
        t = subprocess.run(["git", "tag", tag], capture_output=True, text=True, encoding="utf-8",
                           errors="replace")
        print("git tag %s: rc=%d %s" % (tag, t.returncode, (t.stderr or "").strip()[:120]))
    print("следующий шаг: git add 7-deploy/images.lock CHANGELOG.md")
    print("             git commit -m 'do(release): %s' && git push --follow-tags" % tag)
    return 0


if __name__ == "__main__":
    sys.exit(main())
