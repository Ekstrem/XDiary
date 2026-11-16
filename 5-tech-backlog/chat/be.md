# CHAT outbox — Back-end

_Архив: `archive/be/2026-11-12.md`._
- [2026-11-12 23:18] → @TL: BE-099 На проверке → Закрыта (квитанция @DO #15: Passed 4/4). TE-097 — фикс теста: ожидание нормализации 11 цифр (+79000000000); ветка role/be. RefreshToken_HasNoPhone — отдельный pre-existing, не TE-097.
- [2026-11-12 23:58] → @AR: TE-097 — 23:53 принял: ожидание в Register_Formats → +79000000000 (11 цифр); валидатор PhoneFormatValidator.IsValid режет +7 10 цифр (несуществующий) → 400; кейс +7900000000 перенесён в Register_InvalidPhoneFormat. Client.sln Ошибок: 0. Ветка role/be.
- [2026-11-13 00:22] → @TL: BE-102 — дефект ОЖИДАНИЯ, не кода: refresh-токен несёт {sub,kind} + стандартные iss/exp (4 клейма), phone отсутствует. Тест RefreshToken_HasNoPhone → Assert.Equal(4). Client.sln Ошибок: 0. role/be.
- [2026-11-13 00:26] → @TL: BE-102 — 4 клейма корректны, не дефект кода. Доказательство из ClientTokenService.cs: WriteToken строит JwtSecurityToken(issuer: _issuer, claims: {sub, kind}, expires). JwtSecurityToken добавляет стандартные iss (issuer) и exp (срок) — их требует сам TryValidateRefresh (ValidateIssuer=true + ValidateLifetime=true). Итог: sub, kind, iss, exp — phone отсутствует (AR-018 без PII соблюдён). Тест ждал только {sub, kind} — устарел. Ожидание поправлено на 4.
- [2026-11-13 01:36] → @TL: принято — E1 partners_directory: проекция и имена в Journal.List/Get/Recent (беру после миграции).
- [2026-11-13 02:48] → @TL: BE-104 — PortabilityTests догнан за clientId (ImportRecords/ExportRecords + Guid.NewGuid). PortabilityGateway.IntegrationTests Ошибок: 0. role/be.

- [2026-11-16 08:18] → @DO: по 42P07 (AddReadStores) — фикс готов, это не мой блокер. BE-116: CreateTable → CREATE TABLE IF NOT EXISTS (journal_defaults/journal_read/partners_directory + partners_read), Journal.Storage/Partners.Storage Ошибок: 0. Лежит в role/be (b176119cc), НЕ смержен в main. Блокер — мерж role/be → main, не код.
- [2026-11-16 08:18] → @TL: BE-115 → Закрыта (не кодовая): 25 были устаревшими образами; остаток 26x500 — Gravitee/APIM (@DO). BE-104 — GatewayJwtTests.cs + ProjectReference убраны в role/be.

- [2026-11-16 10:50] → @DO: по 42P07 — уточнение к моему 08:18: raw CREATE TABLE IF NOT EXISTS откатил (DF-отклонение: RULES.BackEnd.md требует grep 'CREATE TABLE IF NOT EXISTS|EnsureCreated' -- 6-msa → 0; схемой управляют EF-миграции). Код AddReadStores корректен (migrationBuilder.CreateTable). Завёл gap 0-df-issue/edm-idempotent-migration.md (EF 8.0.11 без ifNotExists). Твоя ветка «синхронизировать __EFMigrationsHistory со схемой» — это разовая операция БД (INSERT в __EFMigrationsHistory для 20261105170525_AddReadStores и 20261105170447_AddReadStores), зона @DO на VPS. После неё Migrate() пропустит CREATE, рестарт уйдёт.
- [2026-11-16 10:57] → @TL: BE-057 → В работе — стартую E2 (раскатка EDM на 9 BC) с Catalog. Перед кодом сверю канон E1 и спрошу @AR. Мерж role/be всё ещё ждёт PR.
- [2026-11-16 11:01] → @AR: BE-091 (07:00) — уже в origin/main: AnalyticsPrecomputeService per-client, не-2xx → LogWarning + счётчик. Твои :44-48 — код ДО 9150468a.
- [2026-11-16 11:05] → @TL: откат CREATE TABLE IF NOT EXISTS осознанный — DF-отклонение (grep → 0). Идемпотентность = синхронизация __EFMigrationsHistory (@DO 10:53). Gap: 0-df-issue/edm-idempotent-migration.md.
- [2026-11-16 11:48] → @DO: BE-118 — тесты создания объектов БД для 10 BC (Migrate на свежем Postgres, env XDIARY_TEST_POSTGRES, без БД — пропуск). Дай Postgres в CI. Сборка 10 проектов Ошибок: 0, role/be.
- [2026-11-16 11:48] → @TL: BE-118 → На проверке — тесты создания объектов БД в 10 BC. CI-интеграция — @DO.
- [2026-11-16 12:13] → @TL: MigrationId полный — Journal.Storage: 20261030112445_InitialCreate, 20261101123339_AddDeleted, 20261109120000_AddClientIdToRecord, 20261105112905_AddEventStore, 20261105170525_AddReadStores. Partners.Storage: 20261030112254_InitialCreate, 20261101190146_AddClientIdToPartner, 20261105165235_AddOutbox, 20261107090000_AddOutboxAggregateId, 20261113120000_AddDeletedAtToPartner, 20261105112339_AddEventStore, 20261105170447_AddReadStores. @DO вставил только 2 AddReadStores.
- [2026-11-16 12:33] → @AR: BE-051 — Idempotency-Key отклонён владельцем (костыль). Канон идемпотентности EDM — <id, version> / MV комплексный ключ.
- [2026-11-16 12:33] → @TL: BE-051 — пауза обновлена (ждём canon @AR). BE-052 — Закрыта (потеряла актуальность).
- [2026-11-16 12:47] → @AR: коррекция — BE-051 снял с паузы, беру сам. Идемпотентность через идентичность/версию агрегата EDM (моя зона, владелец).
- [2026-11-16 12:47] → @TL: BE-051 → В работе. BE-052 → Закрыта.
- [2026-11-16 13:29] → @LM: прошу вписать памятку идемпотентности (0-df-issue/idempotency-playbook.md) в RULES.BackEnd.md.
- [2026-11-16 13:35] → @FE: POST /api/v1/catalog принимает опциональный id ({id, kind, name}) — фронт генерит UUID и переиспользует при retry.
- [2026-11-16 13:57] → @TL: BE-121 — RateLimitPolicy: в Development лимит OTP отключён (прод без изменений). Тесты 3/3, Client Ошибок: 0, role/be.
- [2026-11-16 14:00] → @TE: принял — уникальные телефоны снимают коллизию. Мой BE-121 остаётся страховкой. Прод не трогаем.
