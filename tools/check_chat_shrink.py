#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""`check_chat_shrink.py` вЂ” РіРµР№С‚ В«С‡Р°С‚ РЅРµ СѓСЃС‹С…Р°РµС‚В» (РёРЅС†РёРґРµРЅС‚ @AR 2026-11-13 00:36).

Р§С‚Рѕ СЃР»СѓС‡РёР»РѕСЃСЊ: Р±РѕС‚ РєРѕРјРјРёС‚РёС‚ `chat/*.md` РёР· СЃРЅРёРјРєР°, РєРѕС‚РѕСЂС‹Р№ РјРѕР¶РµС‚ Р±С‹С‚СЊ **СЃС‚Р°СЂС€Рµ** `main`;
С‚РѕРіРґР° СЃС‚СЂРѕРєРё, РґРѕР±Р°РІР»РµРЅРЅС‹Рµ СЂРѕР»СЏРјРё РїРѕР·Р¶Рµ, РјРѕР»С‡Р° РёСЃС‡РµР·Р°СЋС‚, Р° РєРѕРјРјРёС‚ РІС‹РіР»СЏРґРёС‚ СѓСЃРїРµС€РЅС‹Рј.
РЈ @AR Р·Р° РѕРґРёРЅ РєРѕРјРјРёС‚ РїСЂРѕРїР°Р»Рѕ 17 СЃРѕРѕР±С‰РµРЅРёР№ (`chat/ar.md`: 41 в†’ 24), Сѓ @DO вЂ” 18 (`chat/do.md`:
41 в†’ 26). РџРѕС‚РµСЂСЏ РЅРµ РІРёРґРЅР° РЅРё `chat_lint` (С„РѕСЂРјР°С‚ С†РµР»), РЅРё РіР»Р°Р·Р°РјРё РІ РґРёС„С„Рµ РёР· 17 СѓРґР°Р»РµРЅРёР№.

Р§С‚Рѕ РїСЂРѕРІРµСЂСЏРµС‚: РґР»СЏ РєР°Р¶РґРѕРіРѕ РёР·РјРµРЅС‘РЅРЅРѕРіРѕ `chat/<СЂРѕР»СЊ>.md` СЃС‡РёС‚Р°РµС‚ СЃС‚СЂРѕРєРё-СЃРѕРѕР±С‰РµРЅРёСЏ
(`- [YYYY-MM-DD HH:mm]`) РІ СЂРѕРґРёС‚РµР»Рµ Рё РІ РєРѕРјРјРёС‚Рµ. РЈРјРµРЅСЊС€РµРЅРёРµ в†’ `rc=1` СЃ Р°РґСЂРµСЃРѕРј С„Р°Р№Р»Р°
Рё С‡РёСЃР»Р°РјРё. РћСЃРѕР·РЅР°РЅРЅС‹Р№ СЃР±СЂРѕСЃ С‡Р°С‚Р° (Р°СЂС…РёРІ + РїСѓСЃС‚РѕР№ С„Р°Р№Р») РґРµР»Р°РµС‚СЃСЏ С„Р»Р°РіРѕРј `--allow-shrink`
СЃ РїСЂРёС‡РёРЅРѕР№ вЂ” С‚РѕРіРґР° СЌС‚Рѕ СЂРµС€РµРЅРёРµ, Р° РЅРµ Р°РІР°СЂРёСЏ.

РљРѕРґС‹: `0` вЂ” РЅРµ СѓСЃРѕС…Р»Рѕ (РёР»Рё СЂР°Р·СЂРµС€РµРЅРѕ); `1` вЂ” СѓСЃРѕС…Р»Рѕ; `2` вЂ” РЅРµ РёР·РјРµСЂРµРЅРѕ (РЅРµС‚ git/РїСѓСЃС‚РѕР№ РґРёР°РїР°Р·РѕРЅ).
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

MSG = re.compile(r'^- \[\d{4}-\d{2}-\d{2} \d{2}:\d{2}\]')


def git(args, cwd):
    cp = subprocess.run(["git"] + args, cwd=cwd, capture_output=True, text=True,
                        encoding="utf-8", errors="replace")
    return cp.returncode, cp.stdout, cp.stderr


def count_msgs(text):
    return sum(1 for line in (text or "").splitlines() if MSG.match(line))


def show(ref, path, cwd):
    rc, out, _ = git(["show", "%s:%s" % (ref, path)], cwd)
    return out if rc == 0 else None


def changed_chat_files(rng, cwd):
    rc, out, err = git(["diff", "--name-only", rng, "--", "5-tech-backlog/chat"], cwd)
    if rc != 0:
        raise OSError((err or "").strip()[:200])
    return [f for f in out.splitlines() if f.endswith(".md")]


def check(rev_from, rev_to, root, allow_shrink=False):
    files = changed_chat_files("%s..%s" % (rev_from, rev_to), root)
    findings, measured = [], 0
    for f in files:
        before = show(rev_from, f, root)
        after = show(rev_to, f, root)
        if after is None:
            continue
        b, a = count_msgs(before or ""), count_msgs(after)
        measured += 1
        if a < b:
            findings.append((f, b, a, b - a))
    if allow_shrink:
        findings = []          # РѕСЃРѕР·РЅР°РЅРЅС‹Р№ СЃР±СЂРѕСЃ: С„РёРєСЃРёСЂСѓРµРј СЂРµС€РµРЅРёРµ, РЅРѕ РЅРµ РїСЂРѕРІР°Р»РёРІР°РµРј РіРµР№С‚
    return findings, measured, files


def selftest():
    cases = []
    # Р’РђР–РќРћ: temporary.Directory РЅР° Windows РїР°РґР°РµС‚ РІ rmtree (WinError 5) вЂ” РєР°С‚Р°Р»РѕРі РїРѕРґ cwd,
    # РєР°Рє РІ gen_images_lock.py/@DO 2026-11-12.
    base = os.path.join(os.getcwd(), ".tmp-chat-shrink-selftest")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    if True:
        repo = os.path.join(base, "r")
        os.makedirs(os.path.join(repo, "5-tech-backlog", "chat"))
        chat = os.path.join(repo, "5-tech-backlog", "chat", "do.md")

        def w(text, msg=""):
            open(chat, "w", encoding="utf-8").write(text)
            git(["add", "-A"], repo)
            git(["-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", msg or "c"], repo)
            return git(["rev-parse", "HEAD"], repo)[1].strip()

        def msg(i):
            return "- [2026-11-13 00:%02d] в†’ @TL: СЃРѕРѕР±С‰РµРЅРёРµ %d\n" % (i % 60, i)

        git(["init", "-q"], repo)
        c0 = w("".join(msg(i) for i in range(1, 6)), "five")
        six = w("".join(msg(i) for i in range(1, 7)), "six")

        got, measured, _ = check(c0, six, repo)
        cases.append(("СЂРѕСЃС‚ 5 -> 6 РЅРµ Р»РѕРІРёС‚СЃСЏ", got == [] and measured == 1))

        shrunk = w("".join(msg(i) for i in range(1, 4)), "shrunk")
        got, measured, _ = check(six, shrunk, repo)
        cases.append(("СѓСЃС‹С…Р°РЅРёРµ 6 -> 3 РїРѕР№РјР°РЅРѕ (%d)" % len(got),
                      len(got) == 1 and got[0][1] == 6 and got[0][2] == 3))

        ok, _, _ = check(six, shrunk, repo, allow_shrink=True)
        cases.append(("СѓСЃС‹С…Р°РЅРёРµ СЂР°Р·СЂРµС€РµРЅРѕ С„Р»Р°РіРѕРј", ok == []))

        same = w("".join(msg(i) for i in range(1, 4)), "same")
        got, measured, _ = check(shrunk, same, repo)
        cases.append(("Р±РµР· РёР·РјРµРЅРµРЅРёР№ (0 -> 0 РЅРѕРІС‹С…) РЅРµ Р»РѕРІРёС‚СЃСЏ", got == []))

        touched = w("".join(msg(i) for i in range(1, 4)) + "Р·Р°РіРѕР»РѕРІРѕРє\n", "cosmetic")
        got, _, files = check(same, touched, repo)
        cases.append(("РёР·РјРµРЅРµРЅРёРµ Р·Р°РіРѕР»РѕРІРєР° Р±РµР· СЃРјРµРЅС‹ С‡РёСЃР»Р° вЂ” 0 РЅР°С…РѕРґРѕРє", got == [] and len(files) == 1))

    ok = sum(1 for _, passed in cases if passed)
    for name, passed in cases:
        print("  [%s] %s" % ("OK " if passed else "FAIL", name))
    print("check_chat_shrink --selftest: %d/%d" % (ok, len(cases)))
    return 0 if ok == len(cases) else 1


def main(argv=None):
    ap = argparse.ArgumentParser(description="Р“РµР№С‚ В«С‡Р°С‚ РЅРµ СѓСЃС‹С…Р°РµС‚В» (incident AR/DO 2026-11-13)")
    ap.add_argument("--range", default="HEAD~1..HEAD")
    ap.add_argument("--root", default=".")
    ap.add_argument("--allow-shrink", action="store_true",
                    help="СЂР°Р·СЂРµС€РёС‚СЊ РѕСЃРѕР·РЅР°РЅРЅС‹Р№ СЃР±СЂРѕСЃ С‡Р°С‚Р° (Р°СЂС…РёРІ)")
    ap.add_argument("--reason", default="", help="РїРѕС‡РµРјСѓ СѓСЃС‹С…Р°РЅРёРµ СЂР°Р·СЂРµС€РµРЅРѕ")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root or ".")
    if ".." not in a.range:
        print("::error::check_chat_shrink: --range РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ A..B, РїРѕР»СѓС‡РµРЅРѕ %r" % a.range)
        return 2
    rev_from, rev_to = a.range.split("..", 1)
    try:
        findings, measured, files = check(rev_from, rev_to, root, a.allow_shrink)
    except OSError as ex:
        print("::error::check_chat_shrink: РЅРµ РёР·РјРµСЂРµРЅРѕ: %s" % ex)
        return 2

    if measured == 0:
        print("check_shrink: chat/*.md РЅРµ РјРµРЅСЏР»РёСЃСЊ РІ %s вЂ” РїСЂРѕРІРµСЂСЏС‚СЊ РЅРµС‡РµРіРѕ" % a.range)
        return 0

    if findings and not a.allow_shrink:
        for f, b, aft, delta in findings:
            print("::error::(chat-shrink) %s: СЃРѕРѕР±С‰РµРЅРёР№ Р±С‹Р»Рѕ %d, СЃС‚Р°Р»Рѕ %d (в€’%d) вЂ” "
                  "Р±РѕС‚ Р·Р°РєРѕРјРјРёС‚РёР» СѓСЃС‚Р°СЂРµРІС€РёР№ СЃРЅРёРјРѕРє РёР»Рё СѓСЃС‹С…Р°РЅРёРµ РЅРµ РѕР±СЉСЏСЃРЅРµРЅРѕ" % (f, b, aft, delta))
        print("check_shrink: РџР РћР’РђР› вЂ” СѓСЃРѕС…Р»Рѕ С„Р°Р№Р»РѕРІ %d РёР· %d" % (len(findings), measured))
        return 1

    if findings and a.allow_shrink:
        print("check_shrink: СѓСЃС‹С…Р°РЅРёРµ СЂР°Р·СЂРµС€РµРЅРѕ (%s), С„Р°Р№Р»РѕРІ: %d" % (a.reason or "Р±РµР· РїСЂРёС‡РёРЅС‹",
                                                                    len(findings)))
    print("check_shrink: РѕРє вЂ” РїСЂРѕРІРµСЂРµРЅРѕ С„Р°Р№Р»РѕРІ %d, СѓСЃС‹С…Р°РЅРёР№ %d" % (measured, 0 if a.allow_shrink else len(findings)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
