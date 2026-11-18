# Ранбук: восстановление чата после усыхания (инцидент 2026-11-18)

## Что случилось

Бот-транспорт **трижды** заменил файлы `5-tech-backlog/chat/*.md` устаревшим меньшим снимком:

| время | коммит | сумма сообщений |
|---|---|---|
10:21:52 | `266a15005` | **2 653** (последний целый снимок) |
10:23:15 | `9059ae937` | 939 |
10:35:43 | `b9edce6d2` (восстановление @TL) | 2 661 |
10:42:05 | `ac3ec9397` | 940 |
10:50:53 | `3e05c57a2` (восстановление @TL) | 2 661 |
10:58:04 | `4f83e1ce9` | 941 |

Потери по файлам на первом усыхании: `tl` −798, `te` −447, `ar` −241, `do` −223, `be` −4, `lm` −1.

## Предусловие (иначе бессмысленно)

**Бот должен быть остановлен** (зона @LM). Пока он активен, восстановление живёт 4–7 минут: он берёт **свой устаревший снимок**, а не общее дерево.

## Восстановление (union — ничего не теряется)

База — `266a15005`; к ней добавляются сообщения, появившиеся в `main` после него (их немного), с дедупликацией по полной строке:

```bash
# для каждого файла: база из 266a15005, затем дописать строки-сообщения, которых в ней нет
git show 266a15005:5-tech-backlog/chat/tl.md > /tmp/base_tl.md      # ВНИМАНИЕ: в PowerShell '>' даёт UTF-16!
```

**Правильный способ без перенаправления** (байты как есть): взять версию через `git checkout` в отдельный worktree, либо скриптом на Python (см. ниже). `>` в PowerShell писать **нельзя** — получится UTF-16 (`0xff`), `chat_lint` даст `rc=2 «НЕ ИЗМЕРЕНО»` (на этом обожглись @LM с `be.md` и @TE с `te.md`).

Алгоритм (Python, безопасно):

```python
import subprocess, io
BASE = '266a15005'
for f in ('tl','te','ar','do','be','lm','fe','dr','po','ex'):
    path = '5-tech-backlog/chat/%s.md' % f
    base = subprocess.run(['git','show','%s:%s' % (BASE, path)], capture_output=True).stdout.decode('utf-8','replace')
    cur  = subprocess.run(['git','show','origin/main:%s' % path], capture_output=True).stdout.decode('utf-8','replace')
    bl = base.splitlines(); cl = cur.splitlines()
    bm = [l for l in bl if l.strip().startswith('- [')]
    cm = [l for l in cl if l.strip().startswith('- [')]
    seen = {l.strip() for l in bm}
    extra = [l for l in cm if l.strip() not in seen]
    first = next((i for i,l in enumerate(bl) if l.strip().startswith('- [')), len(bl))
    io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(bl[:first] + bm + extra) + '\n')
```

## Проверка после восстановления

```bash
python tools/chat_lint.py --check --strict-times --dups   # ожидаем rc=0, «точных дублей: 0»
python tools/check_nul.py                                  # ожидаем rc=0
python tools/check_chat_shrink.py                          # ожидаем «усыханий 0»
# сумма сообщений по всем файлам — ожидаем ≥ 2 661
```

## Что должен сделать @LM, чтобы это не повторилось

1. **остановить/починить бота** — источник должен быть общий, а не его снимок;
2. жёсткий инвариант: `if new_lines < existing_lines: abort` (не warning);
3. `check_chat_shrink` на **коммитах бота** (`HEAD~1..HEAD`) с игнором `[skip ci]` — сейчас он молчит при потере 1 700 сообщений;
4. канарейка: падение суммы по всем файлам больше N ⇒ падать;
5. запрет `>` в файлы чата/задач в правилах (три случая UTF-16 за сутки).
