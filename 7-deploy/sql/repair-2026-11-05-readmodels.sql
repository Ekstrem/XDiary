-- Ремонт read-моделей: partners_read, journal_read (2026-11-05, DO-079 / BE-071)
--
-- ЗАЧЕМ. Read-модели наполняются только проекцией событий, а на проде схема event store
-- не создавалась (`Commands.DomainEvents` нет), поэтому `DataMigration:Enabled` уводит
-- сервис в краш-луп (`42P01`), а исторические строки восстановить из событий нельзя —
-- событий по ним просто нет. Единственный путь для истории — перенос ИЗ ИСТОЧНИКА.
--
-- ЧТО ДЕЛАЕТ. Копирует существующие строки из командных таблиц в read-модели.
--   partners: partners      -> partners_read
--   journal:  records       -> journal_read
--
-- ПОЧЕМУ НЕ СКРИПТОМ В СЕРВИСЕ. Канонический путь записи — `projection`; этот файл —
-- одноразовый ремонт, который обязан быть объявлен: `accept_read_path`
-- требует `--write-path sql --repair-declared-in DO-079 --repair-date 2026-11-05
-- --repair-command 7-deploy/sql/repair-2026-11-05-readmodels.sql` и проверяет, что файл
-- существует в репозитории. Ремонт «из чата» приёмку не проходит.
--
-- ИДЕМПОТЕНТНОСТЬ. `ON CONFLICT ("Id") DO NOTHING` — повторный запуск возвращает
-- `INSERT 0 0` и ничего не дублирует (проверено 2026-11-05: 13/13 и 4/4 после повтора).
--
-- ПРИМЕНЕНИЕ (на хосте контура, rootless podman у пользователя деплоя):
--   podman exec -i xdiary-pg psql -U postgres -d partners < repair-2026-11-05-readmodels.sql
--   podman exec -i xdiary-pg psql -U postgres -d journal  < repair-2026-11-05-readmodels.sql
-- (в файле два независимых блока: первый — partners, второй — journal; каждый выполняется
--  в своей базе, «чужие» таблицы в другой базе просто не существуют — применяйте по одной)
--
-- ПРОВЕРКА ПОСЛЕ (замер, а не отчёт):
--   select (select count(*) from partners) || '/' || (select count(*) from partners_read);
--   select (select count(*) from records)  || '/' || (select count(*) from journal_read);

-- ============================ partners =====================================
INSERT INTO partners_read ("Id","Name","Photo","Gender","Notes","ClientId","DeletedAt")
SELECT "Id","Name",COALESCE("Photo",''),COALESCE("Gender",''),COALESCE("Notes",''),
       "ClientId","DeletedAt"
FROM partners
ON CONFLICT ("Id") DO NOTHING;

-- ============================ journal ======================================
-- JSON-поля в `records` уже сериализованы, поэтому переносятся как есть.
-- `PartnersNames` — резолв имён на чтении, исторически пустой массив.
INSERT INTO journal_read
  ("Id","ClientId","Partners","PartnersNames","StartedAt","DurationMin","Place",
   "Positions","Tags","Costumes","Toys","Fantasies","Contraceptives","Orgasms",
   "ContraceptiveCount","Garmin","GarminId","CreatedAt","UpdatedAt","Deleted")
SELECT r."Id", r."ClientId",
       COALESCE(r."PartnersJson",'[]'),
       '[]',
       COALESCE(r."StartedAt",''),
       COALESCE(r."DurationMin",0),
       COALESCE(r."Place",''),
       COALESCE(r."PositionsJson",'[]'),
       COALESCE(r."TagsJson",'[]'),
       COALESCE(r."CostumesJson",'[]'),
       COALESCE(r."ToysJson",'[]'),
       COALESCE(r."FantasiesJson",'[]'),
       COALESCE(r."ContraceptivesJson",'[]'),
       COALESCE(r."OrgasmsJson",'[]'),
       COALESCE(r."ContraceptiveCount",0),
       COALESCE(r."Garmin",false),
       COALESCE(r."GarminId",''),
       COALESCE(r."CreatedAt",''),
       COALESCE(r."UpdatedAt",''),
       COALESCE(r."Deleted",false)
FROM records r
ON CONFLICT ("Id") DO NOTHING;
