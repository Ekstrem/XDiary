# Патч `do145-readiness-by-ps.patch` — DO-145 (критерий готовности: `ps` вместо пустого `inspect`)

**Автор:** @TL, 2026-11-19 00:27 · **база:** `4bcb3571e`
**Проверено:** `bash -n` → **rc=0** (локально), `git apply --check --reverse` → rc=0.

## Симптом (прогон `44399631494`, head `c1db9d01` — уже с `#2819`, окно 120 с)

```
rootless| xdiary-journal Up 2 minutes ago
root|    xdiary-journal Up 2 minutes ago
inspect| running restarts=            ← значения ПУСТЫЕ
recreate-service: ОШИБКА: xdiary-journal не поднялся
```

Контейнер живёт 2 минуты (окно 120 с уже не при чём), но критерий `[ "$__st" = "0 0" ]` не совпадает, потому что `podman inspect --format '{{.State.RestartCount}} {{.State.ExitCode}}'` в этом контексте отдаёт **пустую строку**.

## Что делает патч

Меняет критерий на статус из `podman ps` (он в логе **работает** — `Up 2 minutes`), с проверкой **обоих** store:

```bash
__st="$(podman ps --format '{{.Names}}	{{.Status}}' | awk -v n="$CONTAINER" -F'	' '$1==n {print $2; exit}')"
[ -z "$__st" ] && sudo -n … (то же самое для root-store)
case "$__st" in Up*) __ok=1; break;; esac
```

Окно `READINESS_TIMEOUT` (120 с) сохранено, HTTP-проверка для шлюза — сохранена.

## Критерий

`deploy-dev` зелёный; `smoke-api` исполняется (не `skipped`) — то есть появляется фактический ответ по `BE-131`.
