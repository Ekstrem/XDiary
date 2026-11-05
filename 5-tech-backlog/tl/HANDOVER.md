# HANDOVER — что осталось до закрытия линии (@TL, 2026-11-05)

Документ для владельца: здесь **только два действия**, которые невозможно выполнить из роли
координатора (код и выкатки — зоны @BE/@DO), и всё, что они закрывают. После них известных
дефектов по линии не остаётся.

## 1. @BE — DDL read-store (`BE-068`)

**Почему стоит:** `ListAll` не работал никогда ни на одном контуре: read-таблиц нет ни в одной
миграции, а библиотека `AddReadStorePostgres` схему не создаёт (проверено по исходникам публичной
`Ekstrem/DigiTFactory.Libraries`: только DI + `DbContext`, ни `EnsureCreated`, ни DDL).

**Что сделать:** вставить готовый фрагмент из `5-tech-backlog/back-end/BE-068.md`
(раздел «Готовый фрагмент для вставки») в **два** места — `Journal.Api/Program.cs` и
`Partners.Api/Program.cs`, рядом с существующим `Migrate()`.

**Как проверить, что доехало** (обязательно `origin`, а не своё дерево):

```
git add 6-msa/BC-Journal/src/Journal.Api/Program.cs 6-msa/BC-Partners/src/Partners.Api/Program.cs
git commit -m "be: BE-068 — идемпотентный DDL read-store"
git push
git show origin/main:6-msa/BC-Journal/src/Journal.Api/Program.cs | grep -F "CREATE TABLE IF NOT EXISTS"
```

Два предыдущих отчёта (`0b076b81`, `6f9227b6`) содержали **только `chat/be.md`** — работа была
потеряна вместе с временным worktree. Поэтому порядок: **пуш → проверка `origin` → строка в чат.**

**Критерий приёмки** (@AR): чистая БД → старт → `GET /internal/journal/records` = **200**.

## 2. @DO — выкатка `partners` (образ готов)

**Почему стоит:** один образ закрывает **четыре** карточки (`BE-069`, `TE-050`, `FE-041`, `DO-079`).

Образ уже собран: `ci-partners` **#54 success** на `26a2937a` (в нём `BE-069`: `Paging` вместо
`null!` в `PartnerReadRepository.cs:16`).

```
recreate-from-env.sh partners          # или штатный update.sh
curl -isS http://xdiary-partners:8080/api/v1/partners -H "Authorization: Bearer <токен>"   # ожидание: 200
# повтор прогона контракта:
#   GitHub → Actions → test-partner-softdelete.yml → Run workflow  (ожидание: 4 passed)
python tools/gen_images_lock.py --write && python tools/gen_images_lock.py --check           # rc=0
```

**Критерий:** `GET /partners` изнутри → **200** (сейчас `500` `NullReferenceException`);
`test-partner-softdelete` → **`4 passed`** (сейчас `4 failed`).

## 3. Что уже закрыто и проверено (менять не нужно)

| Проверено | Чем |
|---|---|
| APIM в пути **для интернета** | `catalog` 401 + `X-Gravitee-*`, несуществующий путь 404, `/health` 200 (мимо APIM) |
| `(ii-a)` 74/74 | `via=gravitee`, `rc=0`, прогон из интернета |
| `(ii-b)` 74/74, `(ii-c)` 2/2 | прямые вызовы и `/internal/**` |
| `gen_images_lock --check` | `rc=0`, `journal 1d01433`, `partners 68644da`, `front 5649cf3` |
| Гейты | `public-probe`, `smoke-apim`, `status-lint`, `stats`, `keepalive`, `null_paging` — зелёные |
| Ревью | можно закрывать: `DO-069`, `DO-072`, `DO-073`, `BE-035`, `FE-040`; нельзя: `DO-064`, `DO-065`, `DO-071`, `DO-051` |

## 4. Что ждёт решения владельца (не техническое)

- `DO-077` — APIM на тестовом контуре: (A) только прод + строка в ранбуке, (B) симметрия (нужен браузер к `127.0.0.1:18084` и ≥1.3 ГБ RAM);
- `FE-041`/`FE-042` — прогон на телефоне (фронт `0.94` на проде);
- `DO-081` — источник фантомных уведомлений (28 за сессию, метки которого нет в `origin`).
