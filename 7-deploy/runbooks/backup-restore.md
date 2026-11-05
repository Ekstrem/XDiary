# Бэкап и восстановление PostgreSQL

> «Почему» — `ADR-016` (бэкап-таргет на VPS-2). «Как» — здесь.
> Проверено фактами 2026-11-05: cron живой, файлы есть на обоих контурах, восстановление **ни разу не выполнялось**.

## 1. Что и куда бэкапится

| | |
|---|---|
| скрипт | `7-deploy/backup-postgres.sh` (на VPS-1, пользователь `deploy`) |
| cron | `30 2 * * * /home/deploy/xdiary-backup.sh >> /home/deploy/backup.log 2>&1` |
| что | `podman exec xdiary-pg pg_dumpall -U postgres \| gzip` — **весь кластер**, не одна база |
| локально | `/home/deploy/backups/xdiary-all-<дата>_<время>.sql.gz` |
| копия | `ubuntu@77.91.73.67:/home/ubuntu/backups/` (VPS-2) |
| ротация | `KEEP_DAYS=14`, локально; на VPS-2 ротации нет |

**Замер 2026-11-05:** локально **7** файлов, на VPS-2 **7**; последний — `xdiary-all-2026-11-05_053001.sql.gz` (**80K**). То есть механизм работает, а не «настроен».

## 2. Восстановление

> ⚠️ `pg_dumpall` — **весь кластер** (все 12 баз). Восстановление затрёт текущее состояние целиком,
> поэтому на живом контуре это аварийная операция, а не рутина.

```bash
# 1) остановить сервисы, чтобы никто не писал
cd /opt/xdiary/7-deploy && bash recreate-from-env.sh --stop 2>/dev/null || true
podman stop $(podman ps -q --filter name=xdiary- | grep -v xdiary-pg) 2>/dev/null

# 2) взять бэкап-файл (последний или конкретный)
ls -lh /home/deploy/backups/ | tail -3
F=/home/deploy/backups/xdiary-all-2026-11-05_053001.sql.gz

# 3) восстановить в чистый кластер
podman exec -i xdiary-pg psql -U postgres -d postgres -c 'DROP SCHEMA public CASCADE; CREATE SCHEMA public;' 2>/dev/null
gunzip -c "$F" | podman exec -i xdiary-pg psql -U postgres -d postgres

# 4) поднять сервисы
cd /opt/xdiary/7-deploy && bash recreate-from-env.sh

# 5) проверить числами (см. §3)
```

Если восстанавливать **одну** базу, а не кластер: распаковать и вырезать её секцию, либо
`pg_restore`-путь требует `pg_dump -Fc` — текущий формат **plain SQL**, поэтому только `psql`.

## 3. Проверка после восстановления (обязательно, а не «сервис поднялся»)

```bash
Q() { podman exec xdiary-pg psql -U postgres -d "$1" -Atc "$2"; }
# пары: источник / read-модель должны совпадать
Q partners 'select (select count(*) from partners)||chr(47)||(select count(*) from partners_read);'
Q journal  'select (select count(*) from records)||chr(47)||(select count(*) from journal_read);'
# миграции на месте
Q journal  'select count(*) from "__EFMigrationsHistory";'
```

Норма на 2026-11-05: `partners 13/13`, `journal 4/4`. **`read`-число меньше `источника` = потеря истории**,
а не «пустая база»: read-модель заполняется проекцией/ремонтом, см. `7-deploy/sql/repair-2026-11-05-readmodels.sql`.

## 4. Известные пробелы (честно, 2026-11-05)

1. **Восстановление ни разу не выполнялось** — ни на dev, ни на проде. Бэкап без проверки восстановления
   не подтверждает ничего, кроме того, что `gzip` работает.
2. **Ротация только локальная:** на VPS-2 файлы копятся без ограничения (7 файлов сейчас — не проблема,
   но это не «политика», а её отсутствие).
3. **Свежесть:** последний бэкап `05:30`, а ремонт read-моделей был позже (13:0x) — то есть текущее
   состояние в бэкап **не попало**. После значимых операций бэкап делается вручную:
   `bash /home/deploy/xdiary-backup.sh`.
4. Нет автоматической проверки «бэкап вчерашний и непустой» — кандидат в гейт (`rc=2`, если файла нет).
