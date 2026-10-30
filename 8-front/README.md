# 8-front — адаптация PWA к микросервисам

Рабочая папка фронтенд-разработчика. Задача: перевести PWA с IndexedDB на бэкенд (шлюз `/api/v1/...`), **не меняя дизайн**.

## Исходные данные
- Текущий PWA: `app/` (HTML/CSS/JS, IndexedDB, PIN, CSV).
- Бэкенд: 10 микросервисов + ApiGateway (YARP) в `6-msa/`, контракты в `03-ddd/`, ADR в `04-architecture/ADL/`.

## Задачи (5-tech-backlog/front-end)
- **FE-001 (P0):** journal/partners/catalog с IndexedDB → API.
- **FE-002 (P0):** JWT (access+refresh, Bearer, refresh на 401, PIN локально).
- **FE-003 (P1):** миграция IndexedDB/CSV → бэк (идемпотентно по `id`).

## Подход (FE-001)
1. Скопировать `app/` → `8-front/` (та же вёрстка/стили).
2. Добавить слой API-клиента `js/api.js` (fetch + JWT из FE-002).
3. Заменить вызовы `DB.*` (journal/partners/catalog) на REST-запросы.
4. Маппинг UI-модели ↔ API-DTO (UI остаётся прежним, API — корректным):

| UI (сейчас) | API (шлюз) |
|---|---|
| `partner` (имя, одна строка) | `partners: [partnerId]` (uuid; резолв имени через `GET /api/v1/partners?select=1`) |
| `orgasmsMe` + `orgasmMeType[]`/`orgasmMeRating` | `orgasms: [{participant:"me", count, types[], rating}]` |
| `orgasmsPartner` + `orgasmPartnerType[]`/`orgasmPartnerRating` | `orgasms: [{participant:partnerId, count, types[], rating}]` |
| каталог `kind/name/photo/notes/brand/archived/partner` | `ItemDraft { kind, name, photo, notes, brand, fantasyPartner }` (архив — команды Archive/Restore) |
| партнёр `name/photo/gender/notes` | `PartnerDraft { name, photo, gender, notes }` |

## Ключевые эндпоинты (шлюз `/api/v1/...`)
- **Journal:** `GET /journal`, `GET /journal/{id}`, `GET /journal/draft/{id}`, команды `POST/PUT/DELETE /journal/...` (см. `03-ddd/BC-Journal/03-command-api.md`).
- **Partners:** `GET /partners`, `GET /partners?select=1`, `GET /partners/{id}`, CRUD.
- **Catalog:** `GET /catalog?kind=...`, `GET /catalog/{id}`, CRUD.

## Аутентификация (FE-002)
- JWT access+refresh, заголовок `Authorization: Bearer`.
- При 401 — перевыпуск по refresh.
- Локальный PIN (PBKDF2) остаётся как доп. слой блокировки.
- Logout очищает токены.

## Конфигурация
- Базовый URL шлюза — через настройки (локальный `docker-compose.single` и продакшен).

## Статус
- FE-001: В работе.
