# Dev-стенд (VPS-2): состав, проверка, правила уборки

Ранбук «как», решения — в `ADR-*`. Проверено 2026-11-05 после инцидента: `podman ps -q | xargs podman kill`
уронил **все** контейнеры стенда (источник: @TL 22:31, восстановлено `podman start`).

## Состав (13 контейнеров `xdiary-*`)

`pg`, `partners`, `journal`, `privacy`, `client`, `catalog`, `portability`,
`daily-metrics`, `somatic-series`, `analytics`, `garmin-gateway`, `apple-health-gateway`,
`api-gateway`.

Плюс **временные** контейнеры прогонов (например, Playwright:
образ `mcr.microsoft.com/playwright:*`, команда `npx playwright test`) — они **не** часть стенда.

## Проверка «стенд живой» (три уровня, а не один)

```bash
# 1) все контейнеры запущены
sudo -n podman ps -q | wc -l          # ожидаем 13 (+ временные прогонов)

# 2) образы — те, что запинены (перезапуск НЕ меняет образ, но и не чинит пин)
for s in partners journal; do sudo -n podman inspect xdiary-$s \
  --format "$s {{.RestartCount}} {{.State.Status}}"; done

# 3) health изнутри сети (снаружи вход отдаёт 401 — это норма, а не «сервис мёртв»)
for s in partners journal privacy client catalog portability daily-metrics \
         somatic-series garmin-gateway apple-health-gateway api-gateway; do
  sudo -n podman run --rm --network xdiary docker.io/curlimages/curl:8.10.1 \
    -s -o /dev/null -w "$s %{http_code}\n" "http://xdiary-$s:8080/health"
done
```

**Ожидаемый результат 2026-11-05:** 11 сервисов → **200**, `analytics` → **401**.

> `analytics /health` отвечает `401` с `WWW-Authenticate: Bearer` — health-эндпоинт за auth.
> Это **аномалия** (у остальных `/health` открыт) и ломает любой health-гейт; заведено как дефект
> для @BE. В проверке стенда считать `analytics=401` известным, а не «стенд упал».

## Правило уборки: только свои артефакты

**Запрещено:** `podman ps -q | xargs podman kill`, `podman kill $(podman ps -q)`, `podman ps -aq | xargs podman rm -f`.
Эти команды **не спрашивают**, что было запущено до вас: 2026-11-05 такая строка убила 13 контейнеров
стенда вместе с базой.

Разрешено:

```bash
sudo -n podman kill <точное-имя>          # только свой контейнер
sudo -n podman rm -f <точное-имя>         # только свой контейнер
sudo -n podman run --rm ...                # артефакт убирается сам
sudo -n rm -rf /tmp/test-results           # чистка каталогов — по путям, не «всё»
```

**Перед удалением незнакомого контейнера — посмотреть, что это:**

```bash
sudo -n podman inspect <имя> --format '{{.ImageName}} | {{.Created}} | {{.Config.Cmd}}'
```

Пример из практики: контейнер `magical_jang` (случайное имя) оказался **идущим прогоном
Playwright** (`mcr.microsoft.com/playwright:v1.64.1-noble`, `npx playwright test`). Удаление
«мусора» прервало бы активный прогон — поэтому сначала `inspect`, потом решение.

## Перезапуск без смены образов

`podman start <имена>` поднимает контейнеры **на тех же образах** — то есть пин сохраняется.
Но он **не** пересоздаёт контейнеры с новым `deploy.sh` и не проверяет, что сервис здоров:
после `start` обязательны три уровня проверки выше.

## Связанное: работа с общим деревом и очередью

Правило «пуш не прошёл — рутина, не инцидент; интеграция только `merge`» живёт в
[`docs/GIT-CONCURRENCY.md`](../../docs/GIT-CONCURRENCY.md) (положен @LM в этот репозиторий,
2026-11-06). Здесь — только то, что специфично для стенда:

- маркеры конфликта в `*.md` → `python tools/check_nul.py` даёт **`rc=1`** (по `AGENTS.md` §5 это
  ошибка, а не предупреждение): пока они в `origin`, красен любой прогон, который зовёт гейт;
- если видишь `<<<<<<<` в своём файле — сначала убедись, что строка, которая лежит между маркерами,
  есть в `origin` (иначе она потеряется при разрешении «в сторону origin»), и только потом удаляй маркеры;
- `.git` при незавершённом merge трогает координатор (§5.3), остальные работают в своих worktree.
