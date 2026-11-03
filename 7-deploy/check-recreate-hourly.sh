#!/usr/bin/env bash
# check-recreate-hourly.sh — дешёвый контроль DO-048: сколько раз heal пересоздавал
# gateway за последний час. Пишет в лог/журнал ТОЛЬКО если пересоздания были.
#
# Зачем: после фикса `--add-host` окна прекратились (0 пересозданий за 24 мин), но
# «поверить на слово» — не проверка. Если за сутки ноль — DO-054 (podman 4/Ubuntu 24.04)
# остаётся плановой; если появились — возвращаемся к ней как к приоритету.
#
# Ставка: systemd --user, heal-watch.timer (hourly).
set -uo pipefail

HEAL_LOG="${HEAL_LOG:-/home/deploy/heal.log}"
WATCH_LOG="${WATCH_LOG:-/home/deploy/heal-watch.log}"
WINDOW_MIN="${WINDOW_MIN:-60}"

[ -f "$HEAL_LOG" ] || { printf 'check-recreate-hourly: нет %s\n' "$HEAL_LOG" >&2; exit 1; }

# Строки вида: 2026-11-03_10:49:59 gateway /health failed, recreating
# Считаем те, что попали в окно WINDOW_MIN минут от текущего времени (по метке строки).
LAST="$(grep 'recreating' "$HEAL_LOG" | tail -1 | awk '{print $1}')"
COUNT="$(
  HEAL_LOG="$HEAL_LOG" WINDOW_MIN="$WINDOW_MIN" python3 - <<'PY'
import os
import re
import sys
from datetime import datetime, timedelta

path = os.environ["HEAL_LOG"]
window = int(os.environ.get("WINDOW_MIN", "60"))
threshold = datetime.now() - timedelta(minutes=window)
pat = re.compile(r"^(\d{4}-\d\d-\d\d)_(\d\d:\d\d:\d\d)\b.*recreating")
n = 0
try:
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = pat.match(line)
            if not m:
                continue
            try:
                ts = datetime.strptime(m.group(1) + " " + m.group(2), "%Y-%m-%d %H:%M:%S")
            except ValueError:
                continue
            if ts >= threshold:
                n += 1
except OSError:
    pass
print(n)
PY
)" || exit 1

STAMP="$(date '+%Y-%m-%d_%H:%M:%S')"
if [ "${COUNT:-0}" -eq 0 ]; then
  # тишина: ничего не пишем, только отметка последней удачной проверки
  printf '%s ok 0 recreates (последнее когда-либо: %s)\n' "$STAMP" "${LAST:--}" > "$WATCH_LOG.state" 2>/dev/null || true
  exit 0
fi

# Есть пересоздания → пишем строку в лог контроля и в journal, видимое сообщение
printf '%s ALERT recreates=%s за %s мин, последнее в окне/всего: %s (heal.log)\n' "$STAMP" "$COUNT" "$WINDOW_MIN" "${LAST:--}" >> "$WATCH_LOG"
grep 'recreating' "$HEAL_LOG" | tail -5 >> "$WATCH_LOG"
logger -t xdiary-heal "ALERT: $COUNT пересозданий gateway за $WINDOW_MIN мин"
printf 'ALERT: %s пересозданий gateway за %s мин — см. %s\n' "$COUNT" "$WINDOW_MIN" "$WATCH_LOG"
exit 0
