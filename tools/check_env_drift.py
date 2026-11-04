#!/usr/bin/env python
"""Гейт «VPS == репозиторий» (DO-051; попутно DO-067 про Caddy).

Зачем: на проде было три источника правды — репозиторий, устаревший клон
`/home/deploy/XDiary` и частичное дерево `/opt/xdiary` со своими копиями скриптов
и compose. Из-за этого пайплайн был зелёным, а работал старый образ.

Проверки (запускать НА сервере, где есть и клон, и развёрнутое дерево):
  1. клон стоит на том же коммите, что `origin/main` (без `fetch` это дрейф);
  2. в развёрнутом дереве нет копий файлов репозитория (`*.sh`, `*compose*.yml`);
  3. список сервисов в развёрнутом compose совпадает с репозиторным;
  4. применённый Caddyfile совпадает с репозиторным (`Caddyfile.prod` / `.dev`);
  5. если Funnel проксирует не в Caddy, а прямо в бэкенд — это предупреждение:
     тогда конфиг Caddy не обслуживает трафик, и его обещания не наблюдаемы (DO-067).

    python tools/check_env_drift.py --repo /home/deploy/XDiary --deploy /opt/xdiary
    python tools/check_env_drift.py --selftest
"""
import argparse
import os
import re
import shutil
import subprocess
import sys


def sh(*args, cwd=None):
    r = subprocess.run(args, cwd=cwd, capture_output=True, text=True, encoding="utf-8",
                       errors="replace")
    return r.returncode, (r.stdout or "") + (r.stderr or "")


def check_clone(repo, expect=None):
    bad = []
    rc, head = sh("git", "rev-parse", "--short", "HEAD", cwd=repo)
    if rc != 0:
        return ["клон не читается: %s" % head.strip()[:80]]
    rc2, origin = sh("git", "rev-parse", "--short", "origin/main", cwd=repo)
    if rc2 != 0:
        return ["в клоне нет ref origin/main (fetch не делался): %s" % origin.strip()[:80]]
    h, o = head.strip(), origin.strip()
    if expect:
        if not h.startswith(expect) and not expect.startswith(h):
            bad.append("клон на %s, а ожидается %s — нужен git fetch/reset"
                       % (h, expect[:12]))
    elif h != o:
        bad.append("клон на %s, а origin/main на %s — нужен git fetch/reset" % (h, o))
    rc3, dirty = sh("git", "status", "--porcelain", cwd=repo)
    if rc3 == 0 and dirty.strip():
        bad.append("в клоне грязное дерево (%d файлов)" % len(dirty.strip().splitlines()))
    return bad


REPO_KEY_DIRS = ("7-deploy", "tools", "9-tests")


def check_deploy_copies(deploy, repo=None):
    """Копии скриптов в развёрнутом дереве допустимы, только если они БАЙТ-В-БАЙТ как в репозитории.

    Почему не «удалить копии»: `deploy.sh` запускается из `/opt/xdiary/7-deploy` и берёт
    `--env-file secrets/.env` ОТНОСИТЕЛЬНО этого каталога. Замена каталога симлинком на клон
    увела бы относительный путь в клон, где `secrets/` нет, и деплой сломался бы. Поэтому
    дрейф = **устаревшая копия**, а не сам факт копии.
    """
    if not os.path.isdir(deploy):
        return ["развёрнутого дерева нет: %s" % deploy]
    copies = []
    for root, _dirs, files in os.walk(deploy):
        # бэкапы и старые копии — не дрейф: это страховка при правках
        if any((".bak-" in part or ".old-" in part) for part in root.split(os.sep)):
            continue
        for f in files:
            if f.endswith(".sh"):
                copies.append(os.path.relpath(os.path.join(root, f), deploy))
    if not copies:
        return []
    if not repo or not os.path.isdir(repo):
        return ["в развёрнутом дереве есть скрипты (%d) — сравнить не с чем, укажи --repo"
                % len(copies)]
    stale = []
    for rel in sorted(copies):
        src = os.path.join(repo, rel)
        dst = os.path.join(deploy, rel)
        try:
            if open(src, "rb").read() != open(dst, "rb").read():
                stale.append(rel)
        except OSError:
            stale.append(rel + " (нет пары в репозитории)")
    if stale:
        return ["в развёрнутом дереве УСТАРЕВШИЕ копии скриптов: %s%s"
                % (", ".join(stale[:6]), " …" if len(stale) > 6 else "")]
    return []


def check_compose(repo, deploy):
    repo_file = os.path.join(repo, "7-deploy", "podman-compose.yml")
    dep_file = os.path.join(deploy, "7-deploy", "podman-compose.yml")
    if not os.path.exists(repo_file):
        return ["в репозитории нет 7-deploy/podman-compose.yml"]
    if not os.path.exists(dep_file):
        return ["в развёрнутом дереве нет 7-deploy/podman-compose.yml"]
    def services(path):
        out = set()
        for line in open(path, encoding="utf-8", errors="replace"):
            m = re.match(r'^  ([a-z0-9_-]+):\s*$', line)
            if m:
                out.add(m.group(1))
        return out
    a, b = services(repo_file), services(dep_file)
    missing = sorted(a - b)
    extra = sorted(b - a)
    bad = []
    if missing:
        bad.append("в развёрнутом compose нет сервисов: %s" % ", ".join(missing))
    if extra:
        bad.append("в развёрнутом compose лишние сервисы: %s" % ", ".join(extra))
    return bad


def check_caddy(repo, contour):
    name = "Caddyfile.prod" if contour == "prod" else "Caddyfile.dev"
    src = os.path.join(repo, "7-deploy", "caddy", name)
    applied = "/etc/caddy/Caddyfile"
    if not os.path.exists(src):
        return ["в репозитории нет %s" % name]
    if not os.path.exists(applied):
        return ["нет применённого %s" % applied]
    a = open(src, encoding="utf-8", errors="replace").read().splitlines()
    b = open(applied, encoding="utf-8", errors="replace").read().splitlines()
    if a == b:
        return []
    diff = [l for l in (set(a) ^ set(b)) if l.strip()][:3]
    return ["применённый %s отличается от репозиторного %s (пример: %s)"
            % (applied, name, " | ".join(diff)[:160])]


def check_funnel(contour):
    rc, out = sh("tailscale", "funnel", "status")
    if rc != 0 or "8080" not in out:
        return []
    hits = [l.strip() for l in out.splitlines() if "proxy" in l and "8080" in l]
    if hits:
        return ["Funnel проксирует прямо в бэкенд (%s): Caddy не в пути трафика, "
                "его конфиг не обслуживает запросы (DO-067)" % hits[0][:80]]
    return []


def selftest():
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '.tmp-envdrift')
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(base, exist_ok=True)
    try:
        # норма: клон == origin/main, дерево без копий
        repo = os.path.join(base, "repo")
        os.makedirs(os.path.join(repo, "7-deploy"))
        open(os.path.join(repo, "7-deploy", "podman-compose.yml"), "w").write(
            "services:\n  clients:\n    image: x\n  journal:\n    image: y\n")
        for cmd in (("git", "init", "-q"), ("git", "config", "user.email", "t@t"),
                    ("git", "config", "user.name", "t")):
            sh(*cmd, cwd=repo)
        sh("git", "add", "-A", cwd=repo)
        sh("git", "commit", "-qm", "init", cwd=repo)
        sh("git", "update-ref", "refs/remotes/origin/main", "HEAD", cwd=repo)
        deploy = os.path.join(base, "deploy")
        os.makedirs(os.path.join(deploy, "7-deploy", "secrets"))
        shutil.copy(os.path.join(repo, "7-deploy", "podman-compose.yml"),
                    os.path.join(deploy, "7-deploy", "podman-compose.yml"))
        got = check_clone(repo) + check_deploy_copies(deploy) + check_compose(repo, deploy)
        if not got:
            ok += 1
        else:
            print("  [FAIL] норма помечена дрейфом: %s" % got)

        # дефект: копия скрипта РАСХОДИТСЯ с репозиторием + расхождение compose
        os.makedirs(os.path.join(repo, "7-deploy"), exist_ok=True)
        open(os.path.join(repo, "7-deploy", "deploy.sh"), "w").write("#!/bin/sh\necho repo\n")
        open(os.path.join(deploy, "7-deploy", "deploy.sh"), "w").write("#!/bin/sh\necho OLD\n")
        open(os.path.join(deploy, "7-deploy", "podman-compose.yml"), "w").write(
            "services:\n  clients:\n    image: x\n")
        got2 = check_deploy_copies(deploy) + check_compose(repo, deploy)
        if len(got2) >= 2:
            ok += 1
        else:
            print("  [FAIL] дефект не пойман: %s" % got2)
    finally:
        shutil.rmtree(base, ignore_errors=True)
    print("check_env_drift --selftest: %d/2" % ok)
    return 0 if ok == 2 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default="/home/deploy/XDiary")
    ap.add_argument("--deploy", default="/opt/xdiary")
    ap.add_argument("--contour", default="prod", choices=("prod", "dev"))
    ap.add_argument("--expect", default=None,
                    help="ожидаемый коммит клона (обычно актуальный origin/main)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    problems = []
    problems += [("клон", p) for p in check_clone(a.repo, a.expect)]
    problems += [("дерево", p) for p in check_deploy_copies(a.deploy, a.repo)]
    problems += [("compose", p) for p in check_compose(a.repo, a.deploy)]
    problems += [("caddy", p) for p in check_caddy(a.repo, a.contour)]
    warnings = [("funnel", p) for p in check_funnel(a.contour)]
    for scope, p in problems:
        print("::error::(drift/%s) %s" % (scope, p))
    for scope, p in warnings:
        print("::warning::(drift/%s) %s" % (scope, p))
    print("check_env_drift: проверок 5, ошибок %d, предупреждений %d (контур %s)"
          % (len(problems), len(warnings), a.contour))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
