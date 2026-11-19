# Патч `do-lock-6034c74.patch` — lock на образ с `MigrationsAssembly` (@BE `#2849`)

**Автор:** @TL, 2026-11-19 02:24 (по находке @FE) · **база:** `6095db12f`
**Содержимое:** `7-deploy/images.lock`, 12 строк — `client`/`journal`/… → `single-6034c74` (`commit=6034c74`), digest-ы из `lock-sync/87f01944c`.

## Зачем

`#2849` (`AddDbContext<ReadDbContext>(… MigrationsAssembly("Client.Storage"))`) в `main` с 02:03, образ `single-6034c74` **опубликован** (GHCR, 23:06), но `images.lock` указывает на `single-abc5f15` (`commit=abc5f15`) — **до фикса**. Поэтому `client_read` не создаётся, `POST /clients` = 500, `42P01` в логах.

## Почему патч, а не PR бота

`#2856` (bot, `lock-sync/87f01944c`) имеет `mergeState=BLOCKED` и **0 чеков**: PR, созданный через `GITHUB_TOKEN`, не запускает workflows, required-чеки не появляются ⇒ мерж невозможен даже с `--admin`. Внести может только роль с зоной `7-deploy/**`.

## Как применить (одна команда)

```bash
git checkout -b role/do-lock-6034c74 origin/main
git apply 5-tech-backlog/tl/patches/do-lock-6034c74.patch
git add 7-deploy/images.lock
git commit -m "do: DO-145 — lock на single-6034c74 (MigrationsAssembly для ReadDbContext)"
git push origin role/do-lock-6034c74
gh pr create --base main --head role/do-lock-6034c74 --title "do: lock на single-6034c74" --body "lock на образ с #2849; bot-PR #2856 не мержится (GITHUB_TOKEN)"
gh pr merge <N> --merge
```

## Критерий

`deploy-dev` зелёный; в логе `xdiary-client` — `[migrate] read migrations=[1+]`; `POST /api/v1/clients` → **400/200**; `\dt client_read` в базе `client` даёт таблицу.
