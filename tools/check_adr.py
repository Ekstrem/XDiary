#!/usr/bin/env python3
# КОПИЯ из LoopMaker/check_adr.py (источник правды там; правка — в обоих местах).
"""Гейт формата ADR (PDR-21/D-17): шаблон + протокол согласования.

Зачем: шаблон ADR есть, но «написали по шаблону» проверяется только глазами. Этот гейт
ловит новые ADR без обязательных разделов: варианты (>=3), протокол по ролям, комплаенс.

Критерий «новый/старый» ОДИН — дата создания файла в git (`git log --diff-filter=AR`,
а не поле «Дата»: его можно сдвинуть при правке). ADR, созданный раньше `--cutoff`
(по умолчанию 2026-11-08), при правке даёт предупреждения, а не ошибки — иначе правка
одной ссылки в старом ADR требовала бы переписать весь документ (дефект гейта, §5.4).
`--since` — алиас для совместимости с документацией, но значение МЕНЬШЕ нормы не
принимается: иначе «новыми» станут все 23 старых ADR и гейт покраснеет на истории.

КОПИЯ в проекте: источник правды — LoopMaker/tools/check_adr.py.
"""
import argparse
import io
import datetime
import os
import re
import subprocess
import sys

STATUSES = ("Предложено", "Принято", "Отклонено", "Заменено")
REQUIRED_SECTIONS = ("контекст", "варианты решений", "выбранное решение", "последствия")
DATE_RE = re.compile(r"\*\*Дата:\*\*\s*(\d{4}-\d{2}-\d{2})")
STATUS_RE = re.compile(r"\*\*Статус:\*\*\s*([А-Яа-яA-Za-z]+)")


PLUS_RE = re.compile(r"[-|]\s*\**(Плюсы|Минусы)", re.I)


def count_items(text, label):
    """Сколько пунктов в блоке «Плюсы/Минусы» (bullets или через `;`)."""
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if not re.search(label, line, re.I):
            continue
        # табличный вариант: | Плюсы | a; b; c |
        if "|" in line:
            cells = [c.strip() for c in line.split("|") if c.strip()]
            body = cells[-1] if cells else ""
            body = re.sub(r"^\**%s[^:]*:\**" % label, "", body, flags=re.I)
            items = [x for x in re.split(r"[;]", body) if x.strip()]
            if len(items) > 1:
                return len(items)
            return len([x for x in body.split(",") if x.strip()])
        # bullet-вариант: пункты могут быть и в этой же строке через «;», и списком ниже
        inline = re.sub(r"^[-|]\s*\**%s[^:]*:\**" % label, "", line, flags=re.I).strip()
        if inline:
            parts = [x for x in re.split(r"[;]", inline) if x.strip()]
            if len(parts) > 1:
                return len(parts)
            count = 1 if parts else 0
        else:
            count = 0
        for nxt in lines[i + 1:]:
            s = nxt.strip()
            if not s:
                continue
            if s.startswith(("- ", "* ")) and not re.search(r"\*\*(Плюсы|Минусы|Описание|Соответствие)", s):
                count += 1
                continue
            break
        return count
    return 0

# Норма владельца 2026-11-14 («предусловия вместо моратория», AGENTS §5.2): ADR, созданные
# ПОСЛЕ этой даты, обязаны иметь «Источник» и непустые «Связанные решения». Старые не краснеют —
# грандфазеринг по дате создания файла (иначе 29 ADR покраснеют и гейт отключат).
CANON_DATE = datetime.date(2026, 11, 14)

SOURCE_RE = re.compile(r"\*\*Источник:\*\*\s*([^\n]*)")
RELATED_RE = re.compile(r"\*\*Связанные решения:\*\*\s*([^\n]*)")
TASK_ID_RE = re.compile(r"\b(?:AR|BE|DO|FE|TE)-\d{2,3}\b")
ADR_REF_RE = re.compile(r"ADR-\d{3}")


def check_owner_norm(text):
    """Две проверки нормы владельца: «Источник» и «Связанные решения» (DO-070).

    Почему именно так:
      * «Источник» без задачи и без цитаты владельца — это ADR «из головы»: через месяц
        никто не восстановит, зачем он; допускаем либо ID задачи, либо дату-цитату.
      * «Связанные решения» пустое = ADR не пишется (норма §5.2): если ADR того же типа
        есть, нужно указать **почему меняется** и **соответствует ли комплаенсу прошлого**;
        явное «— (ADR того же типа нет)» — законный ответ.
    """
    errors = []

    m = SOURCE_RE.search(text)
    if not m or not m.group(1).strip():
        errors.append("нет поля «Источник» (нужен ID задачи вида AR-012 или цитата владельца с датой)")
    else:
        val = m.group(1)
        if not TASK_ID_RE.search(val) and not re.search(r"\d{4}-\d{2}-\d{2}", val):
            errors.append("«Источник» не содержит ни ID задачи (AR/BE/DO/FE/TE-NNN), ни цитаты владельца с датой")

    m2 = RELATED_RE.search(text)
    if not m2 or not m2.group(1).strip():
        errors.append("нет поля «Связанные решения» (пусто = ADR не пишется)")
    else:
        val = m2.group(1).strip()
        has_adr = bool(ADR_REF_RE.search(val))
        if has_adr:
            low = val.lower()
            if "почему" not in low:
                errors.append("ссылка на ADR-XXX без «почему меняется»")
            if "комплаенс" not in low:
                errors.append("ссылка на ADR-XXX без «комплаенс прошлого»")
    return errors


def check_file(path, strict=False):
    errors, warnings = [], []
    text = open(path, encoding="utf-8-sig").read()
    low = text.lower()

    m = STATUS_RE.search(text)
    if not m:
        errors.append("нет поля «Статус»")
    elif m.group(1) not in STATUSES:
        errors.append("статус «%s» не из списка: %s" % (m.group(1), ", ".join(STATUSES)))

    if not DATE_RE.search(text):
        # Различаем «поля нет» и «поле есть, но не дата»: иначе роль с плейсхолдером
        # YYYY-MM-DD видит «нет поля», хотя поле есть (нашёл @LM при проверке скелета).
        m_date = re.search(r"\*\*Дата:\*\*\s*([^\n]*)", text)
        if m_date:
            errors.append("поле «Дата» не в формате YYYY-MM-DD (значение: %s) — подставь реальную дату, например 2026-11-09, а не плейсхолдер из шаблона"
                          % m_date.group(1).strip()[:40])
        else:
            errors.append("нет поля «Дата: YYYY-MM-DD» — добавь строку `- **Дата:** 2026-11-09` (это поле обязательно, плейсхолдер не считается)")

    for sec in REQUIRED_SECTIONS:
        if sec not in low:
            errors.append("нет раздела «%s»" % sec.capitalize())

    variants = len(re.findall(r"^###\s+вариант\s+[a-zа-я]", low, re.M))
    if variants < 3:
        errors.append("вариантов %d, нужно >= 3 (### Вариант A/B/C)" % variants)

    if "принял" not in low or "отклонил" not in low:
        errors.append("нет протокола по ролям (столбцы «Принял»/«Отклонил»)")
    if "комплаенс" not in low:
        errors.append("нет раздела «Комплаенс» (условия пересмотра, кто проверяет)")

    # «Плюсы/Минусы» исчерпывающие: один пункт — предупреждение, не ошибка (@TL 02:11).
    blocks = re.split(r"(?im)^###\s+вариант", text)[1:]
    for idx, block in enumerate(blocks, 1):
        for label in ("Плюсы", "Минусы"):
            n = count_items(block, label)
            if n and n < 2:
                warnings.append("вариант %d: «%s» — %d пункт(а), нужен исчерпывающий список"
                                % (idx, label, n))

    if strict:
        if "заменяет" not in low:
            warnings.append("нет поля «Заменяет» (для цепочки замен; пустое значение — «—»)")
        if "владелец решения" not in low:
            warnings.append("нет поля «Владелец решения: @роль»")
    return errors, warnings


def iter_adrs(adr_dir):
    for name in sorted(os.listdir(adr_dir)):
        low = name.lower()
        if low.startswith("adr-") and low.endswith(".md") and "template" not in low:
            yield name, os.path.join(adr_dir, name)


CUTOFF_DEFAULT = "2026-11-08"   # ADR, созданные раньше, грандфазерятся (warning, не error)


def parse_creation_log(text):
    """Разбор `git log --diff-filter=AR --name-status --format=@@%ct`.

    Возвращает путь → epoch ПЕРВОГО появления (A — добавлен, R — переименован).
    Для R в name-status две строки-пути (старый и новый) — учитываем оба, иначе
    переименованный старый ADR считался бы новым и молча получал error (@AR 09:26 п.3).
    """
    m = {}
    ts = None
    for line in text.splitlines():
        if line.startswith("@@"):
            ts = int(line[2:].strip() or 0)
            continue
        if not line.strip() or ts is None:
            continue
        parts = line.split("\t")
        for p in parts[1:]:
            if p.endswith(".md"):
                m[p] = min(m.get(p, ts), ts)
    return m


def creation_map(adr_dir):
    """ОДИН git-вызов на весь каталог (кэш), а не O(N) вызовов в цикле (@AR 09:26 п.1)."""
    out = subprocess.run(["git", "log", "--diff-filter=AR", "--name-status",
                          "--format=@@%ct", "--", adr_dir],
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        return {}
    return parse_creation_log(out.stdout)


def created_date(path, cmap):
    """Дата создания: сначала пакетная карта, потом одиночный git, потом поле «Дата»."""
    rel = path.replace(os.sep, "/")
    epoch = cmap.get(rel)
    if epoch is None:
        out = subprocess.run(["git", "log", "--diff-filter=A", "--format=%ct", "--", path],
                             capture_output=True, text=True, encoding="utf-8", errors="replace")
        times = [int(x) for x in (out.stdout or "").split() if x.strip().isdigit()]
        epoch = times[-1] if times else None
    if epoch is not None:
        return datetime.datetime.fromtimestamp(epoch, datetime.timezone.utc).date()
    dm = DATE_RE.search(open(path, encoding="utf-8-sig").read())
    try:
        return datetime.date.fromisoformat(dm.group(1)) if dm else None
    except ValueError:
        return None


def downgrade_note(cutoff):
    """Текст «ошибки понижены». Вынесен и защищён от None (@TL 11:05).

    Пока строка формировалась инлайн с `args.cutoff`, при default=None в сообщении
    печаталось «создан до None» — гейт работал, а текст врал. Теперь None невозможен:
    функция падает, а `--selftest` проверяет сам ТЕКСТ, а не только rc.
    """
    if cutoff is None:
        raise ValueError("cutoff не задан — сообщение напечатало бы None")
    return ("старый ADR (создан до %s): ошибки понижены до предупреждений — "
            "полный шаблон нужен только при содержательном переписывании" % cutoff)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Гейт формата ADR")
    ap.add_argument("--dir", default=None, help="папка ADL (по умолчанию 04-architecture/ADL)")
    ap.add_argument("--cutoff", default=None,
                    help="порог: ADR, созданные раньше, грандфазерятся (warning). "
                         "По умолчанию %s" % CUTOFF_DEFAULT)
    ap.add_argument("--since", default=None,
                    help="алиас --cutoff для совместимости (AGENTS §5.2); значение меньше "
                         "нормы %s не принимается" % CUTOFF_DEFAULT)
    ap.add_argument("--strict", action="store_true", help="предупреждения считать ошибками")
    ap.add_argument("--strict-empty", action="store_true",
                    help="0 проверенных ADR считать НЕ ИЗМЕРЕНО (rc=2), а не «в норме»")
    ap.add_argument("--range", default=None,
                    help="git-диапазон (a..b): проверять только изменённые ADR")
    ap.add_argument("--selftest", action="store_true", help="негативный контроль (D-12)")
    ap.add_argument("--no-owner-norm", action="store_true",
                    help="не проверять норму владельца 2026-11-14 (Источник/Связанные решения)")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    adr_dir = args.dir or os.path.join("04-architecture", "ADL")
    if not os.path.isdir(adr_dir):
        print("check_adr: нет папки %s" % adr_dir)
        return 2

    ranged = None
    if args.range:
        out = subprocess.run(["git", "diff", "--name-only", args.range],
                             capture_output=True, text=True, encoding="utf-8",
                             errors="replace")
        if out.returncode != 0:
            print("check_adr: git diff %s не сработал: %s"
                  % (args.range, (out.stderr or "").strip()[:120]))
            return 2
        wanted = {line.strip().split("/")[-1] for line in out.stdout.splitlines() if line.strip()}
        ranged = {n for n in wanted
                  if n.lower().startswith("adr-") and n.lower().endswith(".md")
                  and "template" not in n.lower()}
        if not ranged:
            print("check_adr: в диапазоне %s изменённых ADR нет" % args.range)
            return 0

    norm = datetime.date.fromisoformat(CUTOFF_DEFAULT)
    if args.cutoff:
        cutoff = datetime.date.fromisoformat(args.cutoff)
    elif args.since:
        cutoff = datetime.date.fromisoformat(args.since)
        if cutoff < norm:
            print("check_adr: --since %s раньше нормы %s — тогда «новыми» станут все старые "
                  "ADR и гейт покраснеет на истории (это дефект гейта, §5.4). "
                  "Если действительно нужен строгий прогон — используй --cutoff %s"
                  % (cutoff, norm, cutoff))
            return 2
    else:
        cutoff = norm
    cmap = creation_map(adr_dir)
    checked = skipped = downgraded = 0
    bad = 0
    for name, path in iter_adrs(adr_dir):
        if ranged is not None and name not in ranged:
            continue
        # «новый vs старый» — ТОЛЬКО дата создания в git (кэш), фолбэк — поле «Дата».
        created = created_date(path, cmap)
        is_old = created is not None and created < cutoff
        if ranged is None and is_old:
            skipped += 1
            continue
        checked += 1
        errors, warnings = check_file(path, args.strict)
        # DO-070: норма владельца — только для ADR, созданных после даты канона.
        if not args.no_owner_norm and created is not None and created >= CANON_DATE:
            errors += check_owner_norm(open(path, encoding="utf-8-sig").read())
        if is_old and errors:
            # Грандфазеринг по дате создания: правка старого ADR не должна красить гейт.
            # Иначе «исправить одну ссылку в ADR-008» требует переписать весь документ
            # (стимул не трогать доки — класс «доки расходятся с кодом»). @TL 08:41 (б) + @AR 08:42 (по дате создания).
            warnings = warnings + [downgrade_note(cutoff)] + errors
            errors = []
            downgraded += 1
        if errors or (args.strict and warnings):
            bad += 1
            print("✗ %s" % name)
            for e in errors:
                print("   ошибка: %s" % e)
            for w in warnings:
                print("   предупреждение: %s" % w)
        elif warnings:
            print("⚠ %s: %s" % (name, "; ".join(warnings)))

    print("check_adr: проверено %d (старых пропущено %d, старых с понижением %d), нарушений %d"
          % (checked, skipped, downgraded, bad))

    # 0 проверенных ADR — это «не смотрел», а не «чисто» (@AR 2026-11-05 17:45, DO-093).
    if checked == 0:
        _msg = ("ПРОВЕРЕНО 0 ADR — гейт НЕ СМОТРЕЛ документы (граница %s)" % cutoff)
        print("::warning::check_adr: " + _msg)
        if getattr(args, "strict_empty", False):
            print("::error::check_adr: НЕ ИЗМЕРЕНО — " + _msg)
            return 2
    return 1 if bad else 0


GOOD = """# ADR-999: тест

- **Статус:** Предложено
- **Дата:** 2026-11-08
- **Заменяет:** —
- **Владелец решения:** @AR

## 1. Контекст
x

## 2. Варианты решений
### Вариант A — a
### Вариант B — b
### Вариант C — c

## 3. Выбранное решение
### 3.1 Протокол согласования
| Роль | Принял | Отклонил | Воздержался |
|---|---|---|---|
| @AR | ✅ | | |

### 3.3 Комплаенс
| Кто проверяет | @AR |

## 4. Последствия
x
"""

BAD = """# ADR-998: тест-плохой

- **Статус:** Придумано
- **Дата:** 2026-11-08

## 1. Контекст
x
"""


def selftest():
    import shutil
    ok = True
    tmp = os.path.join(os.getcwd(), ".tmp-check-adr")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp, exist_ok=True)
    if True:
        good = os.path.join(tmp, "adr-999-test.md")
        bad = os.path.join(tmp, "adr-998-bad.md")
        open(good, "w", encoding="utf-8", newline="\n").write(GOOD)
        open(bad, "w", encoding="utf-8", newline="\n").write(BAD)

        e_good, w_good = check_file(good, strict=True)
        e_bad, w_bad = check_file(bad, strict=True)
        print("  [%s] хороший ADR -> ошибок %d, предупреждений %d"
              % ("OK " if not e_good and not w_good else "FAIL", len(e_good), len(w_good)))
        ok = ok and not e_good and not w_good
        print("  [%s] плохой ADR -> ошибок %d (ожидали >=5): %s"
              % ("OK " if len(e_bad) >= 5 else "FAIL", len(e_bad), "; ".join(e_bad[:3])))
        ok = ok and len(e_bad) >= 5
        # старый ADR не проверяется
        sys.argv = ["check_adr.py", "--dir", tmp, "--since", "2026-12-01"]
        rc = main()
        print("  [%s] старые пропущены -> rc=%d" % ("OK " if rc == 0 else "FAIL", rc))
        ok = ok and rc == 0
    shutil.rmtree(tmp, ignore_errors=True)

    # Текст ПРЕДУПРЕЖДЕНИЯ о 0 проверенных (@TL 2026-11-05 15:34): подкласс «гейт считает верно,
    # а текст врёт». Ровно так я напечатал «граница None», взяв args.cutoff вместо вычисленного.
    # Теперь это ловится автоматически: в тексте должна быть ВЫЧИСЛЕННАЯ граница, а не None.
    tmp2 = os.path.join(os.getcwd(), ".tmp-check-adr-empty")
    shutil.rmtree(tmp2, ignore_errors=True)
    os.makedirs(tmp2, exist_ok=True)
    open(os.path.join(tmp2, "adr-997-old.md"), "w", encoding="utf-8", newline="\n").write(BAD)
    saved_argv = sys.argv
    sys.argv = ["check_adr.py", "--dir", tmp2, "--cutoff", "2099-01-01"]
    buf = io.StringIO()
    old_stdout = sys.stdout
    sys.stdout = buf
    try:
        rc_empty = main()
    finally:
        sys.stdout = old_stdout
        sys.argv = saved_argv
    text = buf.getvalue()
    has_date = "граница 2099-01-01" in text
    no_none = "None" not in text
    print("  [%s] текст при 0 ADR: граница вычислена=%s, без None=%s, rc=%d"
          % ("OK " if has_date and no_none and rc_empty == 0 else "FAIL",
             has_date, no_none, rc_empty))
    ok = ok and has_date and no_none and rc_empty == 0
    shutil.rmtree(tmp2, ignore_errors=True)

    # Текст сообщения — отдельная проверка (@TL 11:05): гейт может быть прав, а ТЕКСТ врать.
    # Раньше здесь печаталось «создан до None» при default --cutoff.
    try:
        note = downgrade_note(datetime.date(2026, 11, 8))
        ok_text = ("2026-11-08" in note) and ("None" not in note)
    except Exception as exc:  # noqa: BLE001
        ok_text = False
        note = "исключение: %s" % exc
    print("  [%s] текст понижения: %r" % ("OK " if ok_text else "FAIL", note[:70]))
    ok = ok and ok_text

    try:
        downgrade_note(None)
        print("  [FAIL] downgrade_note(None) не упал — снова напечатали бы None")
        ok = False
    except ValueError:
        print("  [OK ] downgrade_note(None) -> ValueError (None напечатать нельзя)")

    # --- DO-070: норма владельца (Источник + Связанные решения) ---
    base_bad = "# ADR-998\n\n- **Статус:** Предложено\n- **Дата:** 2026-11-16\n"
    cases = [
        ("нет «Источник»", base_bad, "Источник", True),
        ("пустые «Связанные решения»", base_bad + "- **Источник:** AR-012\n",
         "Связанные решения", True),
        ("«— (ADR того же типа нет)»", base_bad + "- **Источник:** AR-012\n"
         "- **Связанные решения:** — (ADR того же типа нет)\n", None, False),
        ("ADR-XXX без «комплаенс прошлого»", base_bad + "- **Источник:** AR-012\n"
         "- **Связанные решения:** меняет ADR-020; почему — факты другие\n", "комплаенс", True),
        ("полная ссылка", base_bad + "- **Источник:** owner 2026-11-14\n"
         "- **Связанные решения:** меняет ADR-020 — почему меняется: механика сменилась; "
         "комплаенс прошлого сохранён\n", None, False),
    ]
    for title, text, needle, expect_bad in cases:
        errs = check_owner_norm(text)
        got_bad = bool(errs) if needle is None else any(needle in e for e in errs)
        passed = got_bad == expect_bad
        print("  [%s] DO-070: %s -> %s" % ("OK " if passed else "FAIL", title,
                                           "; ".join(errs)[:90] or "чисто"))
        ok = ok and passed

    print("check_adr: самопроверка %s" % ("пройдена" if ok else "ПРОВАЛЕНА"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())