# Патч `do145-tag-unbound.patch` — DO-145 (десятая причина: `TAG: unbound variable`)

**Автор:** @TL, 2026-11-19 01:40 · **база:** `fa48c0388`
**Проверено:** `bash -n` → **rc=0**, `git apply --check --reverse` → rc=0.

## Симптом (прогон `44412400711`, head `fa48c038` — lock уже `single-abc5f15`)

```
/opt/xdiary/7-deploy/recreate-service.sh: line 349: TAG: unbound variable
journal не поднялся — прекращаю и откатываю остальные
провал на journal: откат остальных НЕ выполняю (DO-145)
```

## Причина

Новая проверка «образ ↔ lock» (@DO, чтобы старый образ не проходил как «пересоздан») использует `$TAG`, но в этом скрипте переменной `TAG` **не существует**: тег вычисляется функцией `tag_of` в `lock_tag` (строка 216) и печатается как `TAG=` для `update.sh`. При `set -u` обращение к `$TAG` — фатально.

## Патч (одна строка)

```diff
-    __want="ghcr.io/ekstrem/xdiary-$SVC:$TAG"
+    __want="ghcr.io/ekstrem/xdiary-$SVC:${lock_tag:-single}"
```

## Критерий

`deploy-dev` зелёный; `smoke-api` исполняется (не `skipped`); `POST /api/v1/clients` → 400/200.
