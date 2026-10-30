-- XDiary: 10 ролей + 10 БД (профиль Single).
-- ADR-001 (10 сервисов), ADR-003/004 (PostgreSQL, одна БД на сервис: event store + read-модель).
--
-- Пароли НЕ хранятся в этом файле (репозиторий публичный) — передаются через psql-переменные.
-- Запуск (пароли подставляет provision-vps2.sh):
--   sudo -u postgres psql -v ON_ERROR_STOP=1 \
--     -v pw_journal='...' -v pw_partners='...' -v pw_catalog='...' \
--     -v pw_somatic_series='...' -v pw_daily_metrics='...' \
--     -v pw_garmin_gateway='...' -v pw_apple_health_gateway='...' \
--     -v pw_analytics='...' -v pw_privacy='...' -v pw_portability='...' \
--     -f db/init-databases.sql
--
-- ВНИМАНИЕ: выполнять ОДИН раз на свежем кластере (повторный запуск упадёт на существующих объектах).

-- 10 ролей (по одной на сервис; login + пароль)
CREATE ROLE journal              LOGIN PASSWORD :'pw_journal';
CREATE ROLE partners             LOGIN PASSWORD :'pw_partners';
CREATE ROLE catalog              LOGIN PASSWORD :'pw_catalog';
CREATE ROLE somatic_series       LOGIN PASSWORD :'pw_somatic_series';
CREATE ROLE daily_metrics        LOGIN PASSWORD :'pw_daily_metrics';
CREATE ROLE garmin_gateway       LOGIN PASSWORD :'pw_garmin_gateway';
CREATE ROLE apple_health_gateway LOGIN PASSWORD :'pw_apple_health_gateway';
CREATE ROLE analytics            LOGIN PASSWORD :'pw_analytics';
CREATE ROLE privacy              LOGIN PASSWORD :'pw_privacy';
CREATE ROLE portability          LOGIN PASSWORD :'pw_portability';

-- 10 БД (owner = своя роль; права на public-схему своей БД даёт владение)
CREATE DATABASE journal              OWNER journal;
CREATE DATABASE partners             OWNER partners;
CREATE DATABASE catalog              OWNER catalog;
CREATE DATABASE somatic_series       OWNER somatic_series;
CREATE DATABASE daily_metrics        OWNER daily_metrics;
CREATE DATABASE garmin_gateway       OWNER garmin_gateway;
CREATE DATABASE apple_health_gateway OWNER apple_health_gateway;
CREATE DATABASE analytics            OWNER analytics;
CREATE DATABASE privacy              OWNER privacy;
CREATE DATABASE portability          OWNER portability;

\echo 'OK: 10 roles + 10 databases created'
