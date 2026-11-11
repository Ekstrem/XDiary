#!/bin/bash
# DO-111 (2026-11-07): ЕДИНСТВЕННЫЙ источник паролей БД — `secrets/.env`, по-сервисные
# ключи `DBPW_<SERVICE>` (общего `DB_PASSWORD` нет: у ролей постгреса разные пароли).
#
# 2026-11-10: заведено и НА ПРОДЕ — `/opt/xdiary/7-deploy/secrets/.env` держит 11 ключей
# `DBPW_*` (сверено по sha256 с env работающих контейнеров, 11/11), а сам скрипт на VPS-1
# исправлен с `Password=__REDACTED__` на `${DBPW_<SERVICE>}`. Причина, почему это было
# критично: `/home/deploy/deploy.sh` — шим с `exec bash /opt/xdiary/7-deploy/deploy.sh`,
# то есть исполнялся именно этот файл, и прод-деплой поставил бы сервисам заглушку (28P01).
# Почему: раньше один и тот же пароль был вписан в 22 места этого файла; на проде копия
# отстала, `-e` перекрывал `--env-file`, и сервисы падали с `28P01`. Путь «починил на
# сервере» больше не нужен: значение живёт в `.env` (вне git), скрипт его только читает.
#
if [ -f "$(dirname "$0")/secrets/.env" ]; then
  set -a; . "$(dirname "$0")/secrets/.env"; set +a
fi

# DO-111 + @TL 2026-11-10 12:06 (критерий приёмки): fail-fast вместо «тихого» пустого пароля.
# Без этой проверки отсутствующий DBPW_<SERVICE> раскрывался в пустую строку, сервис поднимался
# и падал в 28P01 — то есть деплой «зелёный», а сервис мёртв. Теперь деплой падает ДО создания
# контейнеров и печатает, каких ключей нет.
__missing=""
for __k in JOURNAL PARTNERS CATALOG SOMATIC_SERIES DAILY_METRICS GARMIN_GATEWAY \
           APPLE_HEALTH_GATEWAY ANALYTICS PRIVACY PORTABILITY CLIENT; do
  eval "__v=\${DBPW_$__k:-}"
  [ -n "$__v" ] || __missing="$__missing DBPW_$__k"
done
if [ -n "$__missing" ]; then
  echo "::error::deploy: не заданы пароли БД:$__missing — задай их в secrets/.env (DO-111)" >&2
  exit 2
fi
unset __k __v __missing
set -euo pipefail
# DO-066 / ADR-027: ручной тег при деплое ЗАПРЕЩЁН. Источник правды — 7-deploy/images.lock,
# который лежит в git: откат = revert коммита с этим файлом, а не `podman tag` на сервере.
if [ "$#" -gt 0 ]; then
  echo "запрещено: тег задаётся только images.lock (см. 7-deploy/images.lock, DO-066)" >&2
  exit 2
fi
LOCK_FILE="$(dirname "$0")/images.lock"

# DO-113: выкатка по digest. Тег остаётся для читаемости, но тянем ИМЕННО `@sha256:`
# из `images.lock`: тег можно перезаписать, digest — нельзя (ADR-027).
lock_digest() {
  awk -v s="$1" '!/^#/ && NF>=2 && $1==s {for (i=3;i<=NF;i++) if ($i ~ /^digest=/) {sub(/^digest=/,"",$i); print $i; exit}}' "$LOCK_FILE"
}
pull_by_digest() {
  svc="$1"; tag="$(awk -v s="$svc" '!/^#/ && NF>=2 && $1==s {print $2; exit}' "$LOCK_FILE")"
  dig="$(lock_digest "$svc")"; ref="ghcr.io/ekstrem/xdiary-$svc"
  if [ -n "$tag" ] && [ -n "$dig" ] && [ "$dig" != "-" ]; then
    echo "  pull $svc: $ref@$dig (тег $tag — только для чтения)"
    sudo -n podman pull -q "$ref@$dig" >/dev/null 2>&1 || return 1
  elif [ -n "$tag" ]; then
    echo "  pull $svc: $ref:$tag (digest в lock нет — старый формат)"
    sudo -n podman pull -q "$ref:$tag" >/dev/null 2>&1 || return 1
  fi
  return 0
}

[ -f "$LOCK_FILE" ] || { echo "нет $LOCK_FILE — деплой невоспроизводим" >&2; exit 2; }
TAG="$(awk '!/^#/ && NF>=2 {print $2; exit}' "$LOCK_FILE")"
[ -n "$TAG" ] || { echo "в $LOCK_FILE нет тегов" >&2; exit 2; }
cd /opt/xdiary/7-deploy
NET=xdiary
LBL='--label io.containers.autoupdate=registry'
for x in $(podman ps -aq 2>/dev/null); do [ "$x" = "$(podman ps -aq --filter name=xdiary-pg)" ] || podman rm -f $x >/dev/null 2>&1; done
podman run -d --name xdiary-journal --network $NET --ip 10.89.9.20 --hostname journal $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=journal;Username=journal;Password=${DBPW_JOURNAL}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=journal;Username=journal;Password=${DBPW_JOURNAL}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-journal:$TAG >/dev/null
podman run -d --name xdiary-partners --network $NET --ip 10.89.9.21 --hostname partners $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=partners;Username=partners;Password=${DBPW_PARTNERS}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=partners;Username=partners;Password=${DBPW_PARTNERS}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-partners:$TAG >/dev/null
podman run -d --name xdiary-catalog --network $NET --ip 10.89.9.22 --hostname catalog $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=catalog;Username=catalog;Password=${DBPW_CATALOG}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=catalog;Username=catalog;Password=${DBPW_CATALOG}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-catalog:$TAG >/dev/null
podman run -d --name xdiary-somatic-series --network $NET --ip 10.89.9.23 --hostname somatic-series $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=somatic_series;Username=somatic_series;Password=${DBPW_SOMATIC_SERIES}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=somatic_series;Username=somatic_series;Password=${DBPW_SOMATIC_SERIES}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-somatic-series:$TAG >/dev/null
podman run -d --name xdiary-daily-metrics --network $NET --ip 10.89.9.24 --hostname daily-metrics $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=daily_metrics;Username=daily_metrics;Password=${DBPW_DAILY_METRICS}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=daily_metrics;Username=daily_metrics;Password=${DBPW_DAILY_METRICS}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-daily-metrics:$TAG >/dev/null
podman run -d --name xdiary-garmin-gateway --network $NET --ip 10.89.9.25 --hostname garmin-gateway $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=garmin_gateway;Username=garmin_gateway;Password=${DBPW_GARMIN_GATEWAY}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=garmin_gateway;Username=garmin_gateway;Password=${DBPW_GARMIN_GATEWAY}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-garmin-gateway:$TAG >/dev/null
podman run -d --name xdiary-apple-health-gateway --network $NET --ip 10.89.9.26 --hostname apple-health-gateway $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=apple_health_gateway;Username=apple_health_gateway;Password=${DBPW_APPLE_HEALTH_GATEWAY}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=apple_health_gateway;Username=apple_health_gateway;Password=${DBPW_APPLE_HEALTH_GATEWAY}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-apple-health-gateway:$TAG >/dev/null
podman run -d --name xdiary-analytics --network $NET --ip 10.89.9.27 --hostname analytics $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=analytics;Username=analytics;Password=${DBPW_ANALYTICS}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=analytics;Username=analytics;Password=${DBPW_ANALYTICS}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-analytics:$TAG >/dev/null
podman run -d --name xdiary-privacy --network $NET --ip 10.89.9.28 --hostname privacy $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=privacy;Username=privacy;Password=${DBPW_PRIVACY}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=privacy;Username=privacy;Password=${DBPW_PRIVACY}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-privacy:$TAG >/dev/null
podman run -d --name xdiary-portability --network $NET --ip 10.89.9.29 --hostname portability $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=portability;Username=portability;Password=${DBPW_PORTABILITY}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=portability;Username=portability;Password=${DBPW_PORTABILITY}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-portability:$TAG >/dev/null
podman run -d --name xdiary-client --network $NET --ip 10.89.9.31 --hostname client $LBL --env-file secrets/.env -e "ConnectionStrings__CommandDb=Host=10.89.9.10;Port=5432;Database=client;Username=client;Password=${DBPW_CLIENT}" -e "ConnectionStrings__ReadDb=Host=10.89.9.10;Port=5432;Database=client;Username=client;Password=${DBPW_CLIENT}" -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped ghcr.io/ekstrem/xdiary-client:$TAG >/dev/null
podman run -d --name xdiary-api-gateway --network $NET --ip 10.89.9.30 --hostname api-gateway --add-host journal:10.89.9.20 --add-host partners:10.89.9.21 --add-host catalog:10.89.9.22 --add-host somatic-series:10.89.9.23 --add-host daily-metrics:10.89.9.24 --add-host garmin-gateway:10.89.9.25 --add-host apple-health-gateway:10.89.9.26 --add-host analytics:10.89.9.27 --add-host privacy:10.89.9.28 --add-host portability:10.89.9.29 --add-host clients:10.89.9.31 $LBL --env-file secrets/.env -e ASPNETCORE_ENVIRONMENT=Single --memory 256m --pids-limit 512 --read-only --tmpfs /tmp --restart unless-stopped -p 127.0.0.1:8080:8080 ghcr.io/ekstrem/xdiary-api-gateway:$TAG >/dev/null
