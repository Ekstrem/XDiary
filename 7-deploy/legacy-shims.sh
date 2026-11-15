#!/usr/bin/env bash
# legacy-shims.sh — совместимость старых путей деплоя (DO-051 / DO-066).
#
# Зачем: на VPS-1 исторически жили `/home/deploy/deploy.sh` и `/home/deploy/update.sh` —
# рукописные копии с ПАРОЛЯМИ БД в командной строке и `podman rm -f` всех контейнеров.
# Это второй источник правды; живой деплой — `7-deploy/*.sh` из репозитория.
#
# Гейт `deploy.yml` требует на сервере отказ от аргумента-тега (DO-066), иначе деплой
# блокируется (так и было: CI падал на проверке, которой нечего было проверять).
#
# Идемпотентно: сохраняет прежний файл в `/home/deploy/legacy-<ts>/` и ставит шим.
#
#   sudo -n bash 7-deploy/legacy-shims.sh     # на VPS-1 от пользователя deploy
set -euo pipefail

TARGET_DIR="/home/deploy"
TS="$(date +%Y%m%d-%H%M%S)"
BK="$TARGET_DIR/legacy-$TS"

make_shim() {
  local path="$1" delegate="$2" comment="$3"
  cat > "$path" <<SHIM
#!/usr/bin/env bash
# $path — совместимость (DO-051 / DO-066). $comment
#
# Легаси-скрипт заменён: он содержал копию паролей БД в командной строке и делал
# разрушительные операции над всеми контейнерами. Живой путь — репозиторий.
#
# Тег больше НЕ принимается: запрещено: тег задаётся только images.lock (DO-066)
set -euo pipefail
if [ "\$#" -gt 0 ]; then
  echo "запрещено: тег задаётся только images.lock (DO-066)" >&2
  exit 2
fi
exec bash $delegate
SHIM
  chmod +x "$path"
}

mkdir -p "$BK"
for f in deploy.sh update.sh; do
  [ -f "$TARGET_DIR/$f" ] && cp -a "$TARGET_DIR/$f" "$BK/$f" && echo "бэкап: $BK/$f"
done

make_shim "$TARGET_DIR/deploy.sh" "/opt/xdiary/7-deploy/deploy.sh" "Живой деплой — 7-deploy/deploy.sh."
make_shim "$TARGET_DIR/update.sh" "/opt/xdiary/7-deploy/update.sh" "Живой апдейт — 7-deploy/update.sh (читает images.lock)."

for f in "$TARGET_DIR/deploy.sh" "$TARGET_DIR/update.sh"; do
  grep -q 'запрещено: тег задаётся только images.lock' "$f" \
    || { echo "ОШИБКА: $f без отказа от тега" >&2; exit 1; }
  echo "проверено (DO-066): $f"
done

bash "$TARGET_DIR/deploy.sh" single >/dev/null 2>&1 && { echo "ОШИБКА: шим принял тег" >&2; exit 1; } || true
echo "шим отвергает тег (rc!=0) — как требует DO-066"

# DO-124 ч.3 (@DO 2026-11-13 12:2x): пересчитать эталон сторожа `update.sh`.
# `update.sh:139-151` сверяет sha256 шима `/home/deploy/deploy.sh` с эталоном
# `$HOME/.xdiary-deploy-sh.sha256`. Шим мы только что ПЕРЕсоздали (новый текст ⇒ новый
# хеш), а эталон остался от прежнего — отсюда «deploy.sh разошёлся со снимком
# (sha256 8a80296d… != 7b62a233…)» и exit 1 сразу после успешной доставки скриптов.
# Эталон считается от СГЕНЕРИРОВАННОГО текста, поэтому детерминирован и совпадает
# с тем, что вычислит update.sh; осознанная ручная правка по-прежнему требует
# пересчёта этой же командой (сторож не «глохнет», он следует за источником).
HASH_FILE="$TARGET_DIR/.xdiary-deploy-sh.sha256"
sed -E 's/Password=[^;"]*/Password=__REDACTED__/g' "$TARGET_DIR/deploy.sh" \
  | sha256sum | cut -d' ' -f1 > "$HASH_FILE"
echo "эталон deploy.sh пересчитан: $(cat "$HASH_FILE")"

