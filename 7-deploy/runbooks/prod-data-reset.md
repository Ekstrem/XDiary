# Runbook — зачистка бизнес-данных на проде (перед чистыми CJ)

**Когда:** нужен «чистый» прод для сценариев (CJ/E2E): убрать старые клиенты, записи, партнёров, каталог.
**Что не удаляется:** схема, таблицы и `__EFMigrationsHistory` — сервисы продолжают работать без миграций.

**Важно:** это единственная разрешённая запись на проде, кроме деплоя. Выполняется по прямому указанию
владельца (`2026-11-07`), а не по своей инициативе.

## Процедура (порядок обязателен)

### 1. Бэкап и его проверка

```bash
TS=$(date +%Y%m%d-%H%M%S); BK="$HOME/backups/prod-cleanup/$TS"; mkdir -p "$BK"
for db in analytics apple_health_gateway catalog client daily_metrics garmin_gateway \
          journal partners portability privacy somatic_series; do
  podman exec xdiary-pg pg_dump -U postgres -Fc -d "$db" > "$BK/$db.dump"
done
sha256sum "$BK"/*.dump > "$BK/SHA256SUMS"
```

**Проверка дампа — по объектам, а не по размеру** (пустая БД даёт маленький, но валидный дамп):

```bash
for db in ...; do
  n=$(podman exec -i xdiary-pg pg_restore -l < "$BK/$db.dump" | grep -vc '^;')
  t=$(podman exec xdiary-pg psql -U postgres -d "$db" -t -A -c "select count(*) from pg_stat_user_tables;")
  # ок, если объектов > 0 ИЛИ в БД вообще нет таблиц (пример: analytics)
done
```

Если хоть одна БД с таблицами дала 0 объектов — **остановиться**, зачистку не начинать.

### 2. Зачистка

```bash
for db in ...; do
  tbls=$(podman exec xdiary-pg psql -U postgres -d "$db" -t -A -c \
    "select string_agg(format('%I.%I', schemaname, relname), ', ')
       from pg_stat_user_tables where relname <> '__EFMigrationsHistory';")
  [ -n "$tbls" ] && podman exec xdiary-pg psql -U postgres -d "$db" \
    -c "TRUNCATE TABLE $tbls RESTART IDENTITY CASCADE;"
done
```

`RESTART IDENTITY` — чтобы последовательности не продолжали старые номера; `CASCADE` — из-за внешних ключей.

### 3. Квитанция

- по каждой БД: строк **0**, `__EFMigrationsHistory` **на месте** (иначе сервис уйдёт в миграции заново);
- `/health` → **200**, `GET /api/v1/clients` без токена → **401** (auth-контур цел);
- в логах `client/journal/partners` — 0 исключений (нет `28P01`, `42P01`).

## Замер 2026-11-07 11:47 (прод)

| БД | было строк | стало |
|---|---|---|
| `client` | 47 | 0 |
| `journal` | 8 (4 записи + 4 read) | 0 |
| `partners` | 26 (13 + 13 read) | 0 |
| `catalog` | 2 | 0 |
| `privacy` | 1 (`pin_credential`) | 0 |
| остальные (7 БД) | 0 | 0 (только миграции) |

Бэкап: `~/backups/prod-cleanup/20261107-114708/` — **11 дампов**, 568 КБ, `SHA256SUMS` рядом.
`/health` 200, анонимный `GET /clients` 401, `28P01`/`42P01` в логах — 0.

## Восстановление (если понадобится)

```bash
podman exec -i xdiary-pg pg_restore -U postgres -d <db> --clean --if-exists < "$BK/<db>.dump"
```

Восстанавливать **в ту же БД** после `--clean`; на живой прод — только по решению владельца.
