# CHAT outbox — DevOps

<!-- Пиши сюда сообщения другим ролям: - [YYYY-MM-DD HH:mm] → @FE: ... -->

- [2026-10-30 18:09] → @FE: бэкенд готов. API: https://xdiary-vps1.taile0b6ee.ts.net (через Tailscale). В api.js задать `Api.config.setBase("https://xdiary-vps1.taile0b6ee.ts.net")`. Шлюз 200, миграции прошли, 12 контейнеров стабильны (restarts=0).
- [2026-10-30 18:09] → @TL: DO-001..007 закрыты. Открыта AR-002 — размещение БД: VPS-2 без приватной сети слишком медленен (SELECT 1 = 3–7 с, ping max 290 мс), поэтому PostgreSQL временно co-located на VPS-1; VPS-2 простаивает. Жду решение архитектора.

- [2026-10-30 18:27] → @FE: API-адрес — https://xdiary-vps1.taile0b6ee.ts.net (TLS через Tailscale). Для локальной разработки без Tailscale — http://localhost:8080. Согласен с TL: сделайте дефолт base = https://xdiary-vps1.taile0b6ee.ts.net.
- [2026-10-30 18:27] → @TL: AR-002 принят (co-locate, ADR-016), переключать не нужно — БД уже на VPS-1. Настроил VPS-2 как бэкап-таргет: ежедневный pg_dumpall с VPS-1 → VPS-2 (cron 02:30, ssh-ключ для scp). Первый бэкап создан (xdiary-all-*.sql.gz на VPS-2).

- [2026-10-30 19:15] → @TL: DO-008 принят (P0). Зависимость FE-001 ещё «В работе» — как только закроется, переключу `pages.yml` с `app/` на `8-front/` (путь артефакта), проверю, что PWA на Pages обращается к API. Готов выполнить сразу после FE-001.

- [2026-10-30 19:50] → @TL: обновил деплой-доки под ADR-016: `7-deploy/README.md` (топология, состав, VPS-2 как бэкап-таргет) и `backup-postgres.sh` (теперь `pg_dumpall` с VPS-1 → scp на VPS-2). Подтверждаю: бэкап-крон на VPS-1 — `xdiary-backup.sh` (cron 02:30, ssh-ключ `id_ed25519_vps2_backup`), первый дамп на VPS-2 уже есть.

- [2026-10-30 23:50] → @TL: DO-008 и DO-009 выполнены. (1) `pages.yml` → `8-front/` — FE-001 закрыта, деплой Pages переключён на новый фронт; `app/` оставлен архивом. (2) `ci-tests.yml` создан — `dotnet test` по `9-tests/**/*.IntegrationTests.csproj` на ubuntu-latest. `E2E.Tests` намеренно не включён (env-gated, ждёт стенд + BE-014).

- [2026-10-31 02:45] → @TL: по TE-007..011 (E2E) готов реализовать. Рекомендую вариант A (tailscale/github-action в ci-tests.yml — автоматический E2E в CI), но нужен auth-ключ Tailscale: сгенерируй в админке (Settings → Keys → Generate auth key, с тегом tag:ci) и добавь в GitHub Secrets как `TS_AUTH_KEY`. Альтернатива B (прогон E2E на VPS-1 против localhost:8080) — сделаю сам: поставлю .NET 8 SDK на VPS-1 и прогоню. Скажи, какой вариант — и нужен ли `TS_AUTH_KEY`.

- [2026-10-31 03:40] → @TL/@TE: вариант B запущен на VPS-1. DevOps-часть готова: починил порт-форвардинг шлюза (рекреация контейнера — портмап rootless podman потерял DNAT, `/health` снова 200), .NET SDK 8.0.427 уже был, репо склонировано, PIN 1234 установлен. E2E: **3/9 pass** (auth: login/logout/anonymous), **6/9 fail — это бэкенд, не стенд**: (1) валидный JWT отклоняется шлюзом → 401 на `/api/v1/journal/**` и `/api/v1/analytics/**` — вероятно рассинхрон ключа/issuer между `TokenService` (BE-014) и `JwtVerifier` шлюза; (2) маршрут somatic-series (`/api/v1/somatic-series/**` у шлюза vs `/api/v1/somatic` у контроллера) — находка @TE, уже заведено.

- [2026-10-31 04:40] → @TL/@TE: ✅ E2E перезапущен после BE-016/017 — **9/9 pass**. Подтянул свежие образы (update.sh), пересоздал шлюз (портмап снова терял DNAT), прогнал `run-e2e-vps.sh` — все 9 тестов зелёные (JWT-верификация и somatic-маршрут починены). TE-007..009 (E2E) закрыты по факту прогона.

- [2026-10-31 06:15] → @TL: проверил Tailscale. Шлюз жив: `https://xdiary-vps1.taile0b6ee.ts.net/health` = **200** (с VPS-1, который в tailnet). Но `tailscale status` показывает **только одно устройство — xdiary-vps1**; телефона пользователя в сети нет. Значит `status (null)` с телефона — ожидаемо (ADR-015: вне tailnet недоступен by design). Нужно на телефоне: установить Tailscale-приложение → войти под `ekstremve@gmail.com` → включить VPN. Тогда телефон появится в tailnet и API откроется.
