#!/usr/bin/env bash
# recreate-service.sh — пересоздать ОДИН сервис из строки запуска в deploy.sh (DO-052).
#
# Зачем: `deploy.sh` пересоздаёт ВСЕ контейнеры. Для частичного деплоя нужно уметь
# пересоздавать только изменённые — иначе front-only коммит перезапускает бэкенд.
#
# Использование:
#   ./recreate-service.sh <service>              # как в deploy.sh (тег single)
#   ./recreate-service.sh <service> --image <id> # откат на конкретный digest/ID
#   ./recreate-service.sh --list                 # какие сервисы знает deploy.sh
#
# Гарантии:
#   * сначала собираем и валидируем команду (bash -n), только потом удаляем контейнер;
#   * 3 попытки; при провале — печатаем готовую команду восстановления и выходим с 1.
set -uo pipefail

# 2026-11-07: на VPS `/home/deploy/deploy.sh` — ШИМ (DO-066: только `exec` в актуальный
# скрипт), а не сам скрипт. Из-за этого `recreate-service` искал строку запуска в шиме,
# получал ПУСТОЕ тело (1 байт) и падал «контейнер не поднялся» — так `deploy-dev` не ехал
# 5 раундов. Берём реальный скрипт: сначала рядом с этим файлом, потом /opt, потом шим.
if [ -z "${DEPLOY_SH:-}" ]; then
  for cand in "$(dirname "$0")/deploy.sh" /opt/xdiary/7-deploy/deploy.sh /home/deploy/deploy.sh; do
    if [ -f "$cand" ] && ! head -3 "$cand" | grep -q 'exec bash /opt/xdiary/7-deploy'; then
      DEPLOY_SH="$cand"
      break
    fi
  done
  DEPLOY_SH="${DEPLOY_SH:-/home/deploy/deploy.sh}"
fi
# @TL/@AR 2026-11-07: тег берём из images.lock посервисно. Раньше здесь было жёстко
# `TAG=single`, из-за этого `update.sh` (который пинит образы по lock) пересоздавал
# контейнеры на старом `:single` — и dev оставался старше прода.
LOCK_FILE="${LOCK_FILE:-$(dirname "$0")/images.lock}"
tag_of() {
  [ -f "$LOCK_FILE" ] || { echo ""; return; }
  awk -v s="$1" '!/^#/ && NF>=2 && $1==s {print $2; exit}' "$LOCK_FILE"
}
DEPLOY_DIR="${DEPLOY_DIR:-/opt/xdiary/7-deploy}"
# DO-111: строка запуска из deploy.sh содержит ${DB_PASSWORD}; значение берём из secrets/.env,
# иначе при пересоздании одного сервиса переменная окажется пустой и сервис упадёт с 28P01.
if [ -f "$DEPLOY_DIR/secrets/.env" ]; then
  set -a; . "$DEPLOY_DIR/secrets/.env"; set +a
fi
SVCS_ALL="journal partners catalog somatic-series daily-metrics garmin-gateway apple-health-gateway analytics privacy portability client api-gateway"

say() { printf '%s\n' "$*"; }
die() { printf 'recreate-service: ОШИБКА: %s\n' "$*" >&2; exit 1; }

[ -f "$DEPLOY_SH" ] || die "нет $DEPLOY_SH"

if [ "${1:-}" = "--list" ]; then
  for s in $SVCS_ALL; do
    grep -q "podman run -d --name xdiary-$s " "$DEPLOY_SH" && echo "$s"
  done
  exit 0
fi

SVC="${1:-}"

# ---------- ожидаемый набор add-host ----------
gen_expected() {
  # ожидаемый список хостов для сервиса $1 (без самого себя)
  local svc="${1:-$SVC}" gen="" raw=""
  for cand in /opt/xdiary/tools/gen-hosts.py "$(dirname "$DEPLOY_SH")/gen-hosts.py"; do
    [ -f "$cand" ] && { gen="$cand"; break; }
  done
  [ -n "$gen" ] || return 0
  raw="$(python3 "$gen" --add-host 2>/dev/null || true)"
  # DO-128 (@TL 2026-11-13 18:4x): фильтра по ИМЕНИ недостаточно. У сервиса `client`
  # своего имени в списке нет — там `clients:` (псевдоним шлюза, `deploy.sh:78`),
  # поэтому `grep -v "^client:"` ничего не убирал, и клиент получал
  # `--add-host clients:<свой ip>` рядом со своим `--ip` → podman падал на конфликте
  # («error configuring network namespace»), 3 попытки, «client не поднялся».
  # Замер: прогон deploy 43452089927, лог `add-host добавлен для client (11 хостов)`
  # против 10 у остальных. Теперь исключаем ЛЮБОЙ псевдоним, указывающий на свой IP.
  local own_ip=""
  own_ip="$(awk -v n="xdiary-$svc " 'index($0, "podman run -d --name " n) {for (i=1;i<NF;i++) if ($i=="--ip") {print $(i+1); exit}}' \
    "$DEPLOY_SH" 2>/dev/null || true)"
  if [ -n "$own_ip" ]; then
    printf '%s' "$raw" | tr ' ' '\n' | grep -v "^--add-host$" | grep -v "^${svc}:" | grep -v ":$own_ip$" | tr '\n' ' '
  else
    printf '%s' "$raw" | tr ' ' '\n' | grep -v "^--add-host$" | grep -v "^${svc}:" | tr '\n' ' '
  fi
}

# ---------- пост-проверка «флаги реально попали в podman» ----------
# Именно этого не хватило 03.11: `--add-host` встал ПОСЛЕ образа (хвостовой `>/dev/null`),
# скрипт печатал «добавлен», а ExtraHosts оставался пустым. Теперь проверяем конфиг.
check_hosts() {
  local svc="$1" expected actual
  # DO-131 (@DO 2026-11-13 19:4x): сравнивать с ГЕНЕРАТОРОМ неверно — после фильтра «свой ip»
  # и правила «add-host уже в команде — не дублирую» в итоговой команде хостов меньше, чем
  # генерирует `gen-hosts` (прогон #2052: api-gateway ExtraHosts=11 против ожидаемых 12, при том
  # что все нужные хосты, включая `clients`, в контейнере ЕСТЬ). Проверять надо ровно то, что
  # написано в команде: «все флаги из $BODY попали в podman».
  expected="$(grep -o -- '--add-host' "$BODY" 2>/dev/null | wc -l | tr -d ' ')"
  if [ "$expected" -eq 0 ]; then
    say "  check-hosts $svc: ожиданий нет (генератор недоступен) — пропуск"
    return 0
  fi
  if ! podman ps --format '{{.Names}}' | grep -qx "xdiary-$svc"; then
    say "  check-hosts $svc: контейнера нет" >&2
    return 1
  fi
  actual="$(podman inspect "xdiary-$svc" --format '{{len .HostConfig.ExtraHosts}}' 2>/dev/null || echo 0)"
  if [ "${actual:-0}" -lt "$expected" ]; then
    say "ОШИБКА: $svc ExtraHosts=$actual, ожидалось $expected — флаги попали НЕ в podman" >&2
    return 1
  fi
  if [ "$svc" = "api-gateway" ]; then
    podman exec "xdiary-$svc" grep -q ' clients$' /etc/hosts 2>/dev/null \
      || { say "ОШИБКА: у шлюза нет псевдонима clients в /etc/hosts (вход ляжет в 502)" >&2; return 1; }
  fi
  say "  check-hosts $svc: ExtraHosts=$actual (ожидалось $expected), clients есть — ok"
  return 0
}

# ---------- режимы проверки (до разбора <service>) ----------
if [ "${1:-}" = "--check-hosts-all" ]; then
  rc=0
  for s in $SVCS_ALL; do check_hosts "$s" || rc=1; done
  exit $rc
fi
if [ "${1:-}" = "--check-hosts" ]; then
  [ -n "${2:-}" ] || die "нужно: --check-hosts <service>"
  check_hosts "$2"
  exit $?
fi

SVC="${1:-}"
IMAGE_OVERRIDE=""
if [ "${2:-}" = "--image" ]; then IMAGE_OVERRIDE="${3:-}"; [ -n "$IMAGE_OVERRIDE" ] || die "пустой --image"; fi

CONTAINER="xdiary-$SVC"
cd "$DEPLOY_DIR" || die "нет $DEPLOY_DIR"

# DO-129 (@TL 2026-11-13 18:5x): фиксированные `/tmp/recreate-<svc>.*` ломали ОТКАТ:
# в логе прод-прогона 43455070581 — `PermissionError: [Errno 13] Permission denied:
# '/tmp/recreate-partners.body'`, из-за чего «откат partners не удался (нужно вручную)».
# Файл остаётся от предыдущего запуска (другой пользователь/владелец) — берём mktemp.
BODY="$(mktemp "/tmp/recreate-$SVC.body.XXXXXX")"
RUNNER="$(mktemp "/tmp/recreate-$SVC.sh.XXXXXX")"

python3 - "$DEPLOY_SH" "$SVC" "$BODY" <<'PY'
import re
import sys
dep, svc, out = sys.argv[1:4]
src = open(dep, encoding='utf-8').read().replace('\\\n', ' ')
needle = 'podman run -d --name xdiary-%s ' % svc
line = next((l for l in src.splitlines() if needle in l), '')
line = line.strip()
# Убираем хвостовые редиректы ДО разбора «последний аргумент = образ».
# Без этого `--image` подменял не образ, а `>/dev/null`: подмена уходила в конец,
# а флаги `--add-host` вставали после настоящего образа → попадали в Cmd
# контейнера, и откат «успешно» создавал контейнер на СТАРОМ образе без add-host
# (инцидент 2026-11-09 22:50: prod client остался без link-partner).
line = re.sub(r'(\s*(?:2?>\s*/dev/null|&>\s*/dev/null|2>&1))+\s*$', '', line)
open(out, 'w', encoding='utf-8').write(line + '\n')
PY

[ -s "$BODY" ] || die "в $DEPLOY_SH нет команды для $SVC — контейнер не трогаю"

# подмена образа для отката: последний аргумент строки — ghcr.io/...:<tag>
if [ -n "$IMAGE_OVERRIDE" ]; then
  python3 - "$BODY" "$IMAGE_OVERRIDE" <<'PY'
import sys
path, img = sys.argv[1:3]
line = open(path, encoding='utf-8').read().strip()
parts = line.rsplit(' ', 1)
assert len(parts) == 2, 'нет образа в конце строки'
open(path, 'w', encoding='utf-8').write(parts[0] + ' ' + img + '\n')
PY
  # контроль: последний токен — действительно образ (а не флаг/редирект)
  # DO-127 (@DO 2026-11-13 17:4x): сравнивать «как есть» нельзя — podman отдаёт ID в разных
  # формах (`sha256:<64>`, `<64>`, короткие 12), плюс возможен `\r`/пробел из `podman ps --format`.
  # В прогоне #2039 оба значения в сообщении выглядели ОДИНАКОВО, а `[ "$a" = "$b" ]` падал →
  # откат не срабатывал, и `client` оставался снятым (на стенде до сих пор нет контейнера).
  # Нормализуем обе стороны и сравниваем по 12 hex-символам (podman принимает короткий ID).
  last_tok="$(tail -1 "$BODY" | awk '{print $NF}')"
  norm_id() { printf '%s' "$1" | tr -d '\r\n \t' | sed -E 's/^.*sha256://; s/^.*@//' | cut -c1-12; }
  lt="$(norm_id "$last_tok")"
  io="$(norm_id "$IMAGE_OVERRIDE")"
  if [ -n "$lt" ] && [ "$lt" = "$io" ]; then
    say "  образ переопределён на $IMAGE_OVERRIDE (откат)"
  else
    die "подмена образа не удалась: в конце строки '$last_tok' (hex ${lt:-пусто}), ожидали '$IMAGE_OVERRIDE' (hex ${io:-пусто}) — контейнер не трогаю"
  fi
fi

# DO-048: сервисы обращаются к соседям по КОРОТКОМУ имени (`journal:8080`), а CNI-DNS
# отдаёт только `xdiary-journal.dns.podman`. В `deploy.sh` `--add-host` есть ТОЛЬКО у
# api-gateway, поэтому у analytics падал precompute:
# `SocketException: Name or service not known (journal:8080)`.
# Добавляем add-host автоматически из gen-hosts.py --add-host — для любого сервиса.
GEN=""
for cand in /opt/xdiary/tools/gen-hosts.py "$(dirname "$DEPLOY_SH")/gen-hosts.py"; do
  [ -f "$cand" ] && { GEN="$cand"; break; }
done
ADD_HOSTS="$(gen_expected)"

{ echo 'set -u'; echo 'NET=xdiary'
  echo 'LBL="--label io.containers.autoupdate=registry"'
  lock_tag="$(tag_of "$SVC" 2>/dev/null || true)"
  [ -n "$lock_tag" ] || lock_tag="single"
  echo "TAG=$lock_tag"
  cat "$BODY"; } > "$RUNNER"

if [ -n "$ADD_HOSTS" ] && ! grep -q -- '--add-host' "$RUNNER"; then
  # вставляем --add-host ... перед образом (последний аргумент строки)
  python3 - "$RUNNER" "$ADD_HOSTS" <<'PY'
import sys
runner, hosts = sys.argv[1:3]
lines = open(runner, encoding='utf-8').read().splitlines()
out = []
for l in lines:
    if 'podman run -d --name' in l:
        parts = l.rsplit(' ', 1)
        if len(parts) == 2:
            add = ' '.join('--add-host ' + h for h in hosts.split() if ':' in h)
            l = parts[0] + ' ' + add + ' ' + parts[1]
    out.append(l)
open(runner, 'w', encoding='utf-8').write('\n'.join(out) + '\n')
PY
  say "  add-host добавлен для $SVC ($(printf '%s' "$ADD_HOSTS" | wc -w) хостов)"
elif grep -q -- '--add-host' "$RUNNER"; then
  say "  add-host уже в команде $SVC — не дублирую"
fi

bash -n "$RUNNER" || die "собранная команда не проходит bash -n — контейнер не трогаю"

# 2026-11-10 (@DO): ПОРЯДОК ВАЖЕН — образ проверяем/тянем ДО удаления контейнера.
# Инцидент: lock указывал на новый тег, которого не было локально; скрипт удалил старый
# контейнер, три попытки поднять новый провалились, и сервис journal лежал (504).
# Теперь: нет образа локально → `podman pull`; не удалось → выход без удаления старого.
if [ -n "${lock_tag:-}" ]; then
  img="ghcr.io/ekstrem/xdiary-$SVC:$lock_tag"
  if ! sudo -n podman image inspect "$img" >/dev/null 2>&1; then
    echo "  образа $img нет локально — pull"
    if ! sudo -n podman pull -q "$img" >/dev/null 2>&1; then
      echo "  ОШИБКА: не удалось скачать $img — контейнер НЕ трогаю" >&2
      exit 3
    fi
  fi
fi
# DO-129 (@TL 2026-11-13 19:4x): СЕТЬ ПРОВЕРЯЕМ ДО УДАЛЕНИЯ КОНТЕЙНЕРА.
# Прод-инцидент 43461987716: сети `xdiary` на VPS-1 не оказалось
# (`unable to find network configuration for xdiary: network not found`), а скрипт уже
# снял контейнеры — в итоге прод отвечал `POST /api/v1/clients → 500`, потому что сервисы
# остались снятыми, и откат тоже не мог их запустить. В репозитории сеть `xdiary`
# НИКТО не создаёт (создаётся только `apim` в `apim/up.sh`) — она была создана руками.
# Теперь: нет сети → пробуем создать (подсеть как в `deploy.sh`: 10.89.9.0/24),
# не вышло → ВЫХОДИМ, не трогая работающий контейнер.
if ! podman network exists xdiary >/dev/null 2>&1; then
  echo "  сети xdiary нет — создаю (подсеть 10.89.9.0/24, как в deploy.sh)"
  # DO-130 (@DO): контейнеры живут в ROOTLESS-пространстве. Если создавать сеть через
  # `sudo podman`, получается ROOTFUL-сеть (и её conflist в /etc/cni/net.d) — контейнеры
  # её не видят, и мы получаем «сеть есть, а контейнер не стартует». Поэтому порядок
  # ОБРАТНЫЙ: сначала rootless (тот же пользователь, что запускает контейнеры), sudo — только
  # как запасной путь.
  podman network create --subnet 10.89.9.0/24 xdiary >/dev/null 2>&1 || \
    sudo -n podman network create --subnet 10.89.9.0/24 xdiary >/dev/null 2>&1 || true
  if ! podman network exists xdiary >/dev/null 2>&1; then
    echo "  ОШИБКА: сети xdiary нет и создать не удалось — контейнер НЕ трогаю" >&2
    exit 4
  fi
  echo "  сеть xdiary создана"
fi
# DO-129 ч.4 (@TL 2026-11-13 20:4x): `podman network create` пишет conflist с
# `"cniVersion": "1.0.0"`, а плагин `firewall` в этой версии podman его не принимает:
# `log| Error validating CNI config file /etc/cni/net.d/xdiary.conflist:
#  [plugin firewall does not support config version "1.0.0"]` (прогон 43473245196).
# Именно поэтому сеть «есть», а контейнеры не поднимаются. Правим на 0.4.0 — ровно тот
# обход, который описан в `7-deploy/apim/up.sh` и `7-deploy/podman-compose.yml`.
# conflist может лежать в двух местах: rootless (`~/.config/cni/net.d`) или rootful
# (`/etc/cni/net.d`). Правим ОБА, если они есть, — иначе правка уходит не туда.
CNI_LIST=""
for c in "${HOME:-/root}/.config/cni/net.d/xdiary.conflist" /etc/cni/net.d/xdiary.conflist; do
  [ -f "$c" ] && CNI_LIST="$CNI_LIST $c"
done
CNI="${CNI_LIST%% *}" 
for c in $CNI_LIST; do
  grep -q '"cniVersion": "1.0.0"' "$c" 2>/dev/null || continue
  echo "  CNI: правлю cniVersion 1.0.0 -> 0.4.0 в $c (firewall не принимает 1.0.0)"
  sed -i 's/"cniVersion": *"1.0.0"/"cniVersion": "0.4.0"/' "$c" 2>/dev/null || \
    sudo -n sed -i 's/"cniVersion": *"1.0.0"/"cniVersion": "0.4.0"/' "$c" 2>/dev/null || true
  if grep -q '"cniVersion": "1.0.0"' "$c" 2>/dev/null; then
    echo "  ОШИБКА: cniVersion всё ещё 1.0.0 в $c — контейнер НЕ трогаю (иначе снесём рабочий)" >&2
    exit 5
  fi
done
podman rm -f "$CONTAINER" >/dev/null 2>&1 || true
# 2026-11-07: CNI не всегда успевает освободить IP сразу после `rm -f`, и первый
# `podman run` падает с "error configuring network namespace". В `update.sh` это
# выглядело как «сервис не поднялся» (portability: 3 попытки в одну секунду) и
# откатывало весь набор. Даём сети освободить адрес перед запуском.
sleep 2
attempt=1
while [ "$attempt" -le 3 ]; do
  if bash "$RUNNER" >/dev/null 2>&1; then
    sleep 3
    if podman ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
      say "  OK: $CONTAINER пересоздан"
      check_hosts "$SVC" || exit 1
      # DO-051 п.2 (@TL 11:45): «пересоздал» = «проверил». Только для шлюза:
      # ExtraHosts=12 ещё не значит, что вход отвечает (псевдоним `clients` мог
      # потеряться -> 502 при живом /health). Проверяем функционально.
      if [ "$SVC" = "api-gateway" ]; then
        sleep 3
        g=$(curl -s -o /dev/null -w '%{http_code}' -m 10 http://127.0.0.1:8080/api/v1/clients || echo 000)
        p=$(curl -s -o /dev/null -w '%{http_code}' -m 10 -X POST -H 'Content-Type: application/json' \
              -d '{}' http://127.0.0.1:8080/api/v1/clients || echo 000)
        case "$g" in 200|401) ;; *) say "ОШИБКА: GET /api/v1/clients = $g (ждали 200/401; 502 = нет псевдонима clients)" >&2; exit 1;; esac
        case "$p" in 200|201|400) ;; *) say "ОШИБКА: POST /api/v1/clients = $p (ждали 200/201/400; 502 = нет псевдонима clients)" >&2; exit 1;; esac
        say "  вход отвечает: GET /clients=$g, POST /clients=$p"
      fi
      exit 0
    fi
  fi
  say "  попытка $attempt не удалась, повтор…"
  attempt=$((attempt + 1))
  sleep 5
done
# DO-128 (@TL 2026-11-13 19:2x): при провале печатаем ПОЧЕМУ сервис не поднялся.
# Раньше на этом месте была только строка «не поднялся», и три прогона подряд
# (43455070581, 43457921240, 43459800205) разбирались вслепую: без `podman logs`
# нельзя отличить «нет Jwt__SigningKey» от «конфликт сети» или «упал на миграциях».
say "  диагностика $CONTAINER (stderr podman run и логи):" >&2
if [ -f "$RUNNER" ]; then bash "$RUNNER" 2>&1 | tail -n 20 | sed 's/^/    run| /' >&2 || true; fi
podman inspect "$CONTAINER" --format '    exit={{.State.ExitCode}} err={{.State.Error}}' 2>/dev/null >&2 || true
podman logs --tail 40 "$CONTAINER" 2>&1 | sed 's/^/    log| /' >&2 || true
die "$CONTAINER не поднялся. Восстановить: bash $RUNNER"
