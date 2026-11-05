# Бэкап и восстановление PostgreSQL

> «Почему» — `ADR-016` (бэкап-таргет на VPS-2). «Как» — здесь.
> Проверено фактами 2026-11-05 (DO-089): восстановление **выполнено** и числа совпали; при этом найдено, что
> бэкапы **молча обрезались** (`podman exec` терял вывод) — скрипт переделан, полнота проверяется до отправки.

## 1. Что и куда бэкапится

| | |
|---|---|
| скрипт | `7-deploy/backup-postgres.sh` (на VPS-1, пользователь `deploy`), он же `/home/deploy/xdiary-backup.sh` |
| cron | `30 2 * * * /home/deploy/xdiary-backup.sh >> /home/deploy/backup.log 2>&1` |
| что | `pg_dumpall` **внутри контейнера** в файл → `podman cp` → `gzip` — весь кластер (11 прикладных баз) |
| локально | `/home/deploy/backups/xdiary-all-<дата>_<время>.sql.gz` |
| копия | `ubuntu@77.91.73.67:/home/ubuntu/backups/` (VPS-2) |
| ротация | `KEEP_DAYS=14`, локально; на VPS-2 ротации нет |

**Замер 2026-11-05 (после исправления):** локально и на VPS-2 по **2 полных** файла; свежий —
`xdiary-all-2026-11-05_142059.sql.gz` (**516K**, **11** баз). Все неполные файлы удалены (их было 7 из 9 —
они давали частичное восстановление **молча**).

### Найденный дефект: `podman exec` обрезал дамп

Прежняя строка `podman exec xdiary-pg pg_dumpall -U postgres | gzip > file` теряла данные,
а `rc` конвейера был **0** (код от `gzip`), поэтому cron писал «backup … done» годами:

| способ | размер | баз |
|---|---|---|
| через `podman exec \| gzip` | 172K / 596K (разные прогоны) | **3 / 8** из 11 |
| внутри контейнера в файл | **720K** | **11** из 11 |

Лечение: дамп в контейнере → `podman cp` → сверка «число баз == число `CREATE DATABASE`»
**до** отправки → отправка → проверка `test -s` на VPS-2. Неполный дамп завершает скрипт `rc=2`
и никуда не уходит.

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

## 4. Проверка восстановлением (выполнена 2026-11-05, DO-089)

```
F=/home/deploy/backups/xdiary-all-2026-11-05_142059.sql.gz
podman run -d --name pg-rt -e POSTGRES_PASSWORD=x -e POSTGRES_HOST_AUTH_METHOD=trust postgres:16
gunzip -c "$F" | podman exec -i pg-rt psql -U postgres -d postgres -q     # rc=0, 3 c
# числа в восстановленном кластере:
#   партнёры 13, partners_read 13, records 4, journal_read 4, баз 12
podman rm -f pg-rt
```

Совпало с источником (`13/13`, `4/4`). Восстановление делалось в **одноразовый контейнер на проде**,
а не в dev: прод-дамп содержит реальные имена партнёров, и копировать их на dev — это перенос PII,
а не проверка. Проверяется то же самое — полнота и числа.

## 5. Известные пробелы (честно, 2026-11-05)

1. ~~Восстановление ни разу не выполнялось~~ — **выполнено 2026-11-05**, см. §4.
2. ~~Ротация только локальная~~ — **закрыто**: скрипт удаляет старше 14 дней и локально, и на VPS-2.
3. ~~Свежесть~~ — **закрыто**: после ремонта read-моделей снят свежий дамп (`142059`, 516K, 11 баз).
4. **Осталось:** нет автоматической проверки «бэкап вчерашний и полный» — кандидат в гейт
   (`rc=2`, если файла нет или в нём меньше 11 `CREATE DATABASE`). Плюс восстановление проверено
   только в одноразовый контейнер; полный disaster-recovery на живом контуре не репетировался.
