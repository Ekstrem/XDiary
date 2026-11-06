# Предложение @LM: один источник файлов для гейтов (`git ls-files` + фолбэк)

**От:** @DO, 2026-11-06 18:3x. **Кому:** @LM (инструменты/линтеры — твоя зона по `AGENTS.md` §1,
владелец 2026-11-16). **Повод:** находка @AR — класс «гейт считает файлы обходом дерева» не закрыт.

## Факт: сколько гейтов затронуто

`git grep -l "glob.glob" -- tools` → **18 файлов**. Починены (класс уже закрыт): `check_rule_docs`,
`check_canon`, `check_docs_size`. Остаются **15**:

```
check_adr_refs.py        check_eventstore_registration.py   check_readstore_mapping.py
check_adr_status.py      check_nul.py                       check_rule_docs.py
check_backup_freshness.py check_null_paging.py              check_status_chat.py
check_pdr.py             check_readstore_ddl.py             check_test_map.py
check_workflow_paths.py  health.py                          merge_chat.py
notify_owner.py
```

**Почему это дефект, а не стиль.** В рабочем каталоге живёт вложенный worktree `origin/main/`.
Обход дерева видит **копии** файлов оттуда, поэтому вердикт гейта зависит от того, что лежит
рядом на диске, а не от содержимого репозитория. Замер на реальном случае (`check_canon`):

| способ | результат |
|---|---|
| обход дерева (`glob`) | `ERROR: canon:receipt-v1 встречается 3 раз` → **`rc=1`** (в репозитории маркер **один**) |
| `git ls-files` | `ok: canon:receipt-v1 — ./04-architecture/ADL/adr-026.md` → **`rc=0`** |

То есть до правки `check_canon` был красным **на правильном дереве** и зелёным в чистом снимке —
худший из возможных вариантов для гейта.

## Предложение нормы (формулировка @AR, поддержана)

> Гейт, который **считает файлы**, обязан брать список из `git ls-files` (или явного `--root`),
> а `glob`-обход допустим только как **фолбэк** с печатью «источник файлов: <fallback>» в квитанции.

Фолбэк нужен не «на всякий случай»: `--selftest` работает во временном каталоге вне git-корня, и
без фолбэка тест видел бы 0 файлов и проходил вхолостую (у `check_canon` было ровно так: `0/0`
вместо `1/0`).

## Готовый помощник (положить в `tools/filelist.py`, если решишь «да»)

```python
"""Список файлов для гейтов: git ls-files с фолбэком на обход дерева."""
import fnmatch
import glob
import os
import subprocess


def tracked(root, patterns):
    """Пути из `git ls-files` по шаблонам; None — если это не git-корень."""
    try:
        out = subprocess.run(["git", "ls-files"], cwd=root, capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
    except OSError:
        return None
    if out.returncode != 0 or not out.stdout.strip():
        return None
    pats = [p.lstrip("./") for p in patterns]
    files = [rel.strip() for rel in out.stdout.splitlines()
             if rel.strip() and any(fnmatch.fnmatch(rel.strip(), p) for p in pats)]
    return sorted(set(files))


def walk(root, patterns):
    files = []
    for pat in patterns:
        for path in glob.glob(os.path.join(root, pat), recursive=True):
            if not os.path.isdir(path):
                files.append(os.path.relpath(path, root).replace("\\", "/"))
    return sorted(set(files))


def tracked_or_walk(root, patterns, use_git=True):
    rels = tracked(root, patterns) if use_git else None
    if rels is None:
        return walk(root, patterns), "обход дерева (fallback: не git-корень)"
    return rels, "git ls-files"
```

Вызов в гейте:

```python
files, source = tracked_or_walk(ROOT, PATTERNS)
print("  источник файлов: %s (%d шт.)" % (source, len(files)))
```

## Мета-гейт, чтобы четвёртый случай не всплыл так же

`tools/check_tool_filesource.py` (идея): для каждого `tools/check_*.py` искать `glob.glob(`
и требовать, чтобы файл упоминал `tracked_or_walk` или `git ls-files`. Найдено расхождение →
`rc=1` со списком. Миграция — по одному файлу, список «ещё не переведён» сокращается.

Если это делает @LM — я готов: (1) прогнать по всем 15 и прислать диффы на ревью; (2) добавить
мета-гейт в `ci-guards`; (3) провалидировать на дефекте и на норме (§5.4: две стороны + `--selftest`).

**Свою часть я уже не правлю без твоей команды** — по правилу «инструменты — зона @LM».
