# Ранбук: бэкап и восстановление конфигурации APIM

> `$CREDS` ниже — **файл кредов стенда** (600, вне репозитория; путь знает координатор).
> В git имя файла кредов не пишем — §6: репозиторий публичный.


Владелец: **@DO**. Решение, ради которого это существует: **пересоздавать контур APIM нельзя** —
в нём контракты продукта (12 API). Значит нужен воспроизводимый снимок и понятное восстановление.

Инструмент: [`7-deploy/apim/backup.sh`](../apim/backup.sh) (в репозитории, копируется на прод как есть).

## Что попадает в снимок

| часть | содержимое | зачем |
|---|---|---|
| `gravitee.archive.gz` | `mongodump` всей базы `gravitee`: API и их определения, планы, страницы, организации, среды, членства, роли, словари, **пользователи (с хешами)** | полное восстановление контура |
| `apis.json` | `mongoexport` коллекции `apis` — **12 API с определениями V4** | диффы, ревью контрактов |
| `plans.json`, `pages.json`, `applications.json` | планы/страницы/приложения | точечное восстановление |
| `organisations.json`, `environments.json`, `memberships.json`, `roles.json`, `dictionaries.json` | доступы и структура | восстановление прав |
| `MANIFEST.txt` | дата, хост, образ management, `api_count`, размеры | квитанция: что и когда снято |

**Пароли пользователей в читаемые JSON не попадают** — коллекция `users` есть только в `mongodump`.

Почему не через Management API: в этой сборке APIM (4.4) `GET /apis/{id}/_export` отдаёт **404**,
`GET /apis/{id}` — пустое тело, список пагинируется по 10. Проверено 2026-11-06, поэтому читаемые
JSON берутся из базы (`mongoexport`), а не из HTTP.

## Запуск

```bash
# на проде, от deploy; скрипт уже лежит в $HOME/apimtools/backup.sh
bash $HOME/apimtools/backup.sh              # снимок + архив + sha256, хранение 7 дней
bash $HOME/apimtools/backup.sh --keep 14    # другой срок хранения
bash $HOME/apimtools/backup.sh --out /data/apim-backups   # другое место
```

Раскладка:

```
~/backups/apim/20261106-121917/            директория снимка (11 файлов, ~536 K)
~/backups/apim/apim-backup-20261106-121917.tar.gz
~/backups/apim/apim-backup-20261106-121917.tar.gz.sha256
```

Ротация встроена: `--keep N` удаляет архивы и снимки **старше N дней** (по умолчанию 7).

## Проверка снимка (обязательно после прогона)

```bash
A=$(ls -t ~/backups/apim/apim-backup-*.tar.gz | head -1)
sha256sum -c "$A.sha256"                    # ожидаемо: OK
tar -tzf "$A" | wc -l                       # файлов в архиве
sed 's/^/  /' "$(dirname "$A")"/$(basename "$A" .tar.gz)/MANIFEST.txt   # api_count=12
python3 -c "import json,sys; d=json.load(open('$(dirname "$A")/$(basename "$A" .tar.gz)/apis.json')); print('API:', len(d))"
```

Ожидаемо на 2026-11-06: `api_count=12`, `collections_exported=9`, дамп ~214 КБ, архив ~215 КБ, `OK`.

## Восстановление

> Восстанавливать **поверх прода** нельзя: это перезапишет контракты и доступы. Сначала — в отдельную
> базу и отдельный контейнер, проверить состав, и только потом решать с @TL/владельцем.

### 1. Отдельная база и отдельный Mongo (безопасно)

```bash
D=~/backups/apim/20261106-121917      # нужный снимок
podman run -d --name apim-restore --network apim docker.io/library/mongo:6
podman cp "$D/gravitee.archive.gz" apim-restore:/tmp/gravitee.archive.gz
podman exec apim-restore sh -c 'mongorestore --archive=/tmp/gravitee.archive.gz --gzip --nsFrom "gravitee.*" --nsTo "gravitee_restore.*"'
podman run --rm --network apim docker.io/library/mongo:6 \
  mongosh --host apim-restore --quiet gravitee_restore --eval 'print("apis=" + db.apis.countDocuments({}))'
podman rm -f apim-restore
```

Проверка: `apis=12`. Если число другое — снимок не тот, разбираться до любых действий с продом.

### 2. Точечное восстановление одного API (по `apis.json`)

`apis.json` — массив документов API (JSON). Восстановление одного API = взять его документ и
вставить/обновить в коллекции `apis` прода **после согласования с @TL** и с проверкой
`check_apim_drift` до/после.

## Автоматизация

Скрипт можно ставить в cron на проде (ежедневно, 04:30):

```cron
30 4 * * * /bin/bash $HOME/apimtools/backup.sh --keep 14 >> $HOME/apim-backup.log 2>&1
```

Альтернатива без cron — GitHub Actions (`workflow_dispatch`), как у `apim-drift`: тот же скрипт
копируется на прод и запускается по расписанию. Пока не включено: решает @TL (нагрузка на прод).

## Если что-то пошло не так

| симптом | что делать |
|---|---|
| `mongodump` вернул не 0 | смотреть `/tmp/mongodump.log`, **не** считать снимок сделанным |
| `apis.json` пустой или `api_count=0` | контейнер `xdiary-apim-mongo` не тот/не поднялся — проверить `podman inspect` |
| `sha256sum -c` → FAILED | архив повреждён (диск?), снять новый снимок, старый не удалять до разбора |
| в каталоге кончилось место | `--keep 3`, `df -h /home`, чистить старые снимки вручную |

## Связанное

- Ранбук APIM: [`apim.md`](apim.md) — контур, доступы, импорт, drift.
- Доступы: `5-tech-backlog/devops/secrets/APIM-credentials.md` (gitignored) и `$CREDS` (600).
- Карточка: `5-tech-backlog/devops/DO-075.md`.
