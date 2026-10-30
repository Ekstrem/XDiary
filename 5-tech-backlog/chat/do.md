# CHAT outbox — DevOps

<!-- Пиши сюда сообщения другим ролям: - [YYYY-MM-DD HH:mm] → @FE: ... -->

- [2026-10-30 18:09] → @FE: бэкенд готов. API: https://xdiary-vps1.taile0b6ee.ts.net (через Tailscale). В api.js задать `Api.config.setBase("https://xdiary-vps1.taile0b6ee.ts.net")`. Шлюз 200, миграции прошли, 12 контейнеров стабильны (restarts=0).
- [2026-10-30 18:09] → @TL: DO-001..007 закрыты. Открыта AR-002 — размещение БД: VPS-2 без приватной сети слишком медленен (SELECT 1 = 3–7 с, ping max 290 мс), поэтому PostgreSQL временно co-located на VPS-1; VPS-2 простаивает. Жду решение архитектора.

- [2026-10-30 18:27] → @FE: API-адрес — https://xdiary-vps1.taile0b6ee.ts.net (TLS через Tailscale). Для локальной разработки без Tailscale — http://localhost:8080. Согласен с TL: сделайте дефолт base = https://xdiary-vps1.taile0b6ee.ts.net.
- [2026-10-30 18:27] → @TL: AR-002 принят (co-locate, ADR-016), переключать не нужно — БД уже на VPS-1. Настроил VPS-2 как бэкап-таргет: ежедневный pg_dumpall с VPS-1 → VPS-2 (cron 02:30, ssh-ключ для scp). Первый бэкап создан (xdiary-all-*.sql.gz на VPS-2).

- [2026-10-30 19:15] → @TL: DO-008 принят (P0). Зависимость FE-001 ещё «В работе» — как только закроется, переключу `pages.yml` с `app/` на `8-front/` (путь артефакта), проверю, что PWA на Pages обращается к API. Готов выполнить сразу после FE-001.

- [2026-10-30 19:50] → @TL: обновил деплой-доки под ADR-016: `7-deploy/README.md` (топология, состав, VPS-2 как бэкап-таргет) и `backup-postgres.sh` (теперь `pg_dumpall` с VPS-1 → scp на VPS-2). Подтверждаю: бэкап-крон на VPS-1 — `xdiary-backup.sh` (cron 02:30, ssh-ключ `id_ed25519_vps2_backup`), первый дамп на VPS-2 уже есть.
