#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""TE-100: гейт «`optional:` ⟺ nullable в командном record».

Зачем (из `TE-099`): канон держал `photo`/`notes` у `Partners.UpdateProfile` в `optional:`, а в командном
`record` поля были `string` (non-nullable) — потребитель, поверив канону, получал `400`. Рантайм нашёл это
одним вызовом, но проверка остальных POST стоила бы десятков вызовов с токеном и партнёром. Статика отвечает
на тот же вопрос за секунды и может идти до браузера.

Что проверяется: для каждой операции канона с непустым `request.optional` берётся командный `record`
(соответствие — в `OP_TO_RECORD`, потому что имя записи не выводится из пути) и каждое `optional`-поле
обязано быть объявлено nullable (`string?`, `Guid?`, `int?`, `List<…>?`). Non-nullable ⇒ `[ApiController]`
вернёт `400` при отсутствии поля, хотя канон обещает обратное.

Исходы: `rc=0` — обещание канона подтверждено; `rc=1` — нарушение (адрес `файл:поле`); `rc=2` — не измерено
(нет канона/записи). Исключения — только через `KNOWN_GAPS` с причиной, печатаются числом.
"""
from __future__ import annotations

import argparse
import io
import pathlib
import re
import sys

# cp1251-консоль без PYTHONIOENCODING: печать `⟺` в метках `--selftest` падала с
# UnicodeEncodeError — rc=1 без строки вердикта (класс @AR 2026-11-13 00:24). Страховка на печать;
# тексты и ожидания не меняем.
try:
    sys.stdout.reconfigure(errors="replace")
    sys.stderr.reconfigure(errors="replace")
except Exception:  # noqa: BLE001
    pass

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
INDEX = REPO_ROOT / "04-architecture" / "api-index.yaml"
SRC = REPO_ROOT / "6-msa"

# Соответствие «операция канона → командный record». Имя записи не выводится из пути (`/clients/me/profile`
# → `UpdateProfile`), поэтому таблица явная: новая операция без строки попадёт в «не измерено», а не в «ok».
OP_TO_RECORD = {
    ("POST", "/api/v1/clients/me/profile"): ("ClientCommands.cs", "UpdateProfileCommand"),
    ("POST", "/api/v1/privacy/pin/change"): ("PrivacyCommands.cs", "ChangePinCommand"),
    ("POST", "/api/v1/privacy/pin/remove"): ("PrivacyCommands.cs", "RemovePinCommand"),
    ("POST", "/api/v1/privacy/pin/set"): ("PrivacyCommands.cs", "SetPinCommand"),
    # класс, не `record`: тело события объявлено как `class AnalyticsEvent` (Analytics.Api/Precompute/JournalRecord.cs)
    ("POST", "/api/v1/analytics/events"): ("JournalRecord.cs", "AnalyticsEvent"),
}

# Исключения: («операция», «поле») → причина. Печатаются числом, не прячутся.
KNOWN_GAPS: dict[tuple[str, str], str] = {}


def banner(argv):
    """Первая строка — как в остальных измерителях (@TL 2026-11-10 17:05)."""
    try:
        import yaml
        yaml_state = getattr(yaml, "__version__", "есть")
    except Exception:  # noqa: BLE001
        yaml_state = "НЕТ"
    print("::notice::check_api_index_optional: старт · python=%s · yaml=%s · cwd=%s · argv=%s"
          % (sys.version.split()[0], yaml_state, pathlib.Path.cwd(), " ".join(argv)))


def load_ops(path=None):
    import yaml
    path = pathlib.Path(path or INDEX)
    doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    ops = []

    def walk(node):
        if isinstance(node, dict):
            req = node.get("request")
            if isinstance(req, dict) and req.get("optional"):
                ops.append({"bc": node.get("bc"), "method": node.get("method"),
                            "path": node.get("path"), "optional": list(req["optional"])})
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(doc)
    return ops


def find_record(root, file_hint, record_name):
    """Ищет `record <record_name>` в `*<file_hint>`; возвращает (путь, фрагмент объявления).

    Записи в командах **позиционные** (`record X(string? A, string B) : ICommand;`), поэтому фрагмент —
    либо скобки параметров, либо тело `{…}`; парсер полей ниже понимает обе формы.
    """
    root = pathlib.Path(root)
    for path in root.rglob(file_hint):
        text = io.open(path, encoding="utf-8", errors="replace").read()
        m = re.search(r"(?:record|class)\s+%s\b" % re.escape(record_name), text)
        if not m:
            continue
        i = m.end()
        # до первого `{` (тело) или `;` (позиционная запись без тела), но не дальше 2 000 символов
        limit = min(len(text), i + 2000)
        brace = text.find("{", i)
        semi = text.find(";", i)
        if brace != -1 and (semi == -1 or brace < semi) and brace < limit:
            depth, j = 0, brace
            while j < limit:
                if text[j] == "{":
                    depth += 1
                elif text[j] == "}":
                    depth -= 1
                    if depth == 0:
                        break
                j += 1
            return path, text[m.start():j + 1]
        if semi != -1:
            return path, text[m.start():semi + 1]
        return path, text[m.start():limit]
    return None, None


def record_fields(snippet):
    """PascalCase-поле → тип. Понимает позиционные параметры и свойства `{ get; … }`."""
    fields = {}
    # позиционные параметры: `record Name(type a, type b)` (генерики внутри типа не ломают разбор)
    m = re.search(r"\((.*?)\)", snippet, re.S)
    if m:
        depth, buf, parts = 0, "", []
        for ch in m.group(1):
            if ch in "<([":
                depth += 1
            elif ch in ">)]":
                depth -= 1
            if ch == "," and depth == 0:
                parts.append(buf)
                buf = ""
            else:
                buf += ch
        if buf.strip():
            parts.append(buf)
        for part in parts:
            part = part.strip()
            if not part:
                continue
            bits = part.split()
            if len(bits) >= 2:
                fields[bits[-1]] = " ".join(bits[:-1]).strip()
    # свойства в теле записи
    for m in re.finditer(r"public\s+([\w<>\[\],\.\? ]+?)\s+(\w+)\s*\{\s*get", snippet):
        fields[m.group(2)] = m.group(1).strip()
    return fields


def _rel(path, fallback):
    """Относительный путь для квитанции; в selftest путь синтетический — отдаём подсказку."""
    try:
        return pathlib.Path(path).relative_to(REPO_ROOT).as_posix()
    except Exception:  # noqa: BLE001
        return str(fallback)


def pascal(name):
    return name[:1].upper() + name[1:]


VALUE_TYPES = {"int", "long", "short", "byte", "bool", "double", "decimal", "float", "DateTime",
               "DateTimeOffset", "TimeSpan", "Guid", "DateOnly", "TimeOnly"}


def is_nullable(type_name):
    t = type_name.replace(" ", "")
    return t.endswith("?") or t.startswith("List<") or t.startswith("IReadOnlyList<") or t.startswith("IEnumerable<")


def is_reference(type_name):
    """Ссылочный ли тип: `400` при отсутствии поля возможен только у ссылочных.

    `[ApiController]` считает non-nullable **ссылку** обязательной (`string`), а значимый тип (`int`, `Guid`)
    при отсутствии даёт `default` — то есть `optional` для него честно описан. Без этого различия гейт
    краснел бы на `AnalyticsEvent` (`int Stress` в `optional`) — ложная находка.
    """
    t = type_name.replace(" ", "").rstrip("?")
    base = t.split("<")[0]
    if base in VALUE_TYPES:
        return False
    if t.endswith("[]"):
        return True
    return True  # string и любые классы/записи проекта — ссылочные


def check(ops, root, mapping=None, finder=None):
    mapping = OP_TO_RECORD if mapping is None else mapping
    finder = finder or (lambda fh, rn: find_record(root, fh, rn))
    violations, unknown, checked = [], [], 0
    for op in ops:
        key = (op["method"], op["path"])
        target = mapping.get(key)
        if not target:
            unknown.append({"op": key, "why": "нет соответствия в OP_TO_RECORD"})
            continue
        path, body = finder(target[0], target[1])
        if body is None:
            unknown.append({"op": key, "why": "record %s не найден" % target[1]})
            continue
        fields = record_fields(body)
        for field in op["optional"]:
            prop = pascal(field)
            checked += 1
            if prop not in fields:
                unknown.append({"op": key, "why": "поля `%s` нет в %s" % (prop, target[1])})
                continue
            if is_nullable(fields[prop]) or not is_reference(fields[prop]):
                continue
            if (op["path"], field) in KNOWN_GAPS:
                continue
            violations.append({"op": key, "field": prop, "type": fields[prop],
                               "file": _rel(path, target[0]),
                               "known": (op["path"], field) in KNOWN_GAPS})
    return {"ops": len(ops), "checked": checked, "violations": violations,
            "unknown": unknown, "gaps": len(KNOWN_GAPS)}


def verdict(result, strict_unknown=False, strict=False):
    lines = []
    level = "::error::" if strict else "::warning::находка (не блокирует без `--strict`): "
    for v in result["violations"]:
        lines.append(level + "канон обещает необязательность, а поле non-nullable ⇒ `[ApiController]` "
                     "вернёт `400` при отсутствии: %s %s → %s `%s` (%s)"
                     % (v["op"][0], v["op"][1], v["file"], v["field"], v["type"]))
    for u in result["unknown"]:
        lines.append("::warning::не измерено: %s %s — %s" % (u["op"][0], u["op"][1], u["why"]))
    rc = 0
    if result["violations"] and strict:
        rc = 1
    elif result["checked"] == 0:
        # «зелёный на пустоте»: ни одного поля не проверено — это не измерение, а его отсутствие
        lines.append("::warning::не измерено: ни одно поле не проверено (0 операций с optional "
                     "или не найдены записи) — зелёный на пустоте не считается измерением")
        rc = 2
    elif result["unknown"] and strict_unknown:
        rc = 2
    lines.append("квитанция: операций с optional %d | полей проверено %d | нарушений %d | "
                 "не измерено %d | известных исключений %d"
                 % (result["ops"], result["checked"], len(result["violations"]),
                    len(result["unknown"]), result["gaps"]))
    return rc, lines


# ── selftest: норма / дефект / исключение ────────────────────────────────────────────────────────────
FIXTURE_NULLABLE = """
namespace X;
public sealed record UpdateProfile(string? Photo, string? Notes) : ICommand;
"""
FIXTURE_NONNULL = """
namespace X;
public sealed record UpdateProfile(string Photo, string? Notes) : ICommand;
"""


def case(label, body, optional, known=None, expect=0):
    """Прогоняет правило на синтетической записи; возвращает (ok, текст)."""
    global KNOWN_GAPS
    saved = KNOWN_GAPS
    KNOWN_GAPS = dict(known or {})
    try:
        mapping = {("POST", "/api/v1/x"): ("Commands.cs", "UpdateProfile")}
        ops = [{"bc": "X", "method": "POST", "path": "/api/v1/x", "optional": optional}]
        # без диска: в песочнице `tempfile` не может выставить права (WinError 5)
        res = check(ops, None, mapping, finder=lambda fh, rn: (pathlib.Path(fh), body))
        rc, _ = verdict(res, strict=True)
        ok = rc == expect
        return ok, "%-34s rc=%d (ждали %d, --strict)" % (label, rc, expect)
    finally:
        KNOWN_GAPS = saved


def selftest():
    cases = [
        ("норма: optional ⟺ string?", FIXTURE_NULLABLE, ["photo", "notes"], None, 0),
        ("дефект: optional + non-nullable", FIXTURE_NONNULL, ["photo", "notes"], None, 1),
        ("исключение в KNOWN_GAPS", FIXTURE_NONNULL, ["photo", "notes"],
         {("/api/v1/x", "photo"): "фикстура: известный разрыв до ADR"}, 0),
    ]
    passed = 0
    for label, body, optional, known, expect in cases:
        ok, text = case(label, body, optional, known, expect)
        print(("  [OK]   " if ok else "  [FAIL] ") + text)
        passed += 1 if ok else 0
    print("check_api_index_optional --selftest: %d/%d (норма / «optional с non-nullable» / исключение)"
          % (passed, len(cases)))
    return 0 if passed == len(cases) else 1


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    ap = argparse.ArgumentParser(description="optional: в каноне ⟺ nullable в командном record")
    ap.add_argument("--root", default=str(SRC), help="корень с исходниками сервисов (по умолчанию 6-msa)")
    ap.add_argument("--index", default=str(INDEX), help="путь к api-index.yaml")
    ap.add_argument("--strict", action="store_true",
                    help="блокирующий режим: находка ⇒ rc=1 (по умолчанию — отчёт, rc=0)")
    ap.add_argument("--strict-unknown", action="store_true", help="неизмеренное ⇒ rc=2 вместо 0")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--quiet-banner", action="store_true")
    a = ap.parse_args(argv)
    if not a.quiet_banner:
        banner(argv)
    if a.selftest:
        return selftest()
    try:
        ops = load_ops(a.index)
    except Exception as e:  # noqa: BLE001
        print("::warning::не измерено: канон недоступен/не разобран (%s)" % e)
        print("квитанция: НЕ ИЗМЕРЕНО")
        return 2
    result = check(ops, a.root)
    rc, lines = verdict(result, a.strict_unknown, a.strict)
    for line in lines:
        print(line)
    return rc


if __name__ == "__main__":
    sys.exit(main())
