#!/usr/bin/env bash
# Бэкап конфигурации Gravitee APIM (DO-075).
#
# Зачем: в APIM живут КОНТРАКТЫ продукта (12 API). Пересоздание контура недопустимо
# (решение владельца 2026-11-06), поэтому нужен воспроизводимый снимок конфигурации:
#   1) mongodump базы `gravitee` — всё состояние менеджмента (API и их определения,
#      планы, страницы, организации, среды, членства, роли, словари, пользователи);
#   2) читаемые JSON по коллекциям (`mongoexport`) — их можно диффать и смотреть глазами.
#      Пароли пользователей в читаемую копию НЕ попадают: коллекция `users` есть только
#      в mongodump.
#   3) манифест + tar.gz + sha256 + ротация.
#
# Почему не Management API для экспорта: в этой сборке APIM `_export` отдаёт 404,
# `GET /apis/{id}` — пустое тело, а список пагинируется по 10 (12 API = 2 страницы).
# Проверено 2026-11-06. Из базы — надёжнее и без кредов.
#
# Использование (на проде, от deploy):
#   bash 7-deploy/apim/backup.sh                 # снимок в ~/backups/apim/<ts>/ + архив
#   bash 7-deploy/apim/backup.sh --keep 14       # удалить архивы старше 14 дней
#
# Проверка:
#   tar -tzf ~/backups/apim/apim-backup-<ts>.tar.gz | head
#   sha256sum -c ~/backups/apim/apim-backup-<ts>.tar.gz.sha256
#
# Восстановление — только в отдельную базу (см. `7-deploy/runbooks/apim-backup.md`).
set -uo pipefail

MONGO_CT=xdiary-apim-mongo
MGMT_CT=xdiary-apim-management
DB=gravitee
BASE_DIR="${HOME}/backups/apim"
KEEP_DAYS=7
TS="$(date +%Y%m%d-%H%M%S)"
OUT="${BASE_DIR}/${TS}"

while [ $# -gt 0 ]; do
  case "$1" in
    --keep) KEEP_DAYS="$2"; shift 2 ;;
    --out)  BASE_DIR="$2"; OUT="${BASE_DIR}/${TS}"; shift 2 ;;
    *) echo "неизвестный аргумент: $1" >&2; exit 2 ;;
  esac
done

mkdir -p "${OUT}"
echo "== APIM backup ${TS} =="
echo "  контейнер ${MONGO_CT}: $(podman inspect "${MONGO_CT}" --format '{{.State.Status}}/restarts={{.RestartCount}}' 2>/dev/null || echo 'НЕТ')"

# ---------- 1) mongodump (полное состояние менеджмента) ----------
echo "-- 1) mongodump базы ${DB}"
podman exec "${MONGO_CT}" sh -c "rm -f /tmp/${DB}.archive.gz; mongodump --db ${DB} --archive=/tmp/${DB}.archive.gz --gzip" \
  >/tmp/mongodump.log 2>&1
rc=$?
if [ $rc -ne 0 ]; then
  echo "   ОШИБКА mongodump (rc=$rc):" >&2
  tail -5 /tmp/mongodump.log >&2
  exit 1
fi
podman cp "${MONGO_CT}:/tmp/${DB}.archive.gz" "${OUT}/${DB}.archive.gz" >/dev/null 2>&1
echo "   размер: $(stat -c%s "${OUT}/${DB}.archive.gz" 2>/dev/null || echo 0) байт"

# ---------- 2) читаемые JSON по коллекциям ----------
echo "-- 2) читаемые JSON из базы (mongoexport)"
EXPORTED=0
for coll in apis plans pages organisations environments memberships roles dictionaries applications; do
  exists=$(podman exec "${MONGO_CT}" mongosh --quiet "${DB}" --eval "db.getCollectionNames().includes('${coll}')" 2>/dev/null | tr -d '\r')
  [ "${exists}" = "true" ] || continue
  podman exec "${MONGO_CT}" sh -c "rm -f /tmp/${coll}.json; mongoexport --quiet --db ${DB} --collection ${coll} --jsonArray --out /tmp/${coll}.json" >/dev/null 2>&1
  if [ $? -eq 0 ]; then
    podman cp "${MONGO_CT}:/tmp/${coll}.json" "${OUT}/${coll}.json" >/dev/null 2>&1
    EXPORTED=$((EXPORTED + 1))
    echo "   ${coll}.json — $(stat -c%s "${OUT}/${coll}.json" 2>/dev/null || echo 0) байт"
  else
    echo "   ${coll}: mongoexport не удался (пропущено)" >&2
  fi
done
echo "   коллекций выгружено: ${EXPORTED}"

# ---------- 3) манифест ----------
{
  echo "created_at=${TS}"
  echo "host=$(hostname)"
  echo "mongodb=${DB}"
  echo "management_image=$(podman inspect "${MGMT_CT}" --format '{{.Config.Image}}' 2>/dev/null)"
  echo "api_count=$(podman exec "${MONGO_CT}" mongosh --quiet "${DB}" --eval 'db.apis.countDocuments({})' 2>/dev/null | tr -d '\r')"
  echo "collections_exported=${EXPORTED}"
  echo "mongodump_size=$(stat -c%s "${OUT}/${DB}.archive.gz" 2>/dev/null || echo 0)"
  echo "backup_tool=7-deploy/apim/backup.sh"
} >"${OUT}/MANIFEST.txt"
echo "-- 3) MANIFEST.txt готов"

# ---------- 4) архив + sha256 ----------
echo "-- 4) архив"
tar -czf "${BASE_DIR}/apim-backup-${TS}.tar.gz" -C "${BASE_DIR}" "${TS}"
sha256sum "${BASE_DIR}/apim-backup-${TS}.tar.gz" >"${BASE_DIR}/apim-backup-${TS}.tar.gz.sha256"
echo "   ${BASE_DIR}/apim-backup-${TS}.tar.gz ($(stat -c%s "${BASE_DIR}/apim-backup-${TS}.tar.gz" 2>/dev/null || echo 0) байт)"
echo "   sha256: $(cut -d' ' -f1 "${BASE_DIR}/apim-backup-${TS}.tar.gz.sha256" | cut -c1-16)…"

# ---------- 5) ротация ----------
if [ "${KEEP_DAYS}" -gt 0 ] 2>/dev/null; then
  echo "-- 5) ротация: удаляю архивы и снимки старше ${KEEP_DAYS} дней"
  find "${BASE_DIR}" -maxdepth 1 -name 'apim-backup-*.tar.gz*' -mtime +"${KEEP_DAYS}" -print -delete 2>/dev/null | sed 's/^/   удалён: /'
  find "${BASE_DIR}" -maxdepth 1 -mindepth 1 -type d -mtime +"${KEEP_DAYS}" -print 2>/dev/null | while read -r d; do rm -rf "$d"; echo "   удалён каталог: $d"; done
fi

echo "== готово: ${OUT} =="
