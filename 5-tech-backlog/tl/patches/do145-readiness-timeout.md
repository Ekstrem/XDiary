# Патч `do145-readiness-timeout.patch` — DO-145 (окно readiness 30 с короче старта journal)

**Автор:** @TL (по находке @FE), 2026-11-19 00:15 · **база:** `4fbbe3da6`
**Передано:** `→ @DO`. Проверено: **`bash -n` → rc=0** (локально, `C:\Program Files\Git\bin\bash.exe`), `git apply --check --reverse` → rc=0.

## Симптом (прогон `44397887807`, head `f56c876f8`)

```
21:07:16  пересоздаю journal
21:07:51  диагностика: rootless| xdiary-journal Up 30 seconds ago
                      root|    xdiary-journal Up 30 seconds ago
21:07:52  recreate-service: ОШИБКА: xdiary-journal не поднялся → exit 1
```

**Цифры @FE:** от «пересоздаю» до «Up 30 seconds» — ~35 с, а окно `__deadline = now + 30` ⇒ цикл вышел за секунды до готовности, и диагностика через секунду показала «Up».

## Вторая проблема, которую он заметил

В диагностике повторно запускается `bash "$RUNNER"`, и в лог попадает `run| Error: … container name "xdiary-journal" is already in use …` — это **следствие** нашей же проверки, но читается как причина. Отсюда моя ложная версия «dual-store».

## Что делает патч (2 строки)

1. `__deadline = now + ${READINESS_TIMEOUT:-120}` — окно 120 с по умолчанию (настраивается, чтобы шлюз не ждал зря);
2. в диагностике **не перезапускать** `bash "$RUNNER"` — оставить только состояние контейнера.

## Критерий

`deploy-dev` зелёный; `smoke-api`/`smoke-front` исполняются (не `skipped`).
