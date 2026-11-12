# Патчи @TL: как применять (и почему с `--ignore-whitespace`)

## Главное: команда

```bash
git apply --ignore-whitespace 5-tech-backlog/tl/patches/<имя>.patch
```

**Почему не просто `git apply`.** В репозитории `.gitattributes` (`DO-069`) задаёт `text=auto`, а у ролей разные
`core.autocrlf` (на этой машине — `true`). Из-за этого `.patch` **на диске** получает CRLF, и `git apply`
сравнивает контекст `contents: read\r` с `contents: read` → `error: while searching for:  contents: read?`.
Проверено 2026-11-14: рабочая копия `DO-118.patch` — **20 CRLF, 0 LF**.

С `--ignore-whitespace` те же патчи применяются: **`rc=0`** у `BE-099-v2`, `TE-097`, `DO-117`, `DO-118`, `TE-108`.

**Долгосрочно** правильнее добавить в `.gitattributes` строку `*.patch -text` (или `text eol=lf`) — это зона `@DO`
(инфраструктура корня); записал в `DO-117` как «побочный дефект».

## Реестр патчей

| Патч | Карточка | Что делает | Проверено `apply --check --ignore-whitespace` |
|---|---|---|---|
| `BE-099-v2.patch` | `BE-099` | `ResolveNamesAsync` — без фильтра `DeletedAt`; `GetRecentAsync:95` — фильтр **возвращён**; комментарии в обоих местах | `rc=0`; `dotnet build Journal.sln` → **0 ошибок `CS`** |
| `TE-097.patch` | `TE-097` | `PhoneFormatValidator` (нормализация + E.164 по канону `AR-025`), проверка формата в `ClientHandlers` до лимитера/OTP, 7 тестов | `rc=0`; `dotnet build Client.sln` → **0 ошибок `CS`** |
| `DO-117.patch` | `DO-117` | `lock-sync`: ретрай пишет из **реестра** (`--from-registry --write`), guard `--check-digests` — **внутри цикла** | `rc=0`; `yaml.safe_load` ok |
| `DO-118.patch` | `DO-118` | `sync-board`: `concurrency {group: sync-board, cancel-in-progress: true}` | `rc=0`; `yaml` ok; `check_yaml_dups` `rc=0` |
| `TE-108.patch` | `TE-108` | `front-e2e-repeat`: шаг `check_history_resolvable --selftest` + проба через dev-туннель, коды `0/1/2` разведены | `rc=0`; `yaml` ok; `check_workflow_shape` **41/0** |

## Порядок применения (если делать разом)

1. **`DO-118`** — первым: разгружает очередь CI (иначе остальные прогоны ждут).
2. **`BE-099-v2`** — затем квитанция `test-partner-softdelete` (4/4) → `TE-050`.
3. **`TE-097`** — тесты формата телефона.
4. **`TE-108`** — гейт истории в CI (после `BE-099-v2`, чтобы следующий прогон уже проверял починенное).
5. **`DO-117`** — digest в lock (можно независимо).

## Снято

`BE-099.patch` (первая версия, 2026-11-13) **удалён** 2026-11-14: он снимал фильтр в `ResolveNamesAsync`
**без** возврата в `GetRecentAsync`, то есть содержал ту же ошибку, что и коммит `5f135be22`. Оставлен только `BE-099-v2`.
