# Готовность к тестированию: что доказано и чем

Состояние на 2026-11-13 03:44 (@TL). Каждая строка — либо **прогон**, либо **команда**,
либо явно «не проверено». Обещаний безEvidence здесь нет.

## 1. Деплой (`main` → dev → prod)

| Утверждение | Чем доказано |
|---|---|
| гонка `receipt-be092` × очистка стенда устранена | `deploy.yml:306` `needs: [deploy-dev, smoke-front]` (`DO-121`); `role/do` |
| YAML-баг `if-no-files-found` в `run:` убран | `deploy.yml:428` — только артефакт-конфиг (`DO-120`) |
| `check_otp_leak` больше не сирота | шаг после «Туннель к dev», `--selftest` 14/14 (@DO) |
| `deploy.yml` структурно валиден | `yaml.safe_load` → ok, **8 jobs**; `check_workflow_shape` → rc=0 (43 workflow) |
| `deploy-prod` выполнится | **не проверено** — требует мержа в `main` и реального прогона |

## 2. E2E-харнесс (`FE-046`)

| Утверждение | Чем доказано |
|---|---|
| причина — отсутствие `actionTimeout` + безтаймаутные чтения `#toast` | трасса прогона `43322907200`: `Test timeout of 90000ms exceeded`, разрыв `5.6 с → 95.5 с` |
| фикс работает | прогон `43324408782` (`verify/fe046`, патч) → **success**; `43330703353` (`role/te-toast`) → **success** |
| оба уровня таймаута измерены | `43328851817`: `probe-class 15000ms / dur=15s`, `probe-instance 1500ms / dur=1.5s` |
| класс закрыт 4 из 4 | @AR нашёл 4-е чтение (`stepText`), @TE закрыл в `5dd0efe0c` |
| карточка закрыта | `FE-046` = `Закрыта` в `role/frontend-close2` (#2177); в `main` — `На проверке` до мержа |

## 3. Транспорт PR (`DO-119`)

| Утверждение | Чем доказано |
|---|---|
| `PROJECT_PAT` не годится | лог: `createPullRequest ... requires ['public_repo']` |
| фикс токена готов | `role/do` `pr-create.yml`: `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` + `paths: ['**']` |
| **PR от `GITHUB_TOKEN` не запускает чеки** | @AR 03:40 (анти-рекурсия GitHub); в `role/do` **нет** `actions: write` и dispatch |
| фикс второй части | `pr-create-dispatch-checks.patch` (`actions: write` + dispatch `ci-guards`/`ci-backend`); `yaml` ok, `check_workflow_shape` rc=0 |
| транспорт заработал целиком | **не проверено** — нужен мерж и первый авто-PR с чеками |

## 4. P0/P1

| Карточка | Состояние | Evidence |
|---|---|---|
| `TL-002` | **Закрыта** в `role/tl` | `check_docs_size` rc=0, `AGENTS.md` 33 404 → 25 966 |
| `DO-099` | 8 гейтов + `check_otp_leak` | `test-gates` → **success** (`43336316072`) |
| `DO-103` (P0) | фикс в `role/do` | `changes` job + `needs` + `if` |
| `TE-097` | входы согласованы | канон `AR-025` п.2a ↔ `PhoneFormatValidator` ↔ `[InlineData("+7900000000")]`; нужен порядок `#2167` ⩽ `#2169` |
| `BE-104` (новый P1) | фикс проверен | `CS7036`; с патчем сборка **31/31 PASS** (`43336316072`) |
| `BE-071`, `BE-074`, `DO-085` | **замерены как долг, не блокеры** | бэкфилла нет; `ApiGateway` жив; `:8080` — 36 вхождений |
| `DO-082` | вердикт @AR «можно закрывать» | `GET /api/v1/clients` → 405, выдуманный путь → 404 |

## 5. WIP-лимит и транспорт коммитов

| Утверждение | Чем доказано |
|---|---|
| ≤5 в «На проверке» | `review_whip.py` → rc=0 |
| только `role/tl` + PR, без пушей в `main` | 0 пушей в `main` с 00:16; PR **#2174** |

## Чего не хватает (всё — действия людей/владельцев зон)

1. **мерж `#2165`** — вносит PyYAML, `paths`, `github.token`; дальше авто-PR и остальные мержи;
2. **патчи в чужие зоны:** `be104-portability-clientid` (@BE), `test-gates-pyyaml` + `check-chat-pyyaml-order` + `pr-create-dispatch-checks` (@DO);
3. **арбитраж `check_status_chat`** и решение по паузе бот-коммитов (@LM);
4. **первый реальный `deploy`** после мержа — единственное, что осталось **не проверенным** в п.1.
