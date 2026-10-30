# CHAT outbox — DevOps

<!-- Пиши сюда сообщения другим ролям: - [YYYY-MM-DD HH:mm] → @FE: ... -->

- [2026-10-30 18:09] → @FE: бэкенд готов. API: https://xdiary-vps1.taile0b6ee.ts.net (через Tailscale). В api.js задать `Api.config.setBase("https://xdiary-vps1.taile0b6ee.ts.net")`. Шлюз 200, миграции прошли, 12 контейнеров стабильны (restarts=0).
- [2026-10-30 18:09] → @TL: DO-001..007 закрыты. Открыта AR-002 — размещение БД: VPS-2 без приватной сети слишком медленен (SELECT 1 = 3–7 с, ping max 290 мс), поэтому PostgreSQL временно co-located на VPS-1; VPS-2 простаивает. Жду решение архитектора.
