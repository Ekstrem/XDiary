#!/usr/bin/env python3
"""Guard: ищет утечки секретов (ключи/токены/пароли) в репозитории (DO-018).

Публичный репозиторий — прод-секреты живут только в .env / GitHub Secrets.
Сканирует чат/задачи и код/конфиги на паттерны секретов; при находке
выводит файл:строка + паттерн (БЕЗ значения) и завершается с кодом 1.

Паттерны:
  - SigningKey / Jwt:SigningKey со значением (base64 >= 20);
  - GARMIN_API_KEY= / PROJECT_PAT= / password= / passwd= со значением;
  - длинные base64 (с `=`-паддингом) / hex строки (>= 32) — только в chat/*.md.

Allowlist: dev-ключи из appsettings*.json и docker-compose*.yml (не прод-секреты).
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHAT_DIR = os.path.join(ROOT, "5-tech-backlog", "chat")
SKIP_FILES = {"_dialogue.md", "TODO-chat-delivery.md", "check_secrets.py"}

ALLOWLIST = set()


def collect_allowlist():
    # JSON (appsettings): dev-значения ключей
    for path in glob.glob(os.path.join(ROOT, "**", "appsettings*.json"), recursive=True):
        p = path.replace("\\", "/")
        if "/bin/" in p or "/obj/" in p or "/node_modules/" in p:
            continue
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
        except Exception:
            continue

        def walk(obj):
            if isinstance(obj, dict):
                for k, v in obj.items():
                    if isinstance(v, str) and k.lower() in (
                        "signingkey", "password", "username", "garminapikey",
                    ):
                        ALLOWLIST.add(v)
                    walk(v)
            elif isinstance(obj, list):
                for x in obj:
                    walk(x)

        walk(data)

    # YAML (docker-compose): dev-значения
    for path in glob.glob(os.path.join(ROOT, "**", "docker-compose*.yml"), recursive=True):
        try:
            with open(path, encoding="utf-8", errors="ignore") as f:
                for line in f:
                    m = re.match(
                        r'\s*(?:Jwt__?SigningKey|POSTGRES_PASSWORD|Password|GARMIN_API_KEY)'
                        r'\s*:\s*["\']?([^\s"\']+)["\']?', line)
                    if m:
                        ALLOWLIST.add(m.group(1).strip("\"'"))
        except Exception:
            continue


# --- паттерны (значение захватываем, но НЕ печатаем) ---
SIGNING = re.compile(r'(?:Jwt[:__]?)?SigningKey\s*[=:]\s*["\']?([A-Za-z0-9+/=_-]{20,})["\']?', re.I)
GARMIN = re.compile(r'GARMIN_API_KEY\s*=\s*["\']?([A-Za-z0-9_./+-]{6,})', re.I)
PROJECT = re.compile(r'PROJECT_PAT\s*=\s*["\']?([A-Za-z0-9_./+-]{6,})', re.I)
PASSWORD = re.compile(r'(?:password|passwd)\s*[=:]\s*["\']?([A-Za-z0-9_./+-]{8,})', re.I)
B64 = re.compile(r'[A-Za-z0-9+/]{32,}={1,2}')
HEX = re.compile(r'\b[0-9a-f]{32,}\b')

KEY_PATTERNS = [("SigningKey", SIGNING), ("GARMIN_API_KEY", GARMIN),
                ("PROJECT_PAT", PROJECT), ("password", PASSWORD)]
CHAT_PATTERNS = [("base64", B64), ("hex", HEX)]


def should_skip(path):
    p = path.replace("\\", "/")
    if os.path.basename(path) in SKIP_FILES:
        return True
    return any(x in p for x in ("/bin/", "/obj/", "/node_modules/", "/.git/"))


def scan_file(path):
    findings = []
    if should_skip(path):
        return findings
    is_chat = path.replace("\\", "/").startswith(CHAT_DIR.replace("\\", "/"))
    try:
        with open(path, encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
    except Exception:
        return findings
    for i, line in enumerate(lines, 1):
        for name, rx in KEY_PATTERNS:
            for m in rx.finditer(line):
                if m.group(1) in ALLOWLIST or "..." in m.group(1):
                    continue
                findings.append((name, i))
        if is_chat:
            for name, rx in CHAT_PATTERNS:
                for m in rx.finditer(line):
                    if m.group(0) in ALLOWLIST:
                        continue
                    findings.append((name, i))
    return findings


def main():
    collect_allowlist()
    targets = []
    targets += glob.glob(os.path.join(ROOT, "5-tech-backlog", "**", "*.md"), recursive=True)
    for name in ("README.md", "CHANGELOG.md", "AGENTS.md"):
        for path in (os.path.join(ROOT, name), os.path.join(ROOT, "XDiary", name)):
            if os.path.isfile(path):
                targets.append(path)
    for ext in ("*.cs", "*.json", "*.yml", "*.yaml", "*.sh", "*.py"):
        targets += glob.glob(os.path.join(ROOT, "**", ext), recursive=True)

    errors = []
    seen = set()
    for path in sorted(set(targets)):
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        for name, lineno in scan_file(path):
            key = (rel, lineno, name)
            if key in seen:
                continue
            seen.add(key)
            errors.append("%s:%d: %s" % (rel, lineno, name))

    if errors:
        print("утечек секретов: %d" % len(errors), file=sys.stderr)
        for e in errors[:50]:
            print("  " + e, file=sys.stderr)
        if len(errors) > 50:
            print("  ... ещё %d" % (len(errors) - 50), file=sys.stderr)
        return 1
    print("секретов не найдено (allowlist: %d dev-ключей)" % len(ALLOWLIST))
    return 0


if __name__ == "__main__":
    sys.exit(main())
