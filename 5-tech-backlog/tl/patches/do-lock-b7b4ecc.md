# Патч `do-lock-b7b4ecc.patch` — lock на образ с фиксом scope (`#2867`)

**Автор:** @TL, 2026-11-19 03:31 · **база:** `b7b4ecc71`
**Содержимое:** `7-deploy/images.lock` — все 12 сервисов на `single-b7b4ecc` (digest-ы из `lock-sync/97dcae767`, PR `#2868`).

## Зачем

`#2867` (`IServiceScopeFactory` для `ClientEventHandler`) в `main`; `Release` успешен; образ `single-b7b4ecc` **опубликован**. Но `images.lock` указывает на `single-06d26ff` — образ **без фикса**, где `client` падает в рестарт-цикл (`restarts=14`, `DependencyResolutionException`), а `POST /clients` = 502.

## Почему не PR бота

`#2868` создан через `GITHUB_TOKEN` ⇒ workflows не запускаются ⇒ required-чеки не появляются ⇒ `mergeState=BLOCKED` даже с `--admin`.

## Как применить (одна команда)

```bash
git checkout -b role/do-lock5 origin/main
git apply 5-tech-backlog/tl/patches/do-lock-b7b4ecc.patch
git add 7-deploy/images.lock
git commit -m "do: DO-145 — lock на single-b7b4ecc (IServiceScopeFactory, #2867)"
git push origin role/do-lock5
gh pr create --base main --head role/do-lock5 --title "do: lock на single-b7b4ecc" --body "lock на образ с #2867; bot-PR #2868 не мержится (GITHUB_TOKEN)"
gh pr merge <N> --merge
```

## Критерии проверки после деплоя

1. `deploy-dev` зелёный (с `#2865` рестарт-цикл не пропускается);
2. `podman logs xdiary-client --tail 40` без `DependencyResolutionException`;
3. `RestartCount` не растёт;
4. `select count(*) from client_read` > 0 после регистрации;
5. `POST → verify → GET /clients/me` = **200**.
