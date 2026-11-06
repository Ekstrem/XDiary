# ESCALATION — три сообщения, которые снимают блокировку (копировать как есть)

Состояние на 2026-11-06 13:1x, `origin = 29dc30ee`. Продукт стоит на трёх правках.
Ниже — готовые тексты: их можно передать ролям **дословно**, без пересказа.

---

## 1. @BE — сборка (2 строки в одном файле)

> **`BE-089`, P0, первым делом.** В `6-msa/BC-Partners/src/Partners.Api/Events/OutboxDispatcher.cs`:
> удалить строку `using Partners.Storage.Outbox;` и добавить `using XDiary.Contracts;`
> (namespace `Partners.Storage.Outbox` больше не существует, а конвертер переехал в `XDiary.Contracts`).
> Затем — карточка `BE-090` (P0): `POST /api/v1/clients` возвращает `otpCode` в ответе, из-за чего
> по одному номеру телефона можно войти в чужой дневник. Правка: в `ClientsController.Register` вернуть
> явную проекцию `new { id, version, phone, otpExpiresAt, verified }` вместо `Ok(r.Model())`.
> Всё остальное (тесты изоляции, `BE-077`) — после того, как сборка зелёная и образы публикуются.

**Как проверить, что сделано:** `git grep "using Partners.Storage.Outbox"` → пусто;
`ci-partners` → шаг `Build` = success, job `docker` = выполняется.

---

## 2. @DO — CORS на проде (вход владельца)

> **`DO-106`, P0: вход в приложение с телефона невозможен.** `OPTIONS /api/v1/clients` с `Origin: https://ekstrem.github.io`
> возвращает **401** и не отдаёт `Access-Control-Allow-Origin`, поэтому браузер режет ответ (в приложении — «Нет связи
> с сервером», хотя `POST` отвечает 200). Патч `Caddyfile.prod` готов в карточке: в **оба** блока (`443` и `http://:8081`)
> после `log` вставить `@preflight method OPTIONS` + `handle @preflight { header ACAO/ACAM/ACAH; respond "" 204 }`
> и `header Access-Control-Allow-Origin "https://ekstrem.github.io"`. После reload — `python 5-tech-backlog/tl/verify_mvp.py`
> должен дать 10/10 (сейчас 7/10, красные три — CORS).

**Как проверить:** `curl -X OPTIONS` с `Origin` → `204/200` и `Access-Control-Allow-Origin`.

---

## 3. @EX (владелец) — канал OTP (решение, не код)

> Нужно решение: какой канал доставки OTP на проде. Сейчас `Otp__Channel=telegram`, но Telegram с прода
> **недостижим**, а лог печатает «Telegram OTP отправлен» (ложный успех). Варианты:
> **(а)** `sms` — нужен провайдер и креды; **(б)** `telegram` + сетевой путь (прокси/exit-node);
> **(в)** временно `log` — код будет в логах контейнера; **(г)** закрыть регистрацию на проде до решения.
>
> Пока канал не работает (или пока `otpCode` утекает в ответе — `BE-090`), вход на прод невозможен даже после CORS.

---

## Что будет после (проверяю я, отчитываюсь числами)

| Шаг | Проверка | Ожидание |
|---|---|---|
| `BE-089` | `ci-partners`/`ci-journal` Build | success |
| образы | job `docker` | выполняется (не `skipped`) |
| `BE-090` | `POST /api/v1/clients` | в теле **нет** `otpCode` |
| `DO-106` | `verify_mvp.py` | **10/10** |
| выкатка | `deploy-prod` | выполняется (не `skipped`) |

## Почему я не могу сделать это сам

`python tools/check_paths.py --zones`: зона **TechLead** — только `5-tech-backlog/*`, `README.md`, `CHANGELOG.md`,
`.gitignore`, `loop.config.json`, `HEALTH.md`. Правка в `6-msa/**` или `7-deploy/**` **покраснит** job
`Guard (роль не вышла за зону)`. Разрешение владельца этого не отменяет — нужна правка самих правил.
