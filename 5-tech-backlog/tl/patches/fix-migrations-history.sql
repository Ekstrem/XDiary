-- fix-migrations-history.sql — снять рестарт-петлю journal/partners (42P07). DO-135
-- Выполнять в БД СООТВЕТСТВУЮЩЕГО сервиса (journal-БД для journal, partners-БД для partners).
--
-- Как запускать (пример для journal; имя БД/пользователя уточнить по secrets/.env):
--   podman exec -i xdiary-pg psql -U <user> -d <journal_db> -f - < fix-migrations-history.sql
--
-- Проверка ДО: обе строки истории должны ОТСУТСТВОВАТЬ, а таблицы — существовать.

SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '2026110517%';

SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('journal_defaults','journal_read','partners_directory','partners_read');

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105170525_AddReadStores', '8.0.11')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105170447_AddReadStores', '8.0.11')
ON CONFLICT ("MigrationId") DO NOTHING;

SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '2026110517%';

-- После: podman restart xdiary-journal xdiary-partners
-- Откат: DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" IN
--   ('20261105170525_AddReadStores','20261105170447_AddReadStores');
