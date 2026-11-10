#!/usr/bin/env python3
"""check_shell_blocks.py — проверка баланса блоков shell-скрипта БЕЗ bash.

Зачем: в песочнице Windows `bash` не стартует (msys: `couldn't create signal pipe`), поэтому `bash -n` недоступен,
а shell-скрипты править всё равно нужно. Гейт ловит самый частый класс поломки — незакрытый/лишний
`fi`/`done`/`esac` — **не требуя интерпретатора**.

Что учитывается (иначе гейт даёт ложные находки, `@DO 2026-11-10 14:46`: 16 из 17 находок были ложными):
  * **heredoc** (`<<EOF`, `<<'PY'`, `<<-"TAG"`) — тело до закрывающего тега не разбирается;
  * **кавычки** (`'…'`, `"…"`) — токены внутри строк не считаются;
  * **комментарии** (`# …`) — отбрасываются до разбора строки;
  * `if/for/while/until/case` считаются только в позиции команды (после `; & | then do else`).

CRLF: по умолчанию **предупреждение**, а не ошибка: в рабочем дереве Windows git может отдавать CRLF
(`core.autocrlf`), а в блобах `origin` — LF. `--strict-crlf` делает это ошибкой для тех, кому нужно.

Коды: `0` — сошлось (возможны `::warning::`), `1` — есть ошибка блока, `2` — нечего проверять/файл не найден.
`--selftest` доказывает обе стороны: валидные проходят (включая heredoc и кавычки), сломанные — ловятся.
"""
import argparse
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
# Позиция команды: начало строки, после ; & | then do else, а также после `$(` и обратной кавычки —
# иначе инлайн `$(for …; do …; done)` даёт ложное «done без открывающего блока» (@DO 14:46).
CMD_OPEN = re.compile(r"(?:^|[;&|]\s*|\bthen\s+|\bdo\s+|\belse\s+|\$\(\s*|`\s*)(%s)\b"
                      % "|".join(OPENERS))
CMD_CLOSE = re.compile(r"(?:^|[;&|]\s*|\belse\s+|\$\(\s*|`\s*)(fi|done|esac)\b")
HEREDOC = re.compile(r"<<-?\s*(['\"]?)([A-Za-z_][A-Za-z0-9_]*)\1")


def strip_quotes_and_comment(line):
    """Убирает содержимое кавычек и комментарий, сохраняя позиции вне кавычек (для поиска токенов)."""
    out = []
    i = 0
    n = len(line)
    quote = None
    while i < n:
        ch = line[i]
        if quote:
            if ch == quote:
                quote = None
            i += 1
            continue
        if ch in ("'", '"'):
            quote = ch
            i += 1
            continue
        if ch == "#" and (i == 0 or line[i - 1] in " \t;&|("):
            break
        out.append(ch)
        i += 1
    return "".join(out)


def check(path):
    """Возвращает (errors, warnings)."""
    try:
        raw = open(path, "rb").read()
    except OSError as exc:
        return [f"{path}: не читается: {exc}"], []
    text = raw.decode("utf-8", errors="replace")
    errors, warnings = [], []
    if not text.startswith("#!"):
        warnings.append(f"{path}: нет shebang в первой строке")
    if b"\r\n" in raw:
        warnings.append(f"{path}: CRLF в рабочем дереве (в блобах origin обычно LF — это свойство autocrlf, "
                        f"а не дефект скрипта; `--strict-crlf` делает ошибкой)")

    stack = []
    heredoc_tag = None
    for n, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if heredoc_tag is not None:
            if stripped == heredoc_tag:
                heredoc_tag = None
            continue
        m = HEREDOC.search(line)
        if m:
            heredoc_tag = m.group(2)
            stripped = line[: m.start()]
        s = strip_quotes_and_comment(stripped).strip()
        if not s:
            continue
        for mo in CMD_OPEN.finditer(s):
            stack.append((mo.group(1), n))
        for mo in CMD_CLOSE.finditer(s):
            kw = mo.group(1)
            if not stack:
                errors.append(f"{path}:{n}: `{kw}` без открывающего блока")
                continue
            top_kw, top_n = stack.pop()
            if top_kw not in CLOSERS[kw]:
                errors.append(f"{path}:{n}: `{kw}` закрывает `{top_kw}` (открыт на строке {top_n})")
    for kw, n in stack:
        errors.append(f"{path}:{n}: `{kw}` не закрыт")
    return errors, warnings


def selftest():
    ok = total = 0

    def case(name, content, want_errors):
        nonlocal ok, total
        total += 1
        fd, path = tempfile.mkstemp(suffix=".sh")
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content)
            errors, _warnings = check(path)
        finally:
            os.unlink(path)
        if (len(errors) >= want_errors) if want_errors else (not errors):
            ok += 1
        else:
            print(f"  [FAIL] {name}: errors={errors}, want={want_errors}")

    case("валидный", "#!/usr/bin/env bash\nset -e\nif true; then\n  echo ok\nfi\n", 0)
    case("for/done", "#!/usr/bin/env bash\nfor i in 1 2; do\n  echo $i\ndone\n", 0)
    case("heredoc с if/fi внутри — НЕ ловится",
         "#!/usr/bin/env bash\npython3 - <<'PY'\nif x:\n    print('fi')\nPY\necho done\n", 0)
    case("heredoc <<EOF без кавычек",
         "#!/usr/bin/env bash\ncat <<EOF\nfor i in 1; do\nEOF\n", 0)
    case("кавычки: echo \"fi\" — НЕ ловится",
         "#!/usr/bin/env bash\necho \"fi done esac\"\n", 0)
    case("комментарий: # if ... — НЕ ловится",
         "#!/usr/bin/env bash\n# if true; then\necho ok\n", 0)
    case("незакрытый if", "#!/usr/bin/env bash\nif true; then\n  echo x\n", 1)
    case("fi закрывает for", "#!/usr/bin/env bash\nfor i in 1; do\nfi\n", 1)
    case("лишний done", "#!/usr/bin/env bash\ndone\n", 1)
    case("case без esac", "#!/usr/bin/env bash\ncase $x in\na) echo a ;;\n", 1)
    case("инлайн $(for …; do …; done) — НЕ ловится",
         "#!/usr/bin/env bash\necho $(for kv in a b; do printf 'x' \"$kv\"; done)\n", 0)

    print(f"check_shell_blocks --selftest: {ok}/{total}")
    return 0 if ok == total else 1


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*", help="shell-скрипты для проверки")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict-crlf", action="store_true", help="CRLF считать ошибкой (по умолчанию — warning)")
    a = ap.parse_args(argv)
    print("::notice::check_shell_blocks: старт · argv=%s" % (" ".join(sys.argv[1:]) or "—"), flush=True)
    if a.selftest:
        return selftest()
    if not a.paths:
        print("::warning::check_shell_blocks: не переданы пути — нечего проверять (rc=2)")
        return 2
    errors, warnings = [], []
    for p in a.paths:
        e, w = check(p)
        errors.extend(e)
        warnings.extend(w)
    crlf = [w for w in warnings if "CRLF" in w]
    warnings = [w for w in warnings if "CRLF" not in w]
    if a.strict_crlf:
        errors.extend(crlf)
    else:
        for w in crlf:
            print("::warning::check_shell_blocks: " + w)
    for w in warnings:
        print("::warning::check_shell_blocks: " + w)
    for e in errors:
        print("::error::check_shell_blocks: " + e)
    if errors:
        print("чек-лист: %d ошибок в %d файл(ах)" % (len(errors), len(a.paths)))
        return 1
    print("check_shell_blocks: блоки сбалансированы (%d файл(ов), предупреждений %d)"
          % (len(a.paths), len(warnings) + (0 if a.strict_crlf else len(crlf))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
