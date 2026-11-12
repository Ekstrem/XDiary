#!/usr/bin/env python3
"""check_awaits.py — «go» считается выданным только после подтверждения адресатом.

Правило (@TL 2026-11-14 03:10): решение/«go» действует, только если после метки выдачи
адресат либо **ответил** («принято»/«принял»/«делаю» в своём outbox), либо **сделал
артефакт** (коммит с префиксом роли, упоминающий ID/тему). Иначе решение «висит»:
две роли за сутки простояли цикл на неподтверждённом «go».

Источник: `5-tech-backlog/tl/AWAIT.md` — таблица. Ожидаемые колонки (по имени, порядок
не важен): «кому» (роль), «что» (тема/ID), «метка» (YYYY-MM-DD HH:mm).

    python tools/check_awaits.py              # предупреждения в вывод
    python tools/check_awaits.py --strict     # предупреждения = rc=1
    python tools/check_awaits.py --selftest
"""
import argparse
from datetime import datetime
import os
import re
import subprocess
import sys

AWAIT = os.path.join("5-tech-backlog", "tl", "AWAIT.md")
ACCEPT = re.compile(r"(принято|принял|принимаю|делаю|взял|беру)", re.I)
STAMP = re.compile(r"(\d{4}-\d{2}-\d{2} \d{2}:\d{2})")
ROLE = re.compile(r"@?([a-z]{2})\b")
ROLE_PREFIX = {"ar": "ar:", "be": "be:", "do": "do:", "fe": "fe:", "te": "te:", "tl": "tl:"}


def parse_awaits(path):
    """[(role, what, stamp, line_no)] из markdown-таблицы."""
    rows = []
    if not os.path.exists(path):
        return rows
    header = None
    for n, line in enumerate(open(path, encoding="utf-8").read().splitlines(), 1):
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if header is None:
            header = [c.lower() for c in cells]
            continue
        if all(set(c) <= set("-: ") for c in cells if c):
            continue  # разделитель
        idx_role = idx_what = idx_stamp = idx_status = None
        for i, h in enumerate(header):
            if idx_role is None and ("кому" in h or "адресат" in h or "роль" in h):
                idx_role = i
            elif idx_what is None and ("что" in h or "тема" in h or "задач" in h or "go" in h):
                idx_what = i
            elif idx_stamp is None and ("метк" in h or "когда" in h or "выдач" in h):
                idx_stamp = i
            elif idx_status is None and ("статус" in h or "состояни" in h):
                idx_status = i
        if None in (idx_role, idx_what, idx_stamp) or len(cells) <= max(idx_role, idx_what, idx_stamp):
            continue
        m = STAMP.search(cells[idx_stamp])
        mr = ROLE.search(cells[idx_role].lower())
        if not m or not mr:
            continue
        done = False
        if idx_status is not None and idx_status < len(cells):
            cell = cells[idx_status].strip().lower()
            done = ("✅" in cell or "сделано" in cell or "закрыт" in cell or "принято" in cell
                    or "готово" in cell)
        rows.append((mr.group(1), cells[idx_what], m.group(1), n, done))
    return rows



def future_rows(rows, now=None):
    """[(role, what, stamp, line_no, minutes_ahead)] — метки выдачи В БУДУЩЕМ.

    `chat_accepts(role, after=stamp)` ищет подтверждение ПОСЛЕ метки, поэтому строка
    с меткой из будущего висит по построению: ответить на неё нельзя.
    Худший вид красного — когда «не ответили» и «ответить нельзя» неразличимы.
    """
    now = now or datetime.now()
    out = []
    for role, what, stamp, n, _done in rows:
        try:
            ts = datetime.strptime(stamp, "%Y-%m-%d %H:%M")
        except ValueError:
            continue
        if ts > now:
            out.append((role, what, stamp, n, int((ts - now).total_seconds() // 60)))
    return out

def bad_stamps(path):
    """[(stamp, line_no)] — метки с часом вне 0..23 (дефект данных генератора)."""
    out = []
    if not os.path.exists(path):
        return out
    for n, line in enumerate(open(path, encoding='utf-8').read().splitlines(), 1):
        if not line.strip().startswith('|'):
            continue
        m = STAMP.search(line)
        if not m:
            continue
        if int(m.group(1).split()[1].split(':')[0]) > 23:
            out.append((m.group(1), n))
    return out


def _age_minutes(stamp, now):
    """Возраст метки в минутах; inf, если метку не разобрать."""
    try:
        t = datetime.strptime(stamp, "%Y-%m-%d %H:%M")
        return (now - t).total_seconds() / 60.0
    except (ValueError, TypeError):
        return float("inf")


def _topic_tokens(what):
    """Ключи темы строки AWAIT: ID (`TE-049`, `DO-022`) и значимые слова.

    Нужны, чтобы «принял» зачитывалось только по теме. Дефект @TE 2026-11-13 01:35:
    строка про маркеры в `role/tl` закрывала `AWAIT` про `check-adr028-move.sh --post`
    и `TE-049`, потому что `chat_accepts` смотрел только роль и метку.
    """
    text = what or ""
    toks = re.findall(r"[A-Z]{2,3}-\d{2,4}", text)
    toks += [w for w in re.findall(r"[A-Za-zА-Яа-я_][\w.\-]{4,}", text)]
    out = []
    for t in toks:
        t = t.lower()
        if t not in out:
            out.append(t)
    return out[:8]


def chat_accepts(root, role, after, what=None):
    """Есть ли в chat/<role>.md сообщение после `after` со словом принятия ПО ТЕМЕ.

    `what=None` — прежнее поведение (только роль+метка), для обратной совместимости.
    """
    path = os.path.join(root, "5-tech-backlog", "chat", role + ".md")
    if not os.path.exists(path):
        return False
    toks = _topic_tokens(what) if what else []
    for line in open(path, encoding="utf-8").read().splitlines():
        m = STAMP.search(line)
        if not m or m.group(1) <= after:
            continue
        if not ACCEPT.search(line):
            continue
        if toks and not any(t in line.lower() for t in toks):
            continue          # «принял» не по этой теме — не подтверждение (@TE 01:35)
        return True
    return False


def commit_artifacts(root, role, topic):
    """Коммиты после… (упрощённо: любые) с префиксом роли и упоминанием темы."""
    prefix = ROLE_PREFIX.get(role)
    if not prefix:
        return False
    key = re.split(r"[^A-Za-z0-9_-]+", topic.strip())[0]
    if not key:
        return False
    out = subprocess.run(["git", "log", "--format=%s", "-n", "300"], cwd=root,
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    for subject in (out.stdout or "").splitlines():
        if subject.lower().startswith(prefix) and key.lower() in subject.lower():
            return True
    return False


def check(root, use_git=True, max_age_minutes=60, now=None):
    """use_git=False — только outbox (для selftest)."""
    rows = parse_awaits(os.path.join(root, AWAIT))
    hanging = []
    done_rows = 0
    now = now or datetime.now()
    for role, what, stamp, _, done in rows:
        if done:
            done_rows += 1
            continue
        if role == "ex":
            continue  # владелец — не роль пайплайна, outbox-обязанности нет (AN-03)
        if max_age_minutes is not None and 0 <= _age_minutes(stamp, now) < max_age_minutes:
            continue  # свежая выдача — окно отсрочки, не «висит» (AN-03)
        ok = chat_accepts(root, role, stamp, what)
        if not ok and use_git:
            ok = commit_artifacts(root, role, what)
        if not ok:
            hanging.append((role, what, stamp))
    return rows, hanging, done_rows


def selftest():
    import shutil
    ok = 0
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tmp-awaits")
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "tl"), exist_ok=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "chat"), exist_ok=True)

    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | ADR-028 перенос | 2026-11-13 19:00 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:30] → @TL: принял ADR-028, делаю\n")
    rows, hanging, _done = check(base, use_git=False)
    if len(rows) == 1 and not hanging:
        ok += 1
    else:
        print("  [FAIL] подтверждение не распознано: rows=%d hanging=%d" % (len(rows), len(hanging)))

    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 18:00] → @TL: принял (ДО метки — не считается)\n")
    rows, hanging, _done = check(base, use_git=False)
    if len(hanging) == 1:
        ok += 1
    else:
        print("  [FAIL] сообщение до метки зачлось как подтверждение")

    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | ADR-028 перенос | 2026-11-13 19:00 |\n"
        "| @DO | гейт awaited | 2026-11-13 19:30 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "do.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:40] → @TL: принято awaited, беру\n")
    rows, hanging, _done = check(base, use_git=False)
    if len(rows) == 2 and len(hanging) == 1 and hanging[0][0] == "te":
        ok += 1
    else:
        print("  [FAIL] смешанный случай: rows=%d hanging=%s" % (len(rows), hanging))

    shutil.rmtree(base, ignore_errors=True)
    # кейс: строка с ✅ в колонке «Статус» не считается висящей даже без подтверждения
    os.makedirs(os.path.dirname(os.path.join(base, AWAIT)), exist_ok=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "chat"), exist_ok=True)
    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка | статус |\n|---|---|---|---|\n"
        "| @DO | нечто | 2026-11-13 19:00 | ✅ сделано |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "do.md"), "w", encoding="utf-8").write("")
    rows, hanging, done = check(base, use_git=False)
    if len(rows) == 1 and not hanging and done == 1:
        ok += 1
    else:
        print("  [FAIL] ✅-строка учтена как висящая: rows=%d hanging=%s done=%d"
              % (len(rows), hanging, done))

    shutil.rmtree(base, ignore_errors=True)
    # кейс 5: свежая выдача (моложе окна) — не висит
    os.makedirs(os.path.dirname(os.path.join(base, AWAIT)), exist_ok=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "chat"), exist_ok=True)
    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | свежее | 2026-11-13 19:50 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write("")
    rows, hanging, _done = check(base, use_git=False, max_age_minutes=60,
                                 now=datetime(2026, 11, 13, 20, 0))
    if len(rows) == 1 and not hanging:
        ok += 1
    else:
        print("  [FAIL] свежая выдача зачтена висящей: hanging=%s" % hanging)

    # кейс 6: строка @EX — не считается висящей ролью
    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @EX | вход владельца | 2026-11-13 19:00 |\n")
    rows, hanging, _done = check(base, use_git=False, max_age_minutes=60,
                                 now=datetime(2026, 11, 13, 20, 0))
    if len(rows) == 1 and not hanging:
        ok += 1
    else:
        print("  [FAIL] строка @EX зачтена висящей: hanging=%s" % hanging)

    shutil.rmtree(base, ignore_errors=True)
    # --- кейсы «метка выдачи в будущем» (@TL 01:05: в патче не было самотеста) ---
    from datetime import datetime as _dt
    fut = future_rows([("do", "гейт awaited", "2026-11-14 05:10", 21, False)],
                      now=_dt(2026, 11, 13, 1, 0))
    if len(fut) == 1 and fut[0][4] > 1000:
        ok += 1
    else:
        print("  [FAIL] будущая метка не поймана: %s" % (fut,))
    fut2 = future_rows([("do", "старое", "2026-11-12 10:00", 22, False)],
                       now=_dt(2026, 11, 13, 1, 0))
    if fut2 == []:
        ok += 1
    else:
        print("  [FAIL] прошедшая метка принята за будущую: %s" % (fut2,))

    # --- дефект @TE 01:35: «принял» не по теме не должен закрывать AWAIT ---
    shutil.rmtree(base, ignore_errors=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "tl"), exist_ok=True)
    os.makedirs(os.path.join(base, "5-tech-backlog", "chat"), exist_ok=True)
    open(os.path.join(base, AWAIT), "w", encoding="utf-8").write(
        "| кому | что | метка |\n|---|---|---|\n| @TE | ADR-028 перенос | 2026-11-13 19:00 |\n")
    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:30] → @TL: принял маркеры в role/tl, делаю\n")
    rows, hanging, _done = check(base, use_git=False, max_age_minutes=0)
    if len(hanging) == 1:
        ok += 1
    else:
        print("  [FAIL] «принял» не по теме закрыл AWAIT: hanging=%s" % (hanging,))

    open(os.path.join(base, "5-tech-backlog", "chat", "te.md"), "w", encoding="utf-8").write(
        "- [2026-11-13 19:30] → @TL: принято: ADR-028 — беру\n")
    rows, hanging, _done = check(base, use_git=False, max_age_minutes=0)
    if not hanging:
        ok += 1
    else:
        print("  [FAIL] подтверждение ПО ТЕМЕ не зачтено: hanging=%s" % (hanging,))

    print("check_awaits --selftest: %d/10" % ok)
    return 0 if ok == 10 else 1


def _utf8_stdout():
    """Вывод в UTF-8 независимо от локали (TE 2026-11-14 23:47).

    На Windows с cp1251-консолью `print("… → …")` падал `UnicodeEncodeError`, и гейт
    краснел **из-за печати**, а не из-за содержания AWAIT.md. Это дефект инструмента:
    вердикт гейта не должен зависеть от кодировки терминала.
    """
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--max-age-minutes", type=int, default=60)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    _utf8_stdout()
    if a.selftest:
        return selftest()

    root = os.path.abspath(a.root)
    rows, hanging, done_rows = check(root, max_age_minutes=a.max_age_minutes)
    if not rows:
        print("check_awaits: AWAIT.md нет или таблица пуста — висящих решений 0")
        return 0
    bad = bad_stamps(os.path.join(root, AWAIT))
    for stamp, n in bad:
        print("::error::AWAIT.md:%d — метка вне диапазона (час>23): %s" % (n, stamp))
    future = future_rows(rows)
    for role, what, stamp, n, ahead in future:
        print("::error::AWAIT.md:%d — метка выдачи В БУДУЩЕМ: %s (+%d мин, @%s) — "
              "подтверждение невозможно, строка висит по построению" % (n, stamp, ahead, role))
    for role, what, stamp in hanging:
        print("::warning::висит: @%s — «%s» (выдано %s, подтверждения нет)" % (role, what, stamp))
    print("check_awaits: решений в AWAIT.md %d (закрыто по статусу %d), висит %d"
          % (len(rows), done_rows, len(hanging)))
    if a.strict and (hanging or bad or future):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
