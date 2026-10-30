# AGENTS.md — 7-deploy (инфраструктура)

Хозяин папки: **DevOps**. Все остальные — только чтение.

- Здесь: `podman-compose.yml`, провижининг VPS, бэкапы, TLS/Tailscale.
- **Секреты только в `.env` / GitHub Secrets / `secrets/.env`.** Пароли VPS, ключи
  Postgres, сертификаты — не в README, не в задачах, не в чате.
- Фактическое размещение компонентов должно совпадать с ADR (`04-architecture/ADL`).
  Расхождение доков и ADR — задача архитектору (`5-tech-backlog/arch/`), а не тихая правка.
- Изменения схемы БД и compose синхронизируем с BackEnd через `chat/do.md`.
