#!/usr/bin/env python3
"""check_shell_blocks.py — проверка баланса блоков shell-скрипта БЕЗ bash.

Зачем: в песочнице Windows `bash` не стартует (msys: `couldn't create signal pipe`), поэтому обычный
`bash -n` недоступен, а править shell-скрипты всё равно нужно. Этот гейт ловит самый частый класс поломки —
незакрытый/лишний `fi`, `done`, `esac`, — и делает это без интерпретатора.

Проверки:
  1. `shebang` на первой строке;
  2. нет CRLF (иначе `bash` ругается на `\\r` в конце команд);
  3. баланс блоков: `if` → `fi`, `for|while|until` → `done`, `case` → `esac` (с учётом `elif/else`);
  4. закрытие всегда соответствует верхушке стека (не «fi закрывает for»).

Коды: `0` — всё сошлось, `1` — есть ошибка (печатает файл, строку и тип), `2` — нечего проверять/файл не найден.
`--selftest` доказывает обе стороны: валидный скрипт проходит, сломанный — ловится.
"""
import argparse
import io
import os
import re
import sys
import tempfile

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # noqa: BLE001
    pass

OPENERS = ("if", "for", "while", "until", "case")
CLOSERS = {"fi": ("if",), "done": ("for", "while", "until"), "esac": ("case",)}
# `if` и др. считаем только в начале команды (после ; & | then do else) — иначе ловим слова в тексте.
TOKEN = re.compile(r"(?:^|[;&|]\s*|\bthen\s+|\bdo\s+|\belse\s+)(%s)\b" % "|".join(OPENERS))
CLOSE = re.compile(r"(?:^|[;&|]\s*|\belse\s+)(fi|done|esac)\b")


def check(path):
    """Возвращает список проблем (пусто = ok)."""
    try:
        raw = open(path, "rb").read()
    except OSError as exc:
        return [f"не читается: {exc}"]
    text = raw.decode("utf-8", errors="replace")
    problems = []
    if not text.startswith("#!"):
        problems.append("нет shebang в первой строке")
    if b"\r\n" in raw:
        problems.append("CRLF в файле (bash увидит \\r в конце команд)")

    stack = []
    for n, line in enumerate(text.splitlines(), 1):
        s = line.strip()
        if not s or s.startswith("#"):
            continue
        for m in TOKEN.finditer(s):
            stack.append((m.group(1), n))
        for m in CLOSE.finditer(s):
            kw = m.group(1)
            if not stack:
                problems.append(f"{path}:{n}: `{kw}` без открывающего блока")
                continue
            top_kw, top_n = stack.pop()
            if top_kw not in CLOSERS[kw]:
                problems.append(f"{path}:{n}: `{kw}` закрывает `{top_kw}` (открыт на строке {top_n})")
    for kw, n in stack:
        problems.append(f"{path}:{n}: `{kw}` не закрыт")
    return problems


def selftest():
    ok = total = 0

    def case(name, content, want_problems):
        nonlocal ok, total
        total += 1
        fd, path = tempfile.mkstemp(suffix=".sh")
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content)
            got = check(path)
        finally:
            os.unlink(path)
        good = (not got) if want_problems == 0 else (len(got) >= want_problems)
        if good:
            ok += 1
        else:
            print(f"  [FAIL] {name}: got={got}, want_problems={want_problems}")

    case("валидный скрипт", "#!/usr/bin/env bash\nset -e\nif true; then\n  echo ok\nfi\n", 0)
    case("for/done валиден", "#!/usr/bin/env bash\nfor i in 1 2; do\n  echo $i\ndone\n", 0)
    case("незакрытый if", "#!/usr/bin/env bash\nif true; then\n  echo x\n", 1)
    case("fi закрывает for", "#!/usr/bin/env bash\nfor i in 1; do\nfi\n", 1)
    case("лишний done", "#!/usr/bin/env bash\ndone\n", 1)
    case("нет shebang", "echo hi\n", 1)

    print(f"check_shell_blocks --selftest: {ok}/{total}")
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*", help="shell-скрипты для проверки")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args(argv)
    print("::notice::check_shell_blocks: старт · argv=%s" % (" ".join(sys.argv[1:]) or "—"), flush=True)
    if a.selftest:
        return selftest()
    if not a.paths:
        print("::warning::check_shell_blocks: не переданы пути — нечего проверять (rc=2)")
        return 2
    all_problems = []
    for p in a.paths:
        all_problems.extend(check(p))
    for pr in all_problems:
        print("::error::check_shell_blocks: " + pr)
    if all_problems:
        print("чек-лист: %d проблем(ы) в %d файле(ах)" % (len(all_problems), len(a.paths)))
        return 1
    print("check_shell_blocks: блоки сбалансированы, CRLF нет, shebang на месте (%d файл(ов))" % len(a.paths))
    return 0


if __name__ == "__main__":
    sys.exit(main())
